# NA-04 — Identificações físicas do Animal — Design

**Redmine:** #296
**Dependência:** NA-03 — Animal base, integrada à `main`
**Status:** aprovado para especificação; implementação depende da aprovação deste documento e do plano técnico.

## Objetivo

Permitir que um `Animal` tenha zero ou mais identificações físicas de manejo, com histórico preservado, pesquisa consistente e garantias de integridade sob concorrência. `Animal.Id` continua sendo a identidade técnica imutável e `Animal.CodigoInterno` continua sendo a referência operacional; nenhum dos dois é substituído pela nova estrutura.

## Decisão

Será criado um agregado subordinado `IdentificacaoAnimal`, persistido em tabela própria. As mutações e as consultas no contexto de um animal conhecido serão acessadas por endpoints aninhados; uma consulta global somente leitura permitirá descobrir o animal a partir de sua identificação física. A entidade não será incorporada ao cadastro-base nem representará registros institucionais.

O domínio preserva a identidade histórica de cada marcador: depois da criação, `Tipo`, `DescricaoTipo` e `Valor` não poderão ser alterados. Trocas físicas serão feitas inativando o registro anterior e criando outro. Os únicos campos editáveis são `Principal`, `DataAplicacao`, `Observacao` e `Ativo`, por operações que reforçam suas invariantes.

### Alternativas consideradas

1. **Colunas diretamente em `Animal`:** rejeitada porque limita o animal a uma identificação, perde histórico e conflita com a cardinalidade `0..N`.
2. **Cadastro genérico de atributos ou identificadores institucionais:** rejeitada porque mistura marcador físico com SISBOV, UELN, pedigree e registros de entidades externas, todos fora do escopo da NA-04.
3. **Entidade subordinada com índices PostgreSQL de defesa:** adotada porque mantém o domínio explícito, permite histórico e torna a integridade independente de validações prévias da aplicação.

## Modelo de domínio

```text
Animal (1) ──── (0..N) IdentificacaoAnimal

IdentificacaoAnimal
- Id: Guid                         imutável
- AnimalId: Guid                   obrigatório; FK para Animal.Id
- Tipo: TipoIdentificacaoAnimal    imutável
- DescricaoTipo: string?           imutável; obrigatória somente em Outro
- Valor: string                    imutável
- Principal: bool                  editável, somente quando Ativo
- DataAplicacao: DateOnly?         editável
- Observacao: string?              editável
- Ativo: bool                      editável
- CreatedAtUtc / UpdatedAtUtc      conforme padrão do projeto
```

O enum inicial será `Anilha`, `Brinco`, `Microchip`, `Tatuagem`, `Marca` e `Outro`. `DescricaoTipo` será `null` para todos os tipos exceto `Outro`; para `Outro`, será obrigatória. `Marca` é um marcador físico permanente ou semipermanente, sem detalhamento de técnica nesta demanda.

### Normalização

`Valor` e, quando aplicável, `DescricaoTipo` terão apenas as extremidades removidas por `Trim`. Espaços internos, hífens, barras, pontos, zeros e prefixos não serão removidos nem reformatados. A comparação será case-insensitive; a representação trimada fornecida pelo usuário será preservada para exibição.

### Invariantes

- `AnimalId` deve referenciar um animal existente.
- `Tipo` deve pertencer ao enum.
- `Valor` não pode ficar vazio após `Trim`.
- `DescricaoTipo` é exigida exclusivamente quando `Tipo == Outro`; nos demais tipos é persistida como `null`.
- Há no máximo uma identificação simultaneamente `Ativo == true` e `Principal == true` por animal. A primeira identificação nunca é promovida automaticamente.
- Inativar uma principal também a torna não principal; nenhuma sucessora é promovida automaticamente.
- Reativar só é permitido no mesmo `AnimalId`, no próprio registro; não o torna principal automaticamente.
- Definir uma identificação como principal exige que ela já esteja ativa e não altera `Ativo`. Uma identificação inativa deve ser reativada explicitamente antes dessa operação.
- Não haverá `DELETE` exposto pela API.

