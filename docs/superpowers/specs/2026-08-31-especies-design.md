# NA-01 — Espécies Design

**Status:** aprovado em conversa
**Redmine:** #293, filha da Evolução #291
**Branch:** `feature/293-especies`

## Objetivo

Entregar verticalmente o cadastro autenticado de espécies animais, cobrindo domínio, Application, repositório específico, EF Core/PostgreSQL, migration, API, cliente HTTP e interface React com testes backend e frontend.

## Escopo

A entrega permite criar, listar, consultar por `Id`, editar, inativar e reativar espécies. Uma espécie inativa permanece consultável, listável, editável e reativável. Não existe exclusão física nem endpoint `DELETE`.

Não fazem parte desta entrega taxonomia adicional, `Genero`, `Familia`, `Ordem`, `Subespecie`, `Sinonimos`, `Raca`, `Variedade`, `Animal`, propriedade, produção, genética ou qualquer implementação das issues #294–#299.

## Arquitetura

O módulo segue a estrutura vertical já consolidada por `Pessoa`, reutilizando apenas os padrões aplicáveis:

- `GenSW.Domain.Species`: entidade `Especie` e invariantes do modelo;
- `GenSW.Application.Species`: serviço, comandos, consultas, resultados, exceções e `IEspecieRepository`;
- `GenSW.Infrastructure.Species`: implementação EF Core do repositório e tradução de conflitos de unicidade;
- `GenSW.Infrastructure.Persistence`: mapeamento e migration de `Especies`;
- `GenSW.API.Contracts.Species` e `EspeciesController`: contrato HTTP autenticado;
- `features/species`: tipos, parser de contrato, cliente HTTP, listagem e formulário React;
- rotas protegidas e link de navegação no frontend.

Não serão introduzidos MediatR, CQRS, repositório genérico, Redux, Zustand, React Query, Axios, Formik ou React Hook Form.

## Modelo de domínio

`Especie` possui:

```text
Id: Guid
NomeComum: string
NomeCientifico: string?
Ativo: bool
CreatedAtUtc: DateTimeOffset
UpdatedAtUtc: DateTimeOffset
```

`NomeComum` é obrigatório e possui de 1 a 200 caracteres após o tratamento de espaços. `NomeCientifico` é opcional, transforma entrada vazia em `null` e possui no máximo 200 caracteres quando informado.

Na criação e edição, os dois nomes passam pelo mesmo tratamento:

1. remover espaços nas extremidades;
2. substituir qualquer sequência interna de caracteres de espaço por um único espaço comum;
3. preservar a caixa informada para exibição;
4. preservar acentos.

A comparação de unicidade ignora maiúsculas e minúsculas, mas não remove acentos. Portanto, `Homo sapiens` e `HOMO   SAPIENS` são duplicados após o tratamento de espaços, enquanto `Cão` e `Cao` permanecem distintos.

`Ativo` inicia como `true`. `AlterarCadastro` funciona independentemente do estado ativo. `Inativar` e `Reativar` são idempotentes. Alterações efetivas atualizam `UpdatedAtUtc`; operações sem mudança preservam o timestamp.

## Persistência e unicidade

A tabela `Especies` armazena somente os campos conceituais e timestamps. Não existem colunas auxiliares com sufixo `Normalizado`.

O mapeamento EF Core define:

- chave primária em `Id`;
- `NomeComum` obrigatório com limite de 200;
- `NomeCientifico` anulável com limite de 200;
- `Ativo` obrigatório e default SQL `true`;
- timestamps obrigatórios;
- constraints que rejeitam nome comum vazio e valores fora da forma canônica de espaçamento;
- constraint equivalente para nome científico quando informado.

A migration cria índices PostgreSQL explícitos:

```sql
CREATE UNIQUE INDEX "UX_Especies_NomeComum_CaseInsensitive"
ON "Especies" (lower("NomeComum"));

CREATE UNIQUE INDEX "UX_Especies_NomeCientifico_CaseInsensitive"
ON "Especies" (lower("NomeCientifico"))
WHERE "NomeCientifico" IS NOT NULL;
```

