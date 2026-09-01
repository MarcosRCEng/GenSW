# NA-02 — Raças e Variedades: desenho

## Contexto e escopo

A NA-02 entrega os cadastros mestres de `Raca` e `Variedade`, ambos relacionados diretamente a `Especie`, já integrada pela NA-01. A entrega cobre Domain, Application, persistência PostgreSQL/EF Core, API autenticada, frontend e testes.

Ficam fora de escopo Animal e as demandas #295–#299. Não haverá uma entidade ou serviço genérico de classificação, relação `Raca -> Variedade`, DELETE físico, biblioteca frontend nova ou alteração de migrations existentes.

## Decisões de domínio

`Raca` e `Variedade` são entidades independentes e paralelas, cada uma com `Id`, `EspecieId`, `Nome`, `Ativo`, `CreatedAtUtc` e `UpdatedAtUtc`.

O nome é obrigatório, tem no máximo 200 caracteres e é normalizado com a mesma regra de espaços de `Especie`. Cada domínio permite o mesmo nome em espécies diferentes e o rejeita, após normalização, na mesma espécie.

`Ativo` inicia como `true`; inativar, reativar e editar registros inativos são operações idempotentes ou permitidas conforme o padrão de Espécies. Enquanto não existe Animal, uma alteração de `EspecieId` é permitida se a espécie de destino existir.

A API aceitará uma espécie inativa existente. A restrição operacional de não oferecer espécies inativas aplica-se à escolha padrão no frontend, preservando integridade histórica e possibilitando correções administrativas.

## Backend

Cada domínio terá comandos, consultas, resultados, exceções, interface de serviço, interface de repositório, serviço e repositório concretos próprios. Os serviços reutilizarão somente `IEspecieRepository` para verificar a existência de `EspecieId`; não será criado repositório genérico nem camada compartilhada artificial.

Os repositórios retornarão projeções explícitas para leitura, incluindo o resumo da espécie (`id`, nome comum e status), sem transformar Raca e Variedade em um domínio único. A API exporá `/api/v1/racas` e `/api/v1/variedades`, ambas autenticadas, com CREATE, LIST, GET, UPDATE e PATCH de ativo. As listagens aceitarão busca, `especieId`, `ativo`, paginação e ordenação no envelope já adotado.

Ausência do registro ou da espécie resulta em `404`; dado ou consulta inválida em `400`; conflito normalizado em `409`. Não haverá rota DELETE.

## Persistência e concorrência

Uma migration nova criará `Racas` e `Variedades` com chave primária `Id` e FK obrigatória `EspecieId -> Especies(Id)` com `DeleteBehavior.Restrict`; `Nome` obrigatório de até 200 caracteres, default `true` em `Ativo` e timestamps obrigatórios; check constraints que rejeitam nome vazio ou não canônico; e índices únicos funcionais em `(EspecieId, lower(Nome))` para cada tabela.

Os repositórios converterão a violação de cada índice em sua exceção de duplicidade específica, para que concorrência não permita um duplicado entre a verificação de aplicação e o `SaveChanges`.

## Frontend

Haverá módulos separados de Raças e Variedades, cada um com tipos, parser de contrato, serviço HTTP, listagem e formulário. Ambos reutilizam apenas o cliente HTTP e o serviço existente de Espécies. Rotas protegidas e links na home exporão os dois cadastros.

As listagens permitem busca, filtro por espécie e por status, ordenação, paginação e mudança de ativo. Formulários validam localmente o nome e usam a lista de espécies ativas para novos cadastros. Ao editar um registro ligado a uma espécie inativa, o vínculo atual continua mostrado e pode ser mantido; espécies ativas permanecem disponíveis para troca.

## Verificação

Os testes cobrirão, para cada domínio, criação, normalização, espécie inexistente, duplicidade por espécie, mesmo nome entre espécies diferentes, listagem, GET, troca de espécie, edição inativa, inativação e reativação. Os testes PostgreSQL confirmarão FK, constraints e índices únicos. A API cobrirá fluxo autenticado e ausência de DELETE. O frontend cobrirá contratos, HTTP, filtros, seleção de espécie, formulários, erros e lifecycle.

Antes da PR serão executados restore, build e testes do backend; testes, lint e build do frontend; `git diff --check`; e uma inspeção de escopo, migration, dependências e segredos. A homologação manual só será solicitada após PR e CI verde e incluirá Raças, Variedades e o comportamento de espécie inativa.