## Persistência e concorrência

A migração criará `IdentificacoesAnimal` com chave primária `Id`, FK obrigatória para `Animais(Id)` com `DeleteBehavior.Restrict`, tipos `uuid`, `date`, `boolean` e tamanhos de texto explícitos. As regras dependentes de banco serão materializadas como constraints e índices PostgreSQL nomeados:

- `CK_IdentificacoesAnimal_Tipo`: valores válidos do enum.
- `CK_IdentificacoesAnimal_Valor_Canonical`: valor não vazio e sem espaços nas extremidades.
- `CK_IdentificacoesAnimal_DescricaoTipo_Semantics`: `DescricaoTipo` presente e canônica somente para `Outro`, ausente nos demais tipos.
- `UX_IdentificacoesAnimal_Tipo_Valor_CaseInsensitive`: índice único por expressão para `Tipo + lower(Valor)` quando o tipo não for `Outro`.
- `UX_IdentificacoesAnimal_Outro_DescricaoTipo_Valor_CaseInsensitive`: índice único por expressão para `Tipo + lower(DescricaoTipo) + lower(Valor)` quando o tipo for `Outro`.
- `UX_IdentificacoesAnimal_Animal_PrincipalAtiva`: índice único parcial por `AnimalId` onde `Ativo` e `Principal` forem verdadeiros.

Esses índices preservam a unicidade mesmo depois de inativação e impedem a duplicidade histórica entre animais diferentes. A camada de aplicação fará pré-verificação para mensagens de domínio; a infraestrutura converterá violações dos índices nomeados em conflitos determinísticos, sem vazar exceções do PostgreSQL.

Operações que possam alterar `Principal` ou inativar uma principal executarão em transação e bloquearão a linha do `Animal` pai durante a decisão. Para definir principal, a operação exige que a identificação escolhida já esteja ativa; na mesma transação, limpa `Principal` da principal ativa anterior, se existir, e define `Principal=true` na identificação ativa escolhida, sem alterar `Ativo`. A reativação é uma operação distinta e não promove automaticamente a identificação a principal. O bloqueio serializa concorrentes da mesma chave de animal; o índice parcial permanece como defesa final de persistência.

## Aplicação e API

Será criado um módulo paralelo aos módulos existentes de Animal, com contratos de criação, atualização de metadados, resultados, filtros, paginação, serviço, repositório e exceções de domínio. O resultado da consulta global incluirá o resumo mínimo do animal associado, sem transformar a identificação em substituta do cadastro de Animal.

Os endpoints autenticados ficarão sob `/api/v1/animais/{animalId}/identificacoes`:

| Operação | Rota | Semântica |
| --- | --- | --- |
| Criar | `POST /` | Cria o marcador; `Principal=true` aplica a troca atômica. |
| Listar/pesquisar | `GET /?tipo&valor&ativo&principal&page&pageSize` | Filtra por tipo, texto case-insensitive do valor, atividade e principal. |
| Consultar | `GET /{identificacaoId}` | Obtém uma identificação pertencente ao animal da rota. |
| Editar metadados | `PATCH /{identificacaoId}` | Altera somente `DataAplicacao` e `Observacao`. |
| Ativar/inativar | `PATCH /{identificacaoId}/ativo` | Inativar limpa `Principal`; reativar não a promove. |
| Definir/remover principal | `PATCH /{identificacaoId}/principal` | Exige identificação ativa para definir como principal e faz a troca atômica. |

Para `PATCH /{identificacaoId}`, a semântica individual de `DataAplicacao` e `Observacao` será: propriedade ausente não altera o valor persistido; propriedade presente com valor o substitui; propriedade presente com `null` limpa o valor persistido. O futuro plano técnico escolherá uma representação de contrato que preserve esses três estados sem ambiguidade. `Tipo`, `DescricaoTipo` e `Valor` permanecem imutáveis após a criação.

