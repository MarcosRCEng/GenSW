# NA-04 — Identificações físicas do Animal — Design

**Redmine:** #296
**Dependência:** NA-03 — Animal base, integrada à `main`
**Status:** aprovado para especificação; implementação depende da aprovação deste documento e do plano técnico.

## Objetivo

Permitir que um `Animal` tenha zero ou mais identificações físicas de manejo, com histórico preservado, pesquisa consistente e garantias de integridade sob concorrência. `Animal.Id` continua sendo a identidade técnica imutável e `Animal.CodigoInterno` continua sendo a referência operacional; nenhum dos dois é substituído pela nova estrutura.

## Decisão

Será criado um agregado subordinado `IdentificacaoAnimal`, persistido em tabela própria e acessado por endpoints aninhados no contexto do animal. A entidade não será incorporada ao cadastro-base nem representará registros institucionais.

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

Operações que possam alterar `Principal` ou inativar uma principal executarão em transação e bloquearão a linha do `Animal` pai durante a decisão. Definir principal limpará a principal ativa anterior e ativará a escolhida na mesma transação. O bloqueio serializa concorrentes da mesma chave de animal; o índice parcial permanece como defesa final de persistência.

## Aplicação e API

Será criado um módulo paralelo aos módulos existentes de Animal, com contratos de criação, atualização de metadados, resultados, filtros, paginação, serviço, repositório e exceções de domínio. O resultado incluirá o resumo do animal necessário para consulta por identificação, sem transformar a identificação em substituta do cadastro de Animal.

Os endpoints autenticados ficarão sob `/api/v1/animais/{animalId}/identificacoes`:

| Operação | Rota | Semântica |
| --- | --- | --- |
| Criar | `POST /` | Cria o marcador; `Principal=true` aplica a troca atômica. |
| Listar/pesquisar | `GET /?tipo&valor&ativo&principal&page&pageSize` | Filtra por tipo, texto case-insensitive do valor, atividade e principal. |
| Consultar | `GET /{identificacaoId}` | Obtém uma identificação pertencente ao animal da rota. |
| Editar metadados | `PATCH /{identificacaoId}` | Altera somente `DataAplicacao` e `Observacao`. |
| Ativar/inativar | `PATCH /{identificacaoId}/ativo` | Inativar limpa `Principal`; reativar não a promove. |
| Definir/remover principal | `PATCH /{identificacaoId}/principal` | Exige identificação ativa para definir como principal e faz a troca atômica. |

Entradas inválidas recebem `400`, animal ou identificação inexistentes (ou identificação fora do animal da rota) recebem `404`, e violações de unicidade ou concorrência recebem `409` com `ProblemDetails` estáveis e sem detalhes internos. A listagem de animais existente não ganhará uma busca universal; a descoberta por identificação ocorrerá pela consulta do novo recurso, que devolve a associação com seu animal.

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
2. Serviço para animal inexistente, pré-conflitos, escopo por `AnimalId` e escolha explícita de principal.
3. Repositório/migração PostgreSQL para FK, índices parciais, unicidade normalizada e conversão de conflitos persistidos.
4. Testes de concorrência PostgreSQL para duas tentativas de definir principal e para duplicidade de identificador.
5. API para todos os status, filtros e ausência de `DELETE`.
6. Frontend para contratos, painel, criação, edição de metadados, histórico e mensagens de erro sem vazamento técnico.
7. Gates completos: `dotnet test GenSW.sln`, `npm test`, `npm run lint` e `npm run build`.

## Fora do escopo

Não serão implementados registros institucionais (#297), pedigree/filiação, cruzamentos, RFID, QR Code, leitores, impressão, código de barras, estoque de marcadores, propriedade/localização/lote, genética, produção ou status adicionais como perdido, danificado ou substituído.

## Critérios de aceite

O resultado será aceito quando um animal puder manter, por exemplo, uma anilha ativa principal, um microchip ativo e uma anilha inativa histórica; quando identificações iguais normalizadas não puderem pertencer a animais distintos; quando não houver duas principais ativas; e quando a UI, API, migração e testes preservarem essa separação sem exclusão física.
