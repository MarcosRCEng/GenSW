# NA-03 — Animal base: design

**Status:** `NA_03_SPEC_PROPOSTA`, aguardando validação humana do artefato; sem implementação nesta fase
**Redmine:** #295, filha da Evolução #291
**Base analisada:** `main` em `0445b482c4f8f5362a2d7a3a00cdd6c7236dc914`
**Dependências integradas:** NA-01 (#293, Espécies) e NA-02 (#294, Raças e Variedades)

## 1. Objetivo

Definir o desenho revisável do agregado `Animal`: seu cadastro individual, referência operacional por `CodigoInterno`, classificações, lifecycle, contratos de aplicação/API, persistência PostgreSQL/EF Core, frontend e estratégia de testes.

Esta SPEC é a fonte para um plano de implementação posterior. Ela não cria código, migration, branch de implementação nem qualquer artefato das demandas #296–#299.

`Animal.Id` é a identidade técnica permanente. `CodigoInterno` é a referência operacional principal, globalmente única e editável; não é chave técnica, não é FK futura e não codifica espécie, sexo, propriedade, raça, ano ou outra semântica de negócio.

## 2. Escopo desta fase

Inclui:

- cadastro, consulta, listagem, edição, inativação e reativação de um indivíduo `Animal`;
- geração automática concorrente de `CodigoInterno` quando ele for omitido na criação;
- código informado manualmente, sua unicidade global e sua edição;
- vínculo obrigatório com `Especie` e vínculos independentes opcionais com `Raca` e `Variedade`;
- os enums `SexoAnimal` e `EscopoAnimal`;
- telas mínimas de Animais, Novo Animal e Editar Animal;
- proteção estrutural contra alterar a espécie de uma Raça ou Variedade já usada por Animal.

O desenho segue os módulos verticais existentes e seus contratos específicos; não introduz repositório genérico, CQRS/MediatR, biblioteca de formulário/estado, nova abstração de classificação ou tabela genérica de atributos.

## 3. Modelo de domínio

`Animal` é um agregado de indivíduo. Ele não tem navegações de domínio para `Especie`, `Raca` ou `Variedade`; a consistência entre agregados é orquestrada pela Application e reforçada pelo banco.

| Campo | Tipo | Regra |
|---|---|---|
| `Id` | `Guid` | Identidade técnica nova, imutável e não operacional. |
| `CodigoInterno` | `string` | Obrigatório no estado persistido; 1–64 caracteres canônicos; globalmente único sem distinguir caixa. |
| `Nome` | `string?` | Opcional; até 200 caracteres canônicos; não é único. |
| `EspecieId` | `Guid` | Obrigatório. |
| `RacaId` | `Guid?` | Opcional e diretamente relacionada a `EspecieId`. |
| `VariedadeId` | `Guid?` | Opcional e diretamente relacionada a `EspecieId`. |
| `Sexo` | `SexoAnimal` | Obrigatório: `Macho = 1`, `Femea = 2`, `Indeterminado = 3`. |
| `DataNascimento` | `DateOnly?` | Opcional; não persiste idade. Data futura em UTC é inválida. |
| `Escopo` | `EscopoAnimal` | Obrigatório: `Operacional = 1`, `Referencia = 2`. |
| `Ativo` | `bool` | Começa em `true`; representa somente participação operacional corrente. |
| `CreatedAtUtc` / `UpdatedAtUtc` | `DateTimeOffset` | Obrigatórios e UTC, seguindo o padrão atual. |

`SexoAnimal` e `EscopoAnimal` são enums próprios de `Animal`. `Referencia` não é subtipo e não produz tabela, endpoint ou lifecycle diferente: é o mesmo agregado, com o mesmo conjunto de campos e regras.

O domínio valida regras locais: valores de enum definidos, `Guid` obrigatório não vazio, normalização de texto, limites, data de nascimento e lifecycle. Ele oferece `Criar`, `AlterarCadastro`, `Inativar` e `Reativar`. Como em `Especie`, `Raca` e `Variedade`, as operações de lifecycle são idempotentes e uma edição sem alteração efetiva preserva `UpdatedAtUtc`.

## 4. Invariantes

- Um Animal persistido sempre possui `CodigoInterno`, `EspecieId`, `Sexo`, `Escopo`, `Ativo` e timestamps.
- `CodigoInterno` manual é normalizado somente quanto a espaços: sem vazio, bordas ou sequências internas de espaços não canônicas. A caixa apresentada é preservada, mas `AN-000001` e `an-000001` são o mesmo código para unicidade.
- Em `POST`, apenas `null` ou a omissão de `codigoInterno` pedem geração automática. String vazia ou apenas espaços é `400`, não pedido implícito de geração. Em `PUT`, `codigoInterno` é obrigatório; não existe regeneração automática durante edição.
- `Nome` nulo ou vazio após normalização é persistido como `null`; não há unicidade ou código derivado do nome.
- Raça e Variedade são independentes: qualquer uma, ambas ou nenhuma podem ser informadas. Nunca se deduz uma pela outra.
- Um vínculo classificatório novo deve apontar a uma classificação existente, ativa e pertencente à espécie do Animal. A manutenção de um vínculo histórico já existente não revalida o estado ativo dessa classificação.
- Não existe DELETE físico para Animal. Inatividade não significa morte, venda, descarte, transferência ou evento biológico.
- `Sexo` e `Escopo` permanecem editáveis. Filiação, pedigree e cruzamentos futuros poderão acrescentar regras de compatibilidade; eles não bloqueiam a edição nesta demanda.

## 5. `CodigoInterno` e concorrência

### Decisão recomendada

Usar uma `SEQUENCE` PostgreSQL dedicada, global e sem ciclo, chamada conceitualmente `AnimalCodigoInternoSequence`:

- tipo `bigint`, início em `1`, incremento em `1`, mínimo `1`, `NO CYCLE` e `CACHE 1`;
- criada e removida pela migration aditiva, por SQL PostgreSQL explícito, tal como os índices funcionais já adotados pelo projeto;
- utilizada somente para criação automática. Ela não é `DEFAULT` da coluna textual e não está ligada a uma coluna de identidade.

O gerador obtém `n` com `nextval`, então produz `AN-` + a representação decimal invariante de `n`, preenchida para **no mínimo** seis dígitos. A formatação deve equivaler a `n.ToString("D6", InvariantCulture)`: `1 → AN-000001`, `999999 → AN-999999`, `1000000 → AN-1000000`. Não usar `lpad(valor, 6, '0')` isoladamente, pois ele pode truncar números maiores que o tamanho pedido.

O valor numérico é interno ao alocador PostgreSQL e não é uma propriedade nem uma coluna de `Animal`. O limite natural é `bigint`, portanto o maior formato automático possível é `AN-9223372036854775807`; a largura não é limitada a seis dígitos. Ao esgotar a sequence, a Application devolve uma exceção específica de exaustão, mapeada para `409 Conflict`, e não tenta reciclar valores.

`nextval` aloca valores distintos de forma atômica entre sessões. Sequências PostgreSQL não voltam em rollback e podem apresentar lacunas por rollback, colisão automática com código manual, falha ou crash; a ordem de commit também pode diferir da ordem de alocação. Para esta demanda, “sequencial” significa valores automáticos distintos e crescentes na alocação, não numeração fiscal sem lacunas. Esse comportamento é documentado pelo PostgreSQL em [Sequence Manipulation Functions](https://www.postgresql.org/docs/current/functions-sequence.html) e [CREATE SEQUENCE](https://www.postgresql.org/docs/current/sql-createsequence.html).

### Código manual e colisões

O índice `UX_Animais_CodigoInterno_CaseInsensitive` é a autoridade final para todos os códigos, automáticos ou manuais. A Application pode pré-validar para uma mensagem rápida, mas nunca usa a pré-validação como garantia de concorrência.

Um código manual que se pareça com código automático, por exemplo `AN-000001`, é permitido quando livre. Ele não chama `setval` nem altera a sequence; quando uma criação automática alcançar esse mesmo texto, a restrição única detectará a colisão.

Para uma criação automática, a repetição é estritamente limitada a este caso conhecido:

1. alocar o próximo valor atômico da sequence e montar o candidato;
2. tentar persistir;
3. repetir **somente** se o banco reportar `23505` para `UX_Animais_CodigoInterno_CaseInsensitive` daquele candidato automático;
4. restaurar o estado de tracking antes da nova tentativa, ou executar a tentativa em uma unidade de persistência limpa;
5. propagar qualquer outro erro — inclusive FK, timeout, erro de conexão, outra constraint ou duplicidade em código manual.

Isso não é retry cego: cada nova tentativa é sustentada por uma sequence atômica e por uma constraint estrutural que provou que o candidato já existe. Criação ou edição com código informado manualmente nunca troca o valor do usuário: colisão é `AnimalDuplicateException` e `409`.

### Alternativas avaliadas

| Alternativa | Vantagem | Desvantagem | Decisão |
|---|---|---|---|
| Sequence PostgreSQL + índice único + repetição dirigida | Atômica, global, simples, sem hot row e coerente com PostgreSQL. | Aceita lacunas e eventual colisão com código manual autoformatado. | **Adotada.** |
| Tabela-contador com `UPDATE … RETURNING` em transação | Pode manter sequência sem lacunas se todos os fluxos forem serializados. | Hot row global, menor concorrência, coordenação obrigatória também para códigos manuais e nova convenção sem necessidade do MVP. | Não adotar. |
| `MAX(CodigoInterno)+1`, contador em memória ou lock de processo | Parece simples. | Não é seguro entre sessões/processos e falha em concorrência. | Proibida. |
| `DEFAULT nextval` diretamente na coluna | Aloca no banco. | Não resolve formato textual, input manual nem a colisão latente com um código manual. | Não adotar. |

## 6. Relacionamento Espécie, Raça e Variedade

O modelo é direto e paralelo:

```text
Especie 1 ──< Raca
Especie 1 ──< Variedade

Animal ──> Especie      (obrigatório)
Animal ──> Raca?        (opcional, mesma espécie)
Animal ──> Variedade?   (opcional, mesma espécie)
```

Não há relação `Raca → Variedade`, hierarquia implícita, `ClassificacaoAnimal` genérica nem exclusividade entre as duas classificações.

### Regras de criação e edição

| Situação | Comportamento |
|---|---|
| Criar Animal | `EspecieId` deve existir e estar ativo. `RacaId`/`VariedadeId`, se informados, devem existir, estar ativos e ter o mesmo `EspecieId`. |
| Atualizar sem mudar `EspecieId` | Pode editar dados e manter a espécie, raça ou variedade atuais, mesmo se algum desses cadastros foi inativado depois do vínculo. |
| Trocar `EspecieId` | A nova espécie deve existir e estar ativa. O snapshot completo também precisa trazer Raça e Variedade nulas ou compatíveis com a nova espécie. |
| Trocar ou incluir `RacaId` / `VariedadeId` | A nova classificação precisa existir, estar ativa e pertencer à espécie do snapshot. |
| Limpar classificação | Permitido. `null` representa ausência de classificação. |
| Espécie atual tornou-se inativa | O vínculo histórico pode permanecer; o Animal pode continuar sendo editado e reativado. Não é permitido trocar para outra espécie inativa. |
| Raça/Variedade atual tornou-se inativa | O vínculo histórico pode permanecer. Uma classificação inativa não pode ser escolhida como novo vínculo. |

Quando a espécie atual está inativa, é permitido manter esse vínculo histórico e editar outros campos. Também é permitido substituir uma classificação por outra **ativa** e compatível com essa mesma espécie preservada; não se está criando uma nova relação com espécie inativa, apenas completando ou corrigindo a classificação de um vínculo histórico existente.

### Política para mudança de espécie

Foram consideradas duas opções:

- **A — limpeza automática:** mudar a espécie apaga automaticamente Raça e/ou Variedade incompatíveis.
- **B — rejeitar snapshot incompatível:** o servidor rejeita a alteração enquanto o request conservar qualquer classificação incompatível; a pessoa usuária decide explicitamente limpar ou trocar a classificação no mesmo `PUT`.

Adota-se **B**. Limpeza automática descartaria uma referência histórica sem intenção explícita. A UI deve avisar quando a pessoa selecionar outra espécie, conservar a informação em tela como incompatível e exigir que ela seja limpa ou substituída por opção compatível antes do envio. O `PUT` continua atômico: enviar a nova espécie junto de classificações nulas ou compatíveis é válido.

### Proteção contra alterar a espécie de uma classificação usada

A regra da issue #295 exige que `Raca.EspecieId` ou `Variedade.EspecieId` não possa mudar quando já existe Animal que a referencia. Ela será garantida em duas camadas na implementação posterior:

1. Application pré-consulta o uso por Animal quando houver tentativa de troca de espécie e devolve conflito específico;
2. o banco reforça a regra com FKs compostas restritas, descritas na seção de persistência, para evitar corrida entre pré-consulta e gravação.

Trocar nome, ativar ou inativar uma classificação usada continua permitido. A proteção não depende de Animal ou classificação estarem ativos.

## 7. Lifecycle

`Ativo` controla somente a participação operacional corrente.

- `POST` cria Animal ativo por padrão.
- `PATCH /api/v1/animais/{id}/ativo` inativa ou reativa de forma idempotente.
- Animal inativo continua retornando em consulta/listagem quando o filtro permitir, pode ser editado e pode ser reativado.
- Não existe `DELETE`, `StatusAnimal`, evento de morte, venda, descarte, propriedade, localização ou saída nesta etapa.

O frontend não deve bloquear preventivamente os seletores de Sexo ou Escopo pelo simples fato de o Animal já existir ou estar inativo. Regras futuras de filiação e cruzamentos serão adicionadas nos próprios domínios futuros.

## 8. Application

O módulo segue a organização vertical existente:

- `GenSW.Domain.Animals`: `Animal`, `SexoAnimal`, `EscopoAnimal` e regras locais;
- `GenSW.Application.Animals`: comandos, resultados, `AnimalListQuery`, enum de ordenação, exceções, `IAnimalService`, `IAnimalRepository` e a abstração de alocação automática;
- `GenSW.Infrastructure.Animals`: repositório EF/PostgreSQL, tradução de constraints e gerador apoiado pela sequence;
- serviços e repositórios de Espécies, Raças e Variedades continuam específicos; somente recebem as consultas adicionais necessárias à proteção de uso por Animal.

`IAnimalService` expõe conceitualmente `CreateAsync`, `GetByIdAsync`, `ListAsync`, `UpdateAsync` e `SetActiveAsync`. `CreateAsync` distingue código nulo/omitido de código informado. `UpdateAsync` recebe o snapshot completo e exige `CodigoInterno` explícito.

Antes de criar ou alterar, o serviço carrega o Animal rastreado quando aplicável, compara IDs antigos e novos e valida o estado do destino apenas quando o vínculo é novo ou mudou. Assim, preserva vínculos históricos inativos sem permitir um novo destino inativo. As verificações de espécie de Raça e Variedade são separadas; não há validação cruzada entre elas.

Resultados de leitura projetam resumos de classificação, evitando N+1 no frontend:

- espécie: `Id`, `NomeComum`, `Ativo`;
- raça/variedade, quando presentes: `Id`, `Nome`, `Ativo`.

`AnimalListQuery` usa paginação offset como os cadastros existentes: `Page = 1`, `PageSize = 25`, máximo `100`, busca opcional, filtros opcionais e ordenação validada. A ordenação sempre aplica `Id` como desempate estável.

## 9. API

Todos os endpoints são autenticados com `[Authorize]`, no padrão dos controllers atuais.

| Método | Rota | Finalidade |
|---|---|---|
| `POST` | `/api/v1/animais` | Criar; retorna `201 Created` e `Location`. |
| `GET` | `/api/v1/animais` | Listar em envelope paginado. |
| `GET` | `/api/v1/animais/{id}` | Consultar inclusive Animal inativo. |
| `PUT` | `/api/v1/animais/{id}` | Atualizar snapshot completo e classificações. |
| `PATCH` | `/api/v1/animais/{id}/ativo` | Inativar ou reativar. |

Não haverá rota `DELETE`.

Os requests de criação e edição expõem `codigoInterno`, `nome`, `especieId`, `racaId`, `variedadeId`, `sexo`, `dataNascimento` e `escopo`. Em criação, `codigoInterno` é anulável/omitível para geração; em edição é obrigatório. Os enums permanecem numéricos no JSON, preservando a convenção atual: Sexo `1|2|3` e Escopo `1|2`.

Uma resposta de Animal contém os campos persistidos, timestamps e resumos de espécie/raça/variedade. As classificações opcionais são `null` quando ausentes.

`GET /api/v1/animais` aceita:

| Parâmetro | Regra |
|---|---|
| `page`, `pageSize` | Padrões `1` e `25`; tamanho entre `1` e `100`. |
| `search` | Busca case-insensitive por `CodigoInterno` e `Nome`; entrada vazia equivale a ausência. |
| `especieId`, `racaId`, `variedadeId` | Filtros de classificação opcionais. |
| `sexo`, `escopo`, `ativo` | Filtros opcionais pelos valores de enum/status. |
| `sortBy` | `codigoInterno` (padrão), `nome`, `sexo`, `escopo`, `ativo` ou `createdAtUtc`. |
| `sortDirection` | `asc` (padrão) ou `desc`. |

Busca é suficiente para o MVP; não haverá endpoint separado, índice trigram ou filtro redundante de texto até haver evidência de necessidade.

## 10. Persistência

A migration futura será nova e aditiva; não altera migrations existentes. Ela cria `Animais`, a sequence de código e os constraints/índices abaixo. Para reversibilidade, `Down` remove `Animais` e suas FKs compostas, remove as chaves alternativas adicionadas a `Racas` e `Variedades` e só então remove a sequence; assim, o schema volta exatamente ao estado anterior à NA-03. Nenhuma migration é criada nesta fase de design.

### Tabela `Animais`

| Coluna | Persistência |
|---|---|
| `Id` | `uuid`, PK. |
| `CodigoInterno` | `varchar(64)`, obrigatório. |
| `Nome` | `varchar(200)`, anulável. |
| `EspecieId` | `uuid`, obrigatório. |
| `RacaId`, `VariedadeId` | `uuid`, anuláveis. |
| `Sexo`, `Escopo` | inteiro obrigatório, com conversão de enum. |
| `DataNascimento` | `date`, anulável. |
| `Ativo` | `boolean`, obrigatório, default `true`. |
| `CreatedAtUtc`, `UpdatedAtUtc` | `timestamp with time zone`, obrigatórios. |

Constraints:

- `CK_Animais_CodigoInterno_Canonical`: não vazio e no mesmo formato canônico de espaços usado pelos nomes existentes;
- `CK_Animais_Nome_Canonical`: nulo ou canônico, sem vazio persistido;
- `CK_Animais_Sexo`: somente `1`, `2` ou `3`;
- `CK_Animais_Escopo`: somente `1` ou `2`;
- `UX_Animais_CodigoInterno_CaseInsensitive`: índice PostgreSQL único em `lower("CodigoInterno")`.

Índices adicionais mínimos são os de suporte às FKs e filtros de classificação: `EspecieId`, `(RacaId, EspecieId)` e `(VariedadeId, EspecieId)`. Não há índice de busca textual genérico nesta etapa.

### FKs e consistência estrutural

Além da FK obrigatória `Animais.EspecieId → Especies.Id` com `Restrict`, serão declaradas chaves alternativas únicas em `Racas(Id, EspecieId)` e `Variedades(Id, EspecieId)`. `Animais` usará as seguintes FKs compostas, também `Restrict`:

- `(RacaId, EspecieId) → Racas(Id, EspecieId)`;
- `(VariedadeId, EspecieId) → Variedades(Id, EspecieId)`.

Quando `RacaId` ou `VariedadeId` é `null`, sua FK composta é opcional; quando há ID, o banco exige a dupla correspondente. Isso dá garantia real para a espécie comum mesmo diante de concorrência. O `Restrict` configurado trata a exclusão; a ação padrão PostgreSQL para atualização da chave referenciada (`NO ACTION`) impede mudar `Raca.EspecieId` ou `Variedade.EspecieId` enquanto uma linha de Animal ainda depende da dupla antiga. EF Core suporta FKs compostas contra chaves alternativas; qualquer parte nula torna a FK composta opcional, comportamento útil neste modelo ([documentação EF Core](https://learn.microsoft.com/en-us/ef/core/modeling/relationships/one-to-many)).

O estado ativo das classificações não cabe em FK e continua sendo validado pela Application. O banco permanece a defesa contra inexistência, espécie divergente e mudança concorrente de classificação.

## 11. Frontend

Será criado o módulo vertical `features/animals`, sem componente genérico prematuro:

- tipos, parser de contrato e serviço HTTP próprios;
- `AnimalsListPage`, `AnimalFormPage` e seus testes;
- rotas protegidas `/animais`, `/animais/nova` e `/animais/:id/editar`;
- link de navegação na home autenticada.

### Listagem

A tela **Animais** oferece busca por Código Interno ou Nome, filtros por Espécie, Raça, Variedade, Sexo, Escopo e Ativo, ordenação, tamanhos 25/50/100, paginação, loading, estado vazio, retry e erro não destrutivo de lifecycle. Os catálogos usados como filtros incluem registros inativos para localizar histórico.

### Novo e Editar

O formulário mostra `CodigoInterno` opcional apenas na criação, com a informação clara de que ele será gerado se permanecer vazio. Na edição, o código atual é carregado e é editável.

Espécies para novo vínculo são carregadas somente ativas. Depois da seleção de espécie, Raças e Variedades são carregadas separadamente, filtradas por espécie e ativas; ambas podem ser escolhidas simultaneamente. O formulário pagina as respostas de seleção quando necessário, em vez de silenciar itens após a primeira página de 100.

Na edição, uma espécie, raça ou variedade inativa que já compõe o Animal é acrescentada à lista somente para preservar e exibir o vínculo atual. Ela não aparece como opção normal de novo vínculo. Se a pessoa trocar a espécie, o formulário destaca as classificações incompatíveis e exige limpeza ou substituição explícita antes de enviar; não apaga valores silenciosamente.

Sexo e Escopo são selects sempre editáveis. `DataNascimento` usa campo de data sem exibir ou persistir idade. As mensagens para `400`, `404` e `409` são específicas e não expõem detalhes internos de PostgreSQL.

## 12. Tratamento de erros e cenários de concorrência

O padrão HTTP acompanha os módulos atuais: dados ou query inválidos em `400`, ausência em `404`, conflitos em `409`, acesso não autenticado em `401`, e `ProblemDetails` nos erros de mutação.

| Cenário | Fonte de verdade | Resultado Application/API |
|---|---|---|
| Dois `POST` automáticos simultâneos | `nextval` retorna valores distintos; índice único é defesa final. | Ambos criam Animal com códigos distintos (`201`), ainda que a ordem de commit seja diferente. |
| `POST` automático concorre com manual igual ao próximo candidato | `UX_Animais_CodigoInterno_CaseInsensitive`. | Se o manual persistir primeiro, o automático reconhece somente essa violação conhecida, aloca o próximo e ambos podem retornar `201`. Se o automático persistir primeiro, o manual recebe `AnimalDuplicateException` e `409`; o valor manual nunca é alterado. |
| Dois `POST` manuais com o mesmo código | Índice único case-insensitive. | Um persiste; o outro recebe `AnimalDuplicateException`/`409`, mesmo que a pré-validação tenha passado em ambos. |
| Edição para código já usado | Mesmo índice único. | `AnimalDuplicateException`/`409`; o código anterior permanece. |
| Espécie, Raça ou Variedade ausente | Consulta de Application e FK. | Exceção `*NotFound`/`404`. |
| Destino de vínculo inativo ou classificação de espécie diversa | Regra de Application; FK composta reforça a espécie. | `ArgumentException` ou exceção de regra de vínculo/`400`. |
| Mudança de espécie com classificação conservada incompatível | Política B no snapshot completo; FK composta é defesa final. | `400` com campo conflitante e orientação de limpar/substituir. |
| Tentar mudar `Raca.EspecieId` ou `Variedade.EspecieId` após uso por Animal | Pré-consulta e FK composta `Restrict`. | Exceção específica de classificação em uso/`409`; corrida de banco recebe a mesma tradução. |
| Esgotamento da sequence | Sequence PostgreSQL. | Exceção de exaustão de código/`409`, sem reciclagem ou retry genérico. |

## 13. Estratégia de testes

| Camada | Cobertura requerida |
|---|---|
| Domain | Criação mínima válida; enums inválidos; normalização e limites de Código/Nome; campos opcionais; data futura; edição de código, nome, sexo, escopo e classificações; lifecycle idempotente; edição de Animal inativo; ausência de idade persistida. |
| Application | Create manual/automático; enum e query inválidos; espécie ausente/inativa; todas as combinações de Raça/Variedade; cada classificação em espécie divergente; manutenção de vínculos inativos; novo vínculo inativo rejeitado; troca de espécie segundo política B; código duplicado; atualização de código; lifecycle; proteção de alteração de espécie de Raça/Variedade em uso. |
| Infrastructure/PostgreSQL | Modelo EF e migration; defaults, checks, índices, FKs `Restrict`, chaves alternativas e FKs compostas; round-trip `Up`/`Down` que remove também as chaves alternativas; unicidade case-insensitive; paginação/ordenação/filtros; dois contextos reais criando automaticamente em concorrência; colisão manual `AN-000001`; rollback consumindo número e lacuna aceita; tentativa SQL de classificação de espécie incompatível; tentativa de mover classificação referenciada. |
| API | Autenticação; `201`/Location; listagem, paginação e todos os filtros; GET ativo/inativo; PUT; PATCH lifecycle; `400`, `404`, `409`; cenários de concorrência acima quando o harness permitir; inexistência de DELETE (`405`). |
| Frontend | Parsers estritos, inclusive enums e resumos anuláveis; serialização de todos os filtros; rotas protegidas; loading/erro/retry/vazio/paginação; criação com código vazio; edição de código; seletores de espécie/raça/variedade; ambas as classificações simultâneas; vínculo histórico inativo; aviso de troca de espécie; mensagens de erro e lifecycle. |

Os gates de implementação posteriores continuam sendo build e testes backend, testes/lint/build frontend, verificação da migration em PostgreSQL, `git diff --check`, inspeção de escopo/segredos e homologação manual após PR/CI. Eles não são executados como se esta SPEC fosse implementação.

As suítes existentes de Espécies, Raças e Variedades também permanecem obrigatórias como regressão, sobretudo para lifecycle, vínculo com espécie inativa, paginação, endpoints sem DELETE e a nova proteção de mudança de espécie de classificação usada.

## 14. Fora do escopo

Esta demanda não cria Identificação Física (#296), Registro Institucional (#297), Filiação/Pedigree (#298) ou Cruzamentos (#299). Também não inclui propriedade, proprietário, localização, lote, peso, medidas, produção, postura, fertilidade, eclosão, mortalidade, fenótipo, genótipo, genes, alelos, mutações, linhagem, consanguinidade, ranking de reprodutores, eventos de saída ou `StatusAnimal` complexo.

Não serão incluídas tabelas EAV, campos antecipados, pai/mãe em `Animal`, nem componentes frontend genéricos sem uso real.

## 15. Extensibilidade futura

O `Guid` imutável de `Animal` é o ponto de referência para módulos futuros. `CodigoInterno` pode mudar e, portanto, não será usado como FK nem como identidade externa duradoura.

Extensões futuras podem associar suas próprias tabelas a `Animal.Id` sem alterar o agregado base:

- `IdentificacaoAnimal` para identificações físicas;
- `RegistroAnimal` para registros institucionais;
- `FiliacaoAnimal` para relações históricas de progenitor/descendente e projeção de pedigree;
- `Cruzamento` para planejamento ou eventos reprodutivos;
- tabelas próprias para medições, produção, fenótipo e genótipo.

Cada domínio futuro acrescentará suas regras de lifecycle, integridade e permissão de edição. Por exemplo, somente Filiação/Cruzamento poderão limitar mudança de Sexo quando houver uma relação histórica incompatível. Nada dessas estruturas é inferido ou materializado agora.

## 16. Registro de decisões e alternativas arquiteturais

| Decisão | Resultado |
|---|---|
| Identidade operacional | `CodigoInterno` é único global, editável e semanticamente neutro; `Id` é a identidade técnica imutável. |
| Geração automática | Sequence PostgreSQL global, `bigint`, `CACHE 1`, formatação mínima de seis dígitos, lacunas aceitas. |
| Colisão manual/automática | Índice único global e repetição somente para colisão comprovada de candidato automático. |
| Integridade de classificações | FKs compostas contra chaves alternativas, além de validação de estado na Application. |
| Raça e Variedade | Independentes, diretamente ligadas à Espécie e simultaneamente opcionais no Animal. |
| Troca de espécie | Política B: rejeitar snapshot incompatível; limpeza/substituição deve ser explícita. |
| Inatividade | Histórico preservado; novos destinos inativos são proibidos; Animal inativo continua editável e reativável. |
| API/UI | Reutilizar convenções existentes de autenticação, paginação offset, `ProblemDetails`, rotas e módulos verticais. |

Não foi encontrada contradição entre esta SPEC, a issue #295 e os contratos integrados de NA-01/NA-02. A ausência atual da proteção contra troca de espécie de Raça/Variedade é esperada: ela só passa a ser necessária quando Animal existir e fica explicitamente incluída na implementação de #295.

## Critérios de aceite desta fase de design

- A SPEC cobre os campos, invariantes, lifecycle, API, persistência, frontend, erros, concorrência e testes de NA-03.
- A estratégia de `CodigoInterno` é global, atômica, concorrente, sem `MAX+1` ou contador em memória, e define manual/rollback/overflow.
- A política de troca de espécie e as classificações inativas são explícitas.
- A integridade Espécie/Raça/Variedade tem defesa de Application e banco.
- Não há código, migration, branch de implementação, plano de implementação ou alteração das issues #296–#299.