Haverá também a rota global autenticada e somente leitura `GET /api/v1/identificacoes-animal?tipo&valor&ativo&principal&page&pageSize`. Ela pesquisa exclusivamente o recurso `IdentificacaoAnimal`, independentemente de `AnimalId`, com comparação case-insensitive coerente com a normalização de `Valor`, paginação conforme os padrões do projeto e o resumo mínimo do animal associado em cada resultado. Ela permite descobrir o animal a partir de anilha, brinco ou microchip conhecidos, preservando `ProblemDetails` e os demais padrões de resposta já estabelecidos. Não haverá `POST`, `PATCH`, `PUT` ou `DELETE` globais: todas as mutações permanecem exclusivamente em `/api/v1/animais/{animalId}/identificacoes`. A consulta aninhada continua sendo o contexto de um animal conhecido; a rota global não transforma a listagem de animais em busca universal do ERP. `Animal.CodigoInterno`, `IdentificacaoAnimal` e o registro institucional futuro #297 permanecem conceitos separados.

Entradas inválidas recebem `400`, animal ou identificação inexistentes (ou identificação fora do animal da rota) recebem `404`, e violações de unicidade ou concorrência recebem `409` com `ProblemDetails` estáveis e sem detalhes internos.

## Interface

A edição/detalhe do animal terá uma área própria intitulada **Identificações físicas**, visualmente separada de **Código interno**. A página de criação não exibirá esse gerenciamento, pois o `AnimalId` ainda não existe; após salvar o animal, a edição permitirá:

- listar identificações ativas e históricas, com estado e destaque para principal;
- criar um marcador e informar o tipo, a descrição de tipo quando for `Outro`, valor, data e observação;
- consultar o registro;
- editar somente data e observação;
- escolher/remover principal de uma identificação ativa;
- inativar e reativar sem remoção física.

Registros inativos continuarão acessíveis, mas receberão rótulo visual de histórico/inativo para não serem confundidos com o marcador operacional atual. Os textos da UI deixarão claro que uma identificação física não é SISBOV, UELN ou registro de associação.

## Testes e validação

O plano de implementação cobrirá, antes do código de produção:

1. Regras de domínio para criação, `Outro`, imutabilidade, ativação/inativação e principal.
2. Serviço para animal inexistente, pré-conflitos, escopo por `AnimalId`, escolha explícita de principal sem reativação implícita e reativação sem promoção automática.
3. Repositório/migração PostgreSQL para FK, índices parciais, unicidade normalizada e conversão de conflitos persistidos.
4. Testes de concorrência PostgreSQL para duas tentativas de definir principal e para duplicidade de identificador.
5. API para todos os status, filtros, consulta global autenticada por identificação com resumo do animal, inexistência de mutações globais e ausência de `DELETE`.
6. Contrato e API de edição de metadados para os três estados individuais de `DataAplicacao` e `Observacao`: ausente, valor e `null` explícito.
7. Frontend para contratos, painel, criação, edição de metadados, histórico e mensagens de erro sem vazamento técnico.
8. Gates completos: `dotnet test GenSW.sln`, `npm test`, `npm run lint` e `npm run build`.

## Fora do escopo

Não serão implementados registros institucionais (#297), pedigree/filiação, cruzamentos, RFID, QR Code, leitores, impressão, código de barras, estoque de marcadores, propriedade/localização/lote, genética, produção ou status adicionais como perdido, danificado ou substituído.

## Critérios de aceite

O resultado será aceito quando um animal puder manter, por exemplo, uma anilha ativa principal, um microchip ativo e uma anilha inativa histórica; quando identificações iguais normalizadas não puderem pertencer a animais distintos; quando uma identificação inativa não puder ser definida como principal sem reativação explícita e essa reativação não a promover automaticamente; quando não houver duas principais ativas; quando uma identificação física puder localizar seu animal pela consulta global somente leitura; quando a edição de `DataAplicacao` e `Observacao` distinguir propriedade ausente, valor e `null` explícito; e quando a UI, API, migração e testes preservarem essa separação sem exclusão física.