Os índices preservam acentos e garantem a unicidade em gravações concorrentes. A pré-validação do repositório usa a mesma função PostgreSQL `lower` e exclui o próprio `Id` durante edição. Violações concorrentes desses índices são traduzidas para a mesma exceção de conflito produzida pela pré-validação.

A migration é aditiva, possui `Down` simétrico e não modifica migrations existentes.

## Application e repositório

`EspecieService` oferece:

- `CreateAsync`;
- `GetByIdAsync`;
- `ListAsync`;
- `UpdateAsync`;
- `SetActiveAsync`.

Antes de criar ou editar, o serviço consulta conflitos de `NomeComum` e `NomeCientifico`. `NomeCientifico = null` não participa da verificação de unicidade. O repositório oferece operações específicas de leitura sem tracking, leitura para atualização, listagem paginada, detecção de conflitos, adição e persistência.

A listagem suporta busca nos dois nomes, filtro opcional por `Ativo`, paginação e ordenação por `nomeComum`, `nomeCientifico`, `ativo` ou `createdAtUtc`. Sem filtro de status, espécies ativas e inativas são retornadas.

## API

Todos os endpoints exigem autenticação:

```text
POST  /api/v1/especies
GET   /api/v1/especies
GET   /api/v1/especies/{id}
PUT   /api/v1/especies/{id}
PATCH /api/v1/especies/{id}/ativo
```

`GET /api/v1/especies` aceita `page`, `pageSize`, `search`, `ativo`, `sortBy` e `sortDirection`, seguindo limites e formato paginado de `Pessoa` quando aplicáveis.

Respostas usam `400 Bad Request` para dados ou consultas inválidas, `404 Not Found` para `Id` inexistente, `409 Conflict` para duplicidade normalizada e `401 Unauthorized` para acesso sem autenticação. Não existe endpoint `DELETE`.

## Frontend

O frontend adiciona rotas protegidas:

```text
/especies
/especies/nova
/especies/:id/editar
```

A home autenticada recebe o link `Espécies`. A listagem oferece busca pelos nomes, filtro de status, ordenação, paginação, loading inicial, atualização, estado vazio, recuperação de erro e ações de edição, inativação e reativação. A edição permanece habilitada para registros inativos.

O formulário compartilhado cobre criação e edição, apresenta `NomeComum` obrigatório e `NomeCientifico` opcional, aplica os limites do domínio e mostra erros relevantes sem expor detalhes internos. Após uma mutação de status, a listagem mantém filtros e página quando o resultado continuar válido.

O cliente usa `httpRequest` e parsers runtime já existentes. Nenhuma biblioteca adicional de formulário, HTTP ou estado será instalada.

## Testes

O desenvolvimento segue ciclos RED–GREEN–REFACTOR. A cobertura inclui:

- domínio: criação válida, nome comum vazio, tratamento de espaços, científico nulo, edição de inativa e lifecycle;
- Application: create, get, list, update, inativar, reativar, pré-validação dos dois conflitos e exclusão do próprio `Id` na edição;
- infraestrutura: modelo EF, defaults, constraints, índices reais no PostgreSQL, filtros, ordenação, paginação, duplicidades normalizadas e conflito concorrente;
- API: autenticação, contratos, códigos `400`, `404` e `409`, create/list/get/update/inativar/consultar inativa/editar inativa/reativar e ausência de rota `DELETE`;
- frontend: parser, cliente HTTP, listagem, filtros, estados, cadastro, edição de inativa, ativação/inativação, conflitos e rotas protegidas.

## Validação e entrega

Antes do commit final serão executados restore, build e testes do backend; testes, lint e build do frontend; validação da migration em PostgreSQL; `git diff --check`; inspeção de escopo e de migrations antigas.

Somente após todos os gates locais aprovados serão feitos commit de implementação, push da branch e PR para `main`. Os checks Backend e Frontend serão acompanhados. A issue #293 será atualizada para `Em validação`, mantendo homologação manual, revisão e merge como pendências humanas. Nenhuma issue #294–#299 terá status alterado.
