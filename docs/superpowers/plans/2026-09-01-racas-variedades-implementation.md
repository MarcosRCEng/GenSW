# NA-02 — Raças e Variedades Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Entregar cadastros independentes e autenticados de `Raca` e `Variedade`, ligados diretamente a `Especie`, com persistência PostgreSQL, API, frontend e testes completos.

**Architecture:** Cada domínio terá entidade, comandos, consultas, resultados, serviço e repositório próprios. Os dois serviços consultarão o `IEspecieRepository` existente para validar a espécie de destino; os repositórios próprios retornam modelos de leitura que incluem o resumo da espécie. A duplicidade é verificada antes da gravação e novamente protegida por índices funcionais PostgreSQL, cuja violação é convertida na exceção específica de cada domínio.

**Tech Stack:** .NET 8, C#, EF Core 8/Npgsql/PostgreSQL, ASP.NET Core controllers, xUnit, React 18, TypeScript, React Router, Vitest e Testing Library.

**Spec:** `docs/superpowers/specs/2026-09-01-racas-variedades-design.md`

## Global Constraints

- A baseline é `main` em `0acd8ee`, que já contém a integração da NA-01 (`#293`); a implementação ocorre na branch `feature/294-racas-variedades`.
- `Raca` e `Variedade` são domínios paralelos: não criar `ClassificacaoAnimal`, `IRepository<T>`, serviço genérico ou relação `Raca -> Variedade`.
- Não criar DELETE físico, não alterar migrations existentes e não antecipar Animal ou as demandas `#295`–`#299`.
- Cada `Nome` é obrigatório, normalizado por trim e colapso de espaços, limitado a 200 caracteres e único por `(EspecieId, lower(Nome))`; o mesmo nome em espécies diferentes é válido.
- `Ativo` inicia `true`; edição, inativação e reativação seguem o padrão idempotente de `Especie`.
- CREATE com espécie inativa é inválido. No UPDATE, somente uma troca de `EspecieId` exige que a espécie de destino esteja ativa; manter a espécie já vinculada e posteriormente inativada, editar `Nome`/`Ativo` nesse estado e trocar dela para uma espécie ativa são válidos.
- Toda API é autenticada. Retornar `400` para dado ou consulta inválida, `404` para registro ou espécie inexistente e `409` para duplicidade normalizada. Não expor DELETE.
- O frontend usa somente espécies ativas em novos vínculos. Na edição, mantém visível a espécie atual inativa, permite conservá-la ou trocar por uma ativa e não oferece outra espécie inativa.

## File Map

| Área | Arquivos novos ou modificados |
| --- | --- |
| Domain | `src/Backend/GenSW.Domain/Breeds/Raca.cs`, `src/Backend/GenSW.Domain/Varieties/Variedade.cs`, respectivos testes em `tests/GenSW.Domain.Tests/` |
| Application | Pastas próprias `Breeds/` e `Varieties/` em `src/Backend/GenSW.Application/`; `DependencyInjection.cs`; testes de serviço e DI em `tests/GenSW.Application.Tests/` |
| Infrastructure | Repositórios em `src/Backend/GenSW.Infrastructure/Breeds/` e `Varieties/`; `Persistence/GenSWDbContext.cs`, `DependencyInjection.cs`, migration nova e snapshot; testes em `tests/GenSW.Infrastructure.Tests/` |
| API | Controllers `RacasController.cs` e `VariedadesController.cs`; contratos em `Contracts/Breeds/` e `Contracts/Varieties/`; testes em `tests/GenSW.API.Tests/` |
| Frontend | Features `breeds/` e `varieties/` sob `src/Frontend/GenSW.Web/src/features/`; `routes/AppRoutes.tsx`, `AuthenticatedHomePage.tsx` e seus testes |

---

### Task 1: Modelo de domínio de Raça

**Files:**
- Create: `src/Backend/GenSW.Domain/Breeds/Raca.cs`
- Create: `tests/GenSW.Domain.Tests/RacaTests.cs`

**Interfaces:**
- Produces: `Raca.Criar(Guid especieId, string nome, DateTimeOffset nowUtc)`, `AlterarCadastro(Guid especieId, string nome, DateTimeOffset nowUtc)`, `Inativar(DateTimeOffset nowUtc)` e `Reativar(DateTimeOffset nowUtc)`.
- Consumes: apenas `Guid`, `DateTimeOffset` e a regra de normalização já materializada em `Especie`; nenhuma dependência de Application ou EF Core.

- [ ] **Step 1: Escrever os testes de domínio que falham**

  Cobrir `Criar` com `EspecieId`, nome normalizado e `Ativo == true`; nome `null`, vazio, somente espaços e com 201 caracteres; nome com exatamente 200 caracteres; `AlterarCadastro` sem mudança lógica sem mudar `UpdatedAtUtc`; edição de registro inativo; e `Inativar`/`Reativar` idempotentes.

  ```csharp
  [Fact]
  public void Criar_normaliza_nome_e_inicia_ativo()
  {
      var raca = Raca.Criar(especieId, "  Cão\t de   trabalho ", now);
      Assert.Equal("Cão de trabalho", raca.Nome);
      Assert.True(raca.Ativo);
  }
  ```

- [ ] **Step 2: Executar o teste vermelho**

  Run: `dotnet test tests/GenSW.Domain.Tests/GenSW.Domain.Tests.csproj --filter FullyQualifiedName~RacaTests`

  Expected: FAIL porque `Raca` ainda não existe.

- [ ] **Step 3: Implementar a entidade mínima**

  Criar uma classe `sealed` com `Id`, `EspecieId`, `Nome`, `Ativo`, `CreatedAtUtc` e `UpdatedAtUtc`, todos com setters privados. Aplicar a normalização canônica de espaços e o limite de 200 caracteres de `Especie`; não criar utilitário compartilhado apenas para esta duplicação. `AlterarCadastro` deve aceitar a mesma espécie e apenas atualizar timestamp quando houver mudança lógica.

- [ ] **Step 4: Executar os testes de domínio**

  Run: `dotnet test tests/GenSW.Domain.Tests/GenSW.Domain.Tests.csproj --filter FullyQualifiedName~RacaTests`

  Expected: PASS.

- [ ] **Step 5: Commit de marco**

  ```bash
  git add src/Backend/GenSW.Domain/Breeds/Raca.cs tests/GenSW.Domain.Tests/RacaTests.cs
  git commit -m "feat: add breed domain model"
  ```

### Task 2: Modelo de domínio de Variedade

**Files:**
- Create: `src/Backend/GenSW.Domain/Varieties/Variedade.cs`
- Create: `tests/GenSW.Domain.Tests/VariedadeTests.cs`

**Interfaces:**
- Produces: `Variedade.Criar(Guid especieId, string nome, DateTimeOffset nowUtc)`, `AlterarCadastro(Guid especieId, string nome, DateTimeOffset nowUtc)`, `Inativar(DateTimeOffset nowUtc)` e `Reativar(DateTimeOffset nowUtc)`.
- Consumes: somente `Guid` e `DateTimeOffset`, sem referência a `Raca`.

- [ ] **Step 1: Escrever os testes de domínio que falham**

  Cobrir `Criar` com `EspecieId`, normalização e `Ativo == true`; rejeição de nome `null`, vazio, somente espaços e com 201 caracteres; aceitação com 200; preservação de timestamp sem mudança lógica; edição enquanto inativa; e inativação/reativação idempotentes. Incluir uma asserção de compilação que a entidade não depende de `GenSW.Domain.Breeds`.

- [ ] **Step 2: Executar o teste vermelho**

  Run: `dotnet test tests/GenSW.Domain.Tests/GenSW.Domain.Tests.csproj --filter FullyQualifiedName~VariedadeTests`

  Expected: FAIL porque `Variedade` ainda não existe.

- [ ] **Step 3: Implementar a entidade mínima**

  Implementar a entidade paralela, com os mesmos seis campos e comportamento de `Raca`, mantendo classe, namespace e métodos próprios. Não adicionar navegação para `Raca`.

- [ ] **Step 4: Executar os testes de domínio**

  Run: `dotnet test tests/GenSW.Domain.Tests/GenSW.Domain.Tests.csproj --filter "FullyQualifiedName~RacaTests|FullyQualifiedName~VariedadeTests"`

  Expected: PASS.

- [ ] **Step 5: Commit de marco**

  ```bash
  git add src/Backend/GenSW.Domain/Varieties/Variedade.cs tests/GenSW.Domain.Tests/VariedadeTests.cs
  git commit -m "feat: add variety domain model"
  ```

### Task 3: Contratos e serviço de aplicação de Raça

**Files:**
- Create: `src/Backend/GenSW.Application/Breeds/CreateRacaCommand.cs`
- Create: `src/Backend/GenSW.Application/Breeds/UpdateRacaCommand.cs`
- Create: `src/Backend/GenSW.Application/Breeds/RacaResult.cs`
- Create: `src/Backend/GenSW.Application/Breeds/PagedRacaResult.cs`
- Create: `src/Backend/GenSW.Application/Breeds/RacaListQuery.cs`
- Create: `src/Backend/GenSW.Application/Breeds/RacaSortField.cs`
- Create: `src/Backend/GenSW.Application/Breeds/IRacaService.cs`
- Create: `src/Backend/GenSW.Application/Breeds/IRacaRepository.cs`
- Create: `src/Backend/GenSW.Application/Breeds/RacaService.cs`
- Create: `src/Backend/GenSW.Application/Breeds/RacaNotFoundException.cs`
- Create: `src/Backend/GenSW.Application/Breeds/RacaDuplicateException.cs`
- Modify: `src/Backend/GenSW.Application/DependencyInjection.cs`
- Create: `tests/GenSW.Application.Tests/RacaServiceTests.cs`
- Modify: `tests/GenSW.Application.Tests/DependencyInjectionTests.cs`

**Interfaces:**
- Consumes: `Raca`, `IEspecieRepository.GetByIdReadOnlyAsync`, `EspecieNotFoundException` e `TimeProvider`.
- Produces: `IRacaService` com `CreateAsync`, `GetByIdAsync`, `ListAsync`, `UpdateAsync` e `SetActiveAsync`; `IRacaRepository` próprio; `RacaResult` com `EspecieId`, `Nome`, status, timestamps e resumo explícito da espécie (`Id`, `NomeComum`, `Ativo`).

- [ ] **Step 1: Escrever os testes de serviço que falham**

  Usar fakes separados de `IRacaRepository` e `IEspecieRepository`. Cobrir criação válida; espécie inexistente no CREATE (`EspecieNotFoundException`); espécie inativa no CREATE (`ArgumentException`, a ser convertido em `400`); duplicidade normalizada dentro da mesma espécie; mesmo nome em espécie diferente; paginação/filtros/ordenação inválida; GET ausente; edição de raça inativa; e lifecycle.

  Cobrir explicitamente as cinco transições aprovadas:

  ```csharp
  await Assert.ThrowsAsync<ArgumentException>(() =>
      service.CreateAsync(new CreateRacaCommand(inactiveSpecies.Id, "Raça")));
  await Assert.ThrowsAsync<ArgumentException>(() =>
      service.UpdateAsync(raca.Id, new UpdateRacaCommand(otherInactive.Id, "Raça")));
  Assert.False((await service.UpdateAsync(racaWithInactiveSpecies.Id,
      new UpdateRacaCommand(racaWithInactiveSpecies.EspecieId, "Novo nome"))).Especie.Ativo);
  ```

  O mesmo conjunto deve verificar alteração de `Ativo` enquanto a espécie vinculada está inativa e troca dela para uma espécie ativa.

- [ ] **Step 2: Executar o teste vermelho**

  Run: `dotnet test tests/GenSW.Application.Tests/GenSW.Application.Tests.csproj --filter FullyQualifiedName~RacaServiceTests`

  Expected: FAIL porque os contratos de `Breeds` ainda não existem.

- [ ] **Step 3: Implementar contratos, validação e DI**

  Usar os seguintes formatos públicos:

  ```csharp
  public sealed record CreateRacaCommand(Guid EspecieId, string Nome);
  public sealed record UpdateRacaCommand(Guid EspecieId, string Nome);
  public sealed record RacaEspecieResumo(Guid Id, string NomeComum, bool Ativo);
  public sealed record RacaResult(
      Guid Id, Guid EspecieId, string Nome, bool Ativo,
      DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc,
      RacaEspecieResumo Especie);
  public sealed record RacaListQuery(
      int Page = 1, int PageSize = 25, string? Search = null,
      Guid? EspecieId = null, bool? Ativo = null,
      RacaSortField SortBy = RacaSortField.Nome, bool SortDescending = false);
  ```

  Em `IRacaRepository.cs`, declarar `RacaReadModel` e `RacaListPage` como projeções explícitas e manter métodos próprios: `AddAsync(Raca)`, `GetByIdReadOnlyAsync(Guid)` retornando `RacaReadModel?`, `GetByIdForUpdateAsync(Guid)` retornando `Raca?`, `ListAsync(RacaListQuery)`, `HasNomeConflictAsync(Guid especieId, string nome, Guid? excludingId)` e `SaveChangesAsync()`. A projeção contém os campos da raça e `RacaEspecieResumo`, sem um repositório compartilhado.

  O serviço deve buscar a espécie solicitada no CREATE e, no UPDATE, somente quando `command.EspecieId != raca.EspecieId`. Espécie ausente lança `EspecieNotFoundException`; espécie de destino inativa lança `ArgumentException` com mensagem de regra de vínculo. Se o identificador não mudou, não aplicar a regra de espécie ativa. Registrar `IRacaService` como scoped em `AddApplication`.

- [ ] **Step 4: Executar a suíte de aplicação relevante**

  Run: `dotnet test tests/GenSW.Application.Tests/GenSW.Application.Tests.csproj --filter "FullyQualifiedName~RacaServiceTests|FullyQualifiedName~DependencyInjectionTests"`

  Expected: PASS.

- [ ] **Step 5: Commit de marco**

  ```bash
  git add src/Backend/GenSW.Application/Breeds src/Backend/GenSW.Application/DependencyInjection.cs tests/GenSW.Application.Tests/RacaServiceTests.cs tests/GenSW.Application.Tests/DependencyInjectionTests.cs
  git commit -m "feat: add breed application service"
  ```

### Task 4: Contratos e serviço de aplicação de Variedade

**Files:**
- Create: `src/Backend/GenSW.Application/Varieties/CreateVariedadeCommand.cs`
- Create: `src/Backend/GenSW.Application/Varieties/UpdateVariedadeCommand.cs`
- Create: `src/Backend/GenSW.Application/Varieties/VariedadeResult.cs`
- Create: `src/Backend/GenSW.Application/Varieties/PagedVariedadeResult.cs`
- Create: `src/Backend/GenSW.Application/Varieties/VariedadeListQuery.cs`
- Create: `src/Backend/GenSW.Application/Varieties/VariedadeSortField.cs`
- Create: `src/Backend/GenSW.Application/Varieties/IVariedadeService.cs`
- Create: `src/Backend/GenSW.Application/Varieties/IVariedadeRepository.cs`
- Create: `src/Backend/GenSW.Application/Varieties/VariedadeService.cs`
- Create: `src/Backend/GenSW.Application/Varieties/VariedadeNotFoundException.cs`
- Create: `src/Backend/GenSW.Application/Varieties/VariedadeDuplicateException.cs`
- Modify: `src/Backend/GenSW.Application/DependencyInjection.cs`
- Create: `tests/GenSW.Application.Tests/VariedadeServiceTests.cs`
- Modify: `tests/GenSW.Application.Tests/DependencyInjectionTests.cs`

**Interfaces:**
- Consumes: `Variedade`, `IEspecieRepository`, `EspecieNotFoundException` e `TimeProvider`.
- Produces: os equivalentes próprios de Raça, inclusive `IVariedadeService` e `IVariedadeRepository`; não consome nem produz tipo de `Breeds`.

- [ ] **Step 1: Escrever os testes de serviço que falham**

  Cobrir criação válida, nome vazio, nome acima de 200, espécie ausente, CREATE com espécie inativa rejeitado, duplicidade normalizada na mesma espécie, mesmo nome em espécie diferente, GET ausente, LIST com busca/filtros/paginação/ordenação inválida, edição de variedade inativa, inativação e reativação. Cobrir UPDATE para outra espécie inativa rejeitado, UPDATE que mantém o `EspecieId` inativo histórico permitido, edição de `Nome` e `Ativo` nessa situação permitida e troca para espécie ativa permitida.

- [ ] **Step 2: Executar o teste vermelho**

  Run: `dotnet test tests/GenSW.Application.Tests/GenSW.Application.Tests.csproj --filter FullyQualifiedName~VariedadeServiceTests`

  Expected: FAIL porque os contratos de `Varieties` ainda não existem.

- [ ] **Step 3: Implementar o serviço independente**

  Criar `CreateVariedadeCommand(Guid EspecieId, string Nome)`, `UpdateVariedadeCommand(Guid EspecieId, string Nome)`, `VariedadeEspecieResumo(Guid Id, string NomeComum, bool Ativo)`, `VariedadeResult`, `VariedadeListQuery`, `VariedadeSortField` e `PagedVariedadeResult`. Em `IVariedadeRepository.cs`, declarar `VariedadeReadModel`, `VariedadeListPage` e métodos próprios de inclusão, leitura, atualização rastreada, lista, conflito por `(EspecieId, Nome)` e gravação. Aplicar exatamente a decisão de comparar o `EspecieId` anterior antes de exigir espécie ativa. Registrar apenas `IVariedadeService` como scoped, sem alterar o contrato de `IRacaService`.

- [ ] **Step 4: Executar a suíte de aplicação relevante**

  Run: `dotnet test tests/GenSW.Application.Tests/GenSW.Application.Tests.csproj --filter "FullyQualifiedName~RacaServiceTests|FullyQualifiedName~VariedadeServiceTests|FullyQualifiedName~DependencyInjectionTests"`

  Expected: PASS.

- [ ] **Step 5: Commit de marco**

  ```bash
  git add src/Backend/GenSW.Application/Varieties src/Backend/GenSW.Application/DependencyInjection.cs tests/GenSW.Application.Tests/VariedadeServiceTests.cs tests/GenSW.Application.Tests/DependencyInjectionTests.cs
  git commit -m "feat: add variety application service"
  ```

### Task 5: Persistência PostgreSQL, repositórios específicos e migration

**Files:**
- Create: `src/Backend/GenSW.Infrastructure/Breeds/RacaRepository.cs`
- Create: `src/Backend/GenSW.Infrastructure/Varieties/VariedadeRepository.cs`
- Modify: `src/Backend/GenSW.Infrastructure/Persistence/GenSWDbContext.cs`
- Modify: `src/Backend/GenSW.Infrastructure/DependencyInjection.cs`
- Create: os dois arquivos `AddRacasVariedades` gerados pelo EF Core em `src/Backend/GenSW.Infrastructure/Persistence/Migrations/` (o prefixo temporal é atribuído pelo comando de migration)
- Modify: `src/Backend/GenSW.Infrastructure/Persistence/Migrations/GenSWDbContextModelSnapshot.cs` (gerado pelo EF Core)
- Create: `tests/GenSW.Infrastructure.Tests/RacaPersistenceModelTests.cs`
- Create: `tests/GenSW.Infrastructure.Tests/VariedadePersistenceModelTests.cs`
- Create: `tests/GenSW.Infrastructure.Tests/RacaRepositoryTests.cs`
- Create: `tests/GenSW.Infrastructure.Tests/VariedadeRepositoryTests.cs`
- Modify: `tests/GenSW.Infrastructure.Tests/PersistenceConfigurationTests.cs`

**Interfaces:**
- Consumes: `IRacaRepository`, `IVariedadeRepository`, `GenSWDbContext`, `Npgsql.PostgresException` e as entidades próprias.
- Produces: `DbSet<Raca> Racas`, `DbSet<Variedade> Variedades`, repositórios scoped e uma migration nova; as migrations `20260820002015_AddIdentityFoundation` e `20260901012054_AddEspecies` permanecem imutáveis.

- [ ] **Step 1: Escrever testes de modelo e integração PostgreSQL que falham**

  Em cada teste de modelo, afirmar tabela (`Racas` ou `Variedades`), PK, `EspecieId` obrigatório, `Nome` obrigatório com máximo 200, `Ativo` default `true`, timestamps obrigatórios, FK para `Especies` com `DeleteBehavior.Restrict` e a check constraint canônica de nome. Em cada repositório, iniciar `EphemeralPostgreSql`, aplicar migrations e testar GET/LIST com busca, `especieId`, `ativo`, paginação e ordenação estável por `Id`.

  Para os dois domínios, testar diretamente no PostgreSQL que:

  ```csharp
  await repository.AddAsync(first);
  await repository.SaveChangesAsync();
  await repository.AddAsync(sameSpeciesNormalizedDuplicate);
  await Assert.ThrowsAsync<RacaDuplicateException>(() => repository.SaveChangesAsync());
  ```

  Para Variedade, persistir uma primeira linha e tentar uma segunda com o mesmo `EspecieId` e nome que normaliza para o mesmo valor, exigindo `VariedadeDuplicateException`. Confirmar que o mesmo nome sob outro `EspecieId` persiste; consultar `pg_indexes` para os dois índices funcionais compostos; e confirmar que uma tentativa de remover uma espécie referenciada é restringida pela FK.

- [ ] **Step 2: Executar os testes vermelhos**

  Run: `dotnet test tests/GenSW.Infrastructure.Tests/GenSW.Infrastructure.Tests.csproj --filter "FullyQualifiedName~RacaPersistenceModelTests|FullyQualifiedName~VariedadePersistenceModelTests|FullyQualifiedName~RacaRepositoryTests|FullyQualifiedName~VariedadeRepositoryTests"`

  Expected: FAIL porque tabelas, repositórios e migration ainda não existem.

- [ ] **Step 3: Configurar EF Core e os repositórios**

  Adicionar as duas entidades ao `GenSWDbContext`. Configurar `HasOne<Especie>().WithMany().HasForeignKey(entity => entity.EspecieId).IsRequired().OnDelete(DeleteBehavior.Restrict)`, as check constraints de nome canônico e `HasDefaultValue(true)`. A migration deve criar `Racas` e `Variedades` e executar, para cada tabela, SQL equivalente a:

  ```sql
  CREATE UNIQUE INDEX "UX_Racas_EspecieId_Nome_CaseInsensitive"
  ON "Racas" ("EspecieId", lower("Nome"));
  ```

  Usar o nome correspondente `UX_Variedades_EspecieId_Nome_CaseInsensitive` para `Variedades`. Gerar a migration com:

  ```bash
  dotnet ef migrations add AddRacasVariedades --project src/Backend/GenSW.Infrastructure/GenSW.Infrastructure.csproj --startup-project src/Backend/GenSW.API/GenSW.API.csproj --output-dir Persistence/Migrations
  ```

  O prefixo temporal é atribuído pelo EF Core; não pré-criar arquivos e não editar migrations anteriores. Em cada repositório, capturar `DbUpdateException` cuja `PostgresException` tenha `SqlState == PostgresErrorCodes.UniqueViolation` e o nome do índice do próprio domínio, lançando a exceção de duplicidade correspondente. Registrar ambos os repositórios como scoped.

- [ ] **Step 4: Executar a suíte de infraestrutura relevante**

  Run: `dotnet test tests/GenSW.Infrastructure.Tests/GenSW.Infrastructure.Tests.csproj --filter "FullyQualifiedName~Raca|FullyQualifiedName~Variedade|FullyQualifiedName~PersistenceConfigurationTests"`

  Expected: PASS; quando os binários PostgreSQL locais não estiverem instalados, a infraestrutura existente marca somente os testes de integração PostgreSQL como skipped.

- [ ] **Step 5: Commit de marco**

  ```bash
  git add src/Backend/GenSW.Infrastructure tests/GenSW.Infrastructure.Tests
  git commit -m "feat: persist breeds and varieties"
  ```

### Task 6: API autenticada de Raças

**Files:**
- Create: `src/Backend/GenSW.API/Contracts/Breeds/CreateRacaRequest.cs`
- Create: `src/Backend/GenSW.API/Contracts/Breeds/UpdateRacaRequest.cs`
- Create: `src/Backend/GenSW.API/Contracts/Breeds/UpdateRacaStatusRequest.cs`
- Create: `src/Backend/GenSW.API/Contracts/Breeds/EspecieResumoResponse.cs`
- Create: `src/Backend/GenSW.API/Contracts/Breeds/RacaResponse.cs`
- Create: `src/Backend/GenSW.API/Contracts/Breeds/RacasListResponse.cs`
- Create: `src/Backend/GenSW.API/Controllers/RacasController.cs`
- Create: `tests/GenSW.API.Tests/RacasApiTests.cs`

**Interfaces:**
- Consumes: `IRacaService`, `CreateRacaCommand`, `UpdateRacaCommand`, `RacaListQuery`, `RacaNotFoundException`, `RacaDuplicateException`, `EspecieNotFoundException` e `ArgumentException` para destino inativo.
- Produces: endpoints protegidos `POST|GET /api/v1/racas`, `GET|PUT /api/v1/racas/{id:guid}` e `PATCH /api/v1/racas/{id:guid}/ativo`; não produz endpoint DELETE.

- [ ] **Step 1: Escrever testes de aceitação que falham**

  Basear o fixture em `AuthWebApplicationFactory` e autenticar como em `EspeciesApiTests`. Cobrir `401` anônimo; `201` com `Location`; GET/LIST com `search`, `especieId`, `ativo`, paginação e ordenação; `400` para nome vazio, nome acima de 200, consulta inválida e espécie inativa em CREATE ou troca; `404` para raça ou espécie inexistente; `409` para duplicidade normalizada; `PATCH` de status; e `405 MethodNotAllowed` para DELETE.

  Semear espécies por `factory.ExecuteDbContextAsync`: uma ativa, a espécie vinculada que será inativada e uma segunda inativa. Testar no HTTP os cinco casos de espécie inativa aprovados, especialmente PUT com o mesmo identificador e mudança apenas de nome/status versus PUT que aponta para a segunda inativa.

- [ ] **Step 2: Executar o teste vermelho**

  Run: `dotnet test tests/GenSW.API.Tests/GenSW.API.Tests.csproj --filter FullyQualifiedName~RacasApiTests`

  Expected: FAIL porque a rota e os contratos não existem.

- [ ] **Step 3: Implementar controller e contratos**

  Criar `RacasController` com `[ApiController]`, `[Authorize]` e `[Route("api/v1/racas")]`, espelhando a estrutura de `EspeciesController`. Mapear `RacaResult` para `RacaResponse`, incluindo o resumo de espécie. Converter `RacaDuplicateException` em `409`; `RacaNotFoundException` e `EspecieNotFoundException` em `404`; e `ArgumentException` em `400`. Não capturar a exceção de espécie inativa como `404`.

- [ ] **Step 4: Executar os testes da API de Raças**

  Run: `dotnet test tests/GenSW.API.Tests/GenSW.API.Tests.csproj --filter FullyQualifiedName~RacasApiTests`

  Expected: PASS.

- [ ] **Step 5: Commit de marco**

  ```bash
  git add src/Backend/GenSW.API/Contracts/Breeds src/Backend/GenSW.API/Controllers/RacasController.cs tests/GenSW.API.Tests/RacasApiTests.cs
  git commit -m "feat: expose breed api"
  ```

### Task 7: API autenticada de Variedades

**Files:**
- Create: `src/Backend/GenSW.API/Contracts/Varieties/CreateVariedadeRequest.cs`
- Create: `src/Backend/GenSW.API/Contracts/Varieties/UpdateVariedadeRequest.cs`
- Create: `src/Backend/GenSW.API/Contracts/Varieties/UpdateVariedadeStatusRequest.cs`
- Create: `src/Backend/GenSW.API/Contracts/Varieties/EspecieResumoResponse.cs`
- Create: `src/Backend/GenSW.API/Contracts/Varieties/VariedadeResponse.cs`
- Create: `src/Backend/GenSW.API/Contracts/Varieties/VariedadesListResponse.cs`
- Create: `src/Backend/GenSW.API/Controllers/VariedadesController.cs`
- Create: `tests/GenSW.API.Tests/VariedadesApiTests.cs`

**Interfaces:**
- Consumes: `IVariedadeService`, contratos próprios de `Varieties`, `AuthWebApplicationFactory` e `AuthTestHttp`.
- Produces: `POST|GET /api/v1/variedades`, `GET|PUT /api/v1/variedades/{id:guid}` e `PATCH /api/v1/variedades/{id:guid}/ativo`.

- [ ] **Step 1: Escrever testes de aceitação que falham**

  Cobrir `401` anônimo; `201` e `Location` no CREATE; GET; LIST com `search`, `especieId`, `ativo`, paginação e ordenação; `400` para nome vazio, nome acima de 200, consulta inválida, CREATE com espécie inativa e troca para espécie inativa; `404` para variedade ou espécie inexistente; `409` para duplicidade normalizada; PATCH de status; e `405` em DELETE. Semear espécie ativa, espécie vinculada depois inativada e outra inativa para comprovar os cinco cenários aprovados de vínculo.

- [ ] **Step 2: Executar o teste vermelho**

  Run: `dotnet test tests/GenSW.API.Tests/GenSW.API.Tests.csproj --filter FullyQualifiedName~VariedadesApiTests`

  Expected: FAIL porque a rota e os contratos não existem.

- [ ] **Step 3: Implementar controller e contratos próprios**

  Criar `VariedadesController` com `[ApiController]`, `[Authorize]` e `[Route("api/v1/variedades")]`. Mapear `VariedadeResult` para `VariedadeResponse`, incluindo resumo de espécie; converter `VariedadeDuplicateException` em `409`, registro ou espécie ausente em `404` e `ArgumentException` em `400`. O resumo de espécie pertence à resposta de Variedade, sem importar contratos de `Breeds`.

- [ ] **Step 4: Executar os testes da API de Variedades**

  Run: `dotnet test tests/GenSW.API.Tests/GenSW.API.Tests.csproj --filter FullyQualifiedName~VariedadesApiTests`

  Expected: PASS.

- [ ] **Step 5: Commit de marco**

  ```bash
  git add src/Backend/GenSW.API/Contracts/Varieties src/Backend/GenSW.API/Controllers/VariedadesController.cs tests/GenSW.API.Tests/VariedadesApiTests.cs
  git commit -m "feat: expose variety api"
  ```

### Task 8: Contrato HTTP frontend de Raças

**Files:**
- Create: `src/Frontend/GenSW.Web/src/features/breeds/types/breeds.ts`
- Create: `src/Frontend/GenSW.Web/src/features/breeds/services/breedsContractParsers.ts`
- Create: `src/Frontend/GenSW.Web/src/features/breeds/services/breedsService.ts`
- Create: `src/Frontend/GenSW.Web/src/features/breeds/services/breedsContractParsers.test.ts`
- Create: `src/Frontend/GenSW.Web/src/features/breeds/services/breedsService.test.ts`

**Interfaces:**
- Consumes: `httpRequest`, `InvalidApiResponseError` e o contrato já existente de listagem de espécies em `features/species/services/speciesService.ts`.
- Produces: tipos `Raca`, `RacasPage`, `CreateRacaRequest`, `UpdateRacaRequest`, `UpdateRacaStatusRequest`, `ListRacasParams`; parsers `parseRaca`/`parseRacasPage`; e funções `createRaca`, `getRacaById`, `listRacas`, `updateRaca`, `setRacaAtivo` para `/racas`.

- [ ] **Step 1: Escrever testes de parser e serviço que falham**

  Validar uma resposta com `id`, `especieId`, `nome`, `ativo`, timestamps e `{ especie: { id, nomeComum, ativo } }`; rejeitar cada campo inválido; e confirmar método, URL, corpo autenticado e query string de `listRacas`, inclusive `especieId`.

- [ ] **Step 2: Executar o teste vermelho**

  Run: `npm --prefix src/Frontend/GenSW.Web test -- src/features/breeds`

  Expected: FAIL porque a feature `breeds` não existe.

- [ ] **Step 3: Implementar tipos, parser e cliente**

  Reproduzir o padrão de `features/species`, alterando somente o contrato. `listRacas` constrói `/racas` com `page`, `pageSize`, `search`, `especieId`, `ativo`, `sortBy` e `sortDirection`; todas as chamadas usam `authenticated: true`.

- [ ] **Step 4: Executar os testes da camada HTTP**

  Run: `npm --prefix src/Frontend/GenSW.Web test -- src/features/breeds/services`

  Expected: PASS.

- [ ] **Step 5: Commit de marco**

  ```bash
  git add src/Frontend/GenSW.Web/src/features/breeds
  git commit -m "feat: add breed frontend client"
  ```

### Task 9: Contrato HTTP frontend de Variedades

**Files:**
- Create: `src/Frontend/GenSW.Web/src/features/varieties/types/varieties.ts`
- Create: `src/Frontend/GenSW.Web/src/features/varieties/services/varietiesContractParsers.ts`
- Create: `src/Frontend/GenSW.Web/src/features/varieties/services/varietiesService.ts`
- Create: `src/Frontend/GenSW.Web/src/features/varieties/services/varietiesContractParsers.test.ts`
- Create: `src/Frontend/GenSW.Web/src/features/varieties/services/varietiesService.test.ts`

**Interfaces:**
- Consumes: o mesmo cliente HTTP compartilhado e `listEspecies`; não importa tipos de Raças.
- Produces: os tipos, parsers e funções próprias `createVariedade`, `getVariedadeById`, `listVariedades`, `updateVariedade` e `setVariedadeAtivo` para `/variedades`.

- [ ] **Step 1: Escrever testes de parser e serviço que falham**

  Aceitar somente resposta de Variedade com `id`, `especieId`, `nome`, `ativo`, timestamps e resumo `{ id, nomeComum, ativo }`; rejeitar campo ausente ou de tipo inválido; e confirmar método, endpoint, corpo autenticado e query string de `listVariedades`, incluindo `especieId`, `ativo`, paginação e ordenação.

- [ ] **Step 2: Executar o teste vermelho**

  Run: `npm --prefix src/Frontend/GenSW.Web test -- src/features/varieties/services`

  Expected: FAIL porque a feature `varieties` não existe.

- [ ] **Step 3: Implementar tipos, parser e cliente próprios**

  Seguir os contratos da API de Variedades e o formato de `speciesService.ts`; manter endpoint e nomes de função próprios.

- [ ] **Step 4: Executar os testes da camada HTTP**

  Run: `npm --prefix src/Frontend/GenSW.Web test -- src/features/varieties/services`

  Expected: PASS.

- [ ] **Step 5: Commit de marco**

  ```bash
  git add src/Frontend/GenSW.Web/src/features/varieties
  git commit -m "feat: add variety frontend client"
  ```

### Task 10: Listagem e lifecycle de Raças no frontend

**Files:**
- Create: `src/Frontend/GenSW.Web/src/features/breeds/pages/BreedsListPage.tsx`
- Create: `src/Frontend/GenSW.Web/src/features/breeds/pages/BreedsListPage.test.tsx`

**Interfaces:**
- Consumes: `listRacas`, `setRacaAtivo`, `ListRacasParams` e a rota de formulário `/racas/nova`/`/racas/:id/editar`.
- Produces: listagem de Raças com busca, filtro de espécie, filtro de status, ordenação, paginação, links de edição e ação de ativar/inativar.

- [ ] **Step 1: Escrever testes de página que falham**

  Mockar o serviço de Raças. Verificar carregamento, tabela com nome e resumo de espécie, busca, filtro de espécie, status, ordem, paginação, estado vazio, erro e retry. Verificar que uma raça inativa ainda apresenta link `Editar`; que status mutation bloqueia duplo clique, recarrega os filtros atuais e trata erro.

- [ ] **Step 2: Executar o teste vermelho**

  Run: `npm --prefix src/Frontend/GenSW.Web test -- src/features/breeds/pages/BreedsListPage.test.tsx`

  Expected: FAIL porque a página não existe.

- [ ] **Step 3: Implementar a listagem**

  Adaptar apenas o comportamento de `SpeciesListPage`: usar `listRacas`, incluir o `select` de espécie no filtro, renderizar `raca.especie.nomeComum` e preservar os parâmetros atuais ao recarregar depois de `setRacaAtivo`.

- [ ] **Step 4: Executar o teste da página**

  Run: `npm --prefix src/Frontend/GenSW.Web test -- src/features/breeds/pages/BreedsListPage.test.tsx`

  Expected: PASS.

- [ ] **Step 5: Commit de marco**

  ```bash
  git add src/Frontend/GenSW.Web/src/features/breeds/pages
  git commit -m "feat: add breed list and lifecycle"
  ```

### Task 11: Listagem e lifecycle de Variedades no frontend

**Files:**
- Create: `src/Frontend/GenSW.Web/src/features/varieties/pages/VarietiesListPage.tsx`
- Create: `src/Frontend/GenSW.Web/src/features/varieties/pages/VarietiesListPage.test.tsx`

**Interfaces:**
- Consumes: `listVariedades`, `setVariedadeAtivo`, `ListVariedadesParams` e as rotas `/variedades`.
- Produces: listagem independente de Variedades com o mesmo conjunto de controles de consulta e lifecycle.

- [ ] **Step 1: Escrever testes de página que falham**

  Cobrir dados, busca, filtros de espécie e status, ordenação, paginação, vazio, retry, edição de registro inativo, inativação, reativação e bloqueio de mutação concorrente da própria linha.

- [ ] **Step 2: Executar o teste vermelho**

  Run: `npm --prefix src/Frontend/GenSW.Web test -- src/features/varieties/pages/VarietiesListPage.test.tsx`

  Expected: FAIL porque a página não existe.

- [ ] **Step 3: Implementar a listagem própria**

  Usar somente `varietiesService` e os tipos de Variedade. Não reutilizar componente de Raças até existir uma necessidade comprovada além desta demanda.

- [ ] **Step 4: Executar o teste da página**

  Run: `npm --prefix src/Frontend/GenSW.Web test -- src/features/varieties/pages/VarietiesListPage.test.tsx`

  Expected: PASS.

- [ ] **Step 5: Commit de marco**

  ```bash
  git add src/Frontend/GenSW.Web/src/features/varieties/pages
  git commit -m "feat: add variety list and lifecycle"
  ```

### Task 12: Formulário de Raça e seleção de espécie ativa

**Files:**
- Create: `src/Frontend/GenSW.Web/src/features/breeds/pages/BreedFormPage.tsx`
- Create: `src/Frontend/GenSW.Web/src/features/breeds/pages/BreedFormPage.test.tsx`

**Interfaces:**
- Consumes: `createRaca`, `getRacaById`, `updateRaca`, `listEspecies`, `isHttpError` e as rotas de Raças.
- Produces: criação e edição de Raça com `{ especieId, nome }` e seleção que representa corretamente espécies ativas e vínculo histórico inativo.

- [ ] **Step 1: Escrever testes de formulário que falham**

  Cobrir criação válida e nome localmente inválido; carregar espécies com `listEspecies({ ativo: true, pageSize: 100, sortBy: 'nomeComum', sortDirection: 'asc' })`; enviar apenas espécie ativa ao criar; `404`, `400` e `409` como erros de salvamento legíveis; e estados loading/not-found/retry.

  Para edição, mockar uma raça cuja `especie.ativo` é `false` e afirmar: a espécie atual aparece selecionada; salvar alteração de nome com o mesmo `especieId` chama `updateRaca`; a lista não contém outra espécie inativa; e a troca para uma espécie ativa envia o identificador ativo. O teste não deve aceitar uma opção inativa diferente da vinculada.

- [ ] **Step 2: Executar o teste vermelho**

  Run: `npm --prefix src/Frontend/GenSW.Web test -- src/features/breeds/pages/BreedFormPage.test.tsx`

  Expected: FAIL porque a página não existe.

- [ ] **Step 3: Implementar o formulário**

  Carregar a raça antes de montar as opções na edição. Criar a lista de opções a partir de espécies ativas e, somente se `raca.especie.ativo` for falso, acrescentar a espécie atual como opção preservada. O submit normaliza `nome`, exige `1..200` e usa `createRaca` ou `updateRaca`; não permitir que o navegador envie uma espécie inativa que não esteja no vínculo atual.

- [ ] **Step 4: Executar o teste do formulário**

  Run: `npm --prefix src/Frontend/GenSW.Web test -- src/features/breeds/pages/BreedFormPage.test.tsx`

  Expected: PASS.

- [ ] **Step 5: Commit de marco**

  ```bash
  git add src/Frontend/GenSW.Web/src/features/breeds/pages/BreedFormPage.tsx src/Frontend/GenSW.Web/src/features/breeds/pages/BreedFormPage.test.tsx
  git commit -m "feat: add breed form"
  ```

### Task 13: Formulário de Variedade e seleção de espécie ativa

**Files:**
- Create: `src/Frontend/GenSW.Web/src/features/varieties/pages/VarietyFormPage.tsx`
- Create: `src/Frontend/GenSW.Web/src/features/varieties/pages/VarietyFormPage.test.tsx`

**Interfaces:**
- Consumes: `createVariedade`, `getVariedadeById`, `updateVariedade`, `listEspecies` e as rotas de Variedades.
- Produces: criação e edição de Variedade com a mesma política de espécies, sem compartilhar formulário com Raças.

- [ ] **Step 1: Escrever testes de formulário que falham**

  Cobrir criação válida, nome localmente inválido, carregamento com `listEspecies({ ativo: true, pageSize: 100, sortBy: 'nomeComum', sortDirection: 'asc' })`, erro `404`/`400`/`409`, loading/not-found/retry e criação apenas com espécie ativa. Na edição de variedade com espécie atual inativa, exigir opção atual visível e selecionada, atualização de nome ou status com o mesmo `especieId`, ausência de outra espécie inativa e troca permitida para uma espécie ativa.

- [ ] **Step 2: Executar o teste vermelho**

  Run: `npm --prefix src/Frontend/GenSW.Web test -- src/features/varieties/pages/VarietyFormPage.test.tsx`

  Expected: FAIL porque a página não existe.

- [ ] **Step 3: Implementar o formulário próprio**

  Carregar a variedade e as espécies ativas. Montar opções somente com espécies ativas, acrescentando a espécie vinculada exclusivamente quando ela estiver inativa. Normalizar `nome`, validar `1..200`, enviar por `createVariedade` ou `updateVariedade` e manter nomes de componentes, state e chamadas HTTP próprios de Variedades.

- [ ] **Step 4: Executar o teste do formulário**

  Run: `npm --prefix src/Frontend/GenSW.Web test -- src/features/varieties/pages/VarietyFormPage.test.tsx`

  Expected: PASS.

- [ ] **Step 5: Commit de marco**

  ```bash
  git add src/Frontend/GenSW.Web/src/features/varieties/pages/VarietyFormPage.tsx src/Frontend/GenSW.Web/src/features/varieties/pages/VarietyFormPage.test.tsx
  git commit -m "feat: add variety form"
  ```

### Task 14: Rotas protegidas, navegação e teste de integração do frontend

**Files:**
- Modify: `src/Frontend/GenSW.Web/src/routes/AppRoutes.tsx`
- Modify: `src/Frontend/GenSW.Web/src/routes/AppRoutes.test.tsx`
- Modify: `src/Frontend/GenSW.Web/src/features/auth/pages/AuthenticatedHomePage.tsx`

**Interfaces:**
- Consumes: `BreedsListPage`, `BreedFormPage`, `VarietiesListPage`, `VarietyFormPage` e o `ProtectedRoute` existente.
- Produces: rotas protegidas `/racas`, `/racas/nova`, `/racas/:id/editar`, `/variedades`, `/variedades/nova` e `/variedades/:id/editar`, além dos links `Raças` e `Variedades` na home autenticada.

- [ ] **Step 1: Escrever os testes de rota que falham**

  Estender os mocks de serviço em `AppRoutes.test.tsx`. Para cada uma das seis rotas, testar sessão anônima redirecionada para login e sessão autenticada renderizando o heading correto. Testar cliques na home para `Raças` e `Variedades`.

- [ ] **Step 2: Executar o teste vermelho**

  Run: `npm --prefix src/Frontend/GenSW.Web test -- src/routes/AppRoutes.test.tsx`

  Expected: FAIL porque as rotas e links não existem.

- [ ] **Step 3: Registrar rotas e navegação**

  Importar as quatro páginas novas em `AppRoutes.tsx` e inseri-las dentro do `ProtectedRoute`. Acrescentar somente os dois links de cadastro na área já existente da home; não alterar autenticação ou outras rotas.

- [ ] **Step 4: Executar os testes de rotas**

  Run: `npm --prefix src/Frontend/GenSW.Web test -- src/routes/AppRoutes.test.tsx`

  Expected: PASS.

- [ ] **Step 5: Commit de marco**

  ```bash
  git add src/Frontend/GenSW.Web/src/routes/AppRoutes.tsx src/Frontend/GenSW.Web/src/routes/AppRoutes.test.tsx src/Frontend/GenSW.Web/src/features/auth/pages/AuthenticatedHomePage.tsx
  git commit -m "feat: add breed and variety navigation"
  ```

### Task 15: Gates integrados e inspeção de escopo

**Files:**
- Modify only if a prior test identifies a defect in its owning file; this task creates no production artifact.

**Interfaces:**
- Consumes: todos os artefatos das Tasks 1–14 e os comandos de validação existentes no repositório.
- Produces: evidência objetiva de build, testes, lint e escopo, sem código de Animal ou módulos futuros.

- [ ] **Step 1: Executar testes backend completos**

  Run: `dotnet test GenSW.sln`

  Expected: PASS; testes PostgreSQL podem ficar skipped somente pela detecção já existente de binários locais ausentes.

- [ ] **Step 2: Executar build backend**

  Run: `dotnet build GenSW.sln --no-restore`

  Expected: PASS sem warnings novos.

- [ ] **Step 3: Executar gates frontend**

  ```bash
  npm --prefix src/Frontend/GenSW.Web test
  npm --prefix src/Frontend/GenSW.Web run lint
  npm --prefix src/Frontend/GenSW.Web run build
  ```

  Expected: os três comandos retornam código 0.

- [ ] **Step 4: Inspecionar migration, diff e escopo**

  ```bash
  git diff --check
  git diff --name-only origin/main...HEAD
  rg -n "Animal|IdentificacaoAnimal|RegistroAnimal|Filiacao|Pedigree|Cruzamento|Genética|Fenótipo|Produção|Propriedade|Lotes" src tests
  ```

  Expected: `git diff --check` sem saída; o diff contém apenas RA-02 e a nova migration; nenhuma ocorrência nova é introduzida fora de texto de teste já existente.

- [ ] **Step 5: Registrar o resultado dos gates**

  Não criar commit vazio. Se algum gate falhar, voltar exclusivamente à tarefa proprietária do arquivo apontado, corrigir, repetir os comandos de validação e criar um commit com os paths corrigidos explicitamente listados. Se todos passarem, o estado final é uma árvore sem alterações pendentes e evidências prontas para PR e Redmine.

## Coverage Matrix

| Requisito aprovado | Tasks |
| --- | --- |
| Raça e Variedade independentes, Domain e lifecycle | 1–4 |
| CREATE inativo rejeitado; novo vínculo inativo rejeitado | 3, 4, 6, 7, 12, 13 |
| Vínculo inativo histórico preservado; editar Nome/Ativo; trocar para ativa | 3, 4, 6, 7, 12, 13 |
| FK, `DeleteBehavior.Restrict`, limites, default, timestamps e índice PostgreSQL | 5 |
| Conflito concorrente convertido em `409` | 3–7 |
| API autenticada, LIST/GET/CREATE/UPDATE/PATCH e sem DELETE | 6–7 |
| Tipos, parsers, HTTP, listas, filtros, formulários e lifecycle frontend | 8–14 |
| Gates backend/frontend e ausência de #295–#299 | 15 |

## Execution Gate

Este plano não autoriza implementação nesta etapa. Após a revisão humana do plano, escolher `superpowers:subagent-driven-development` ou `superpowers:executing-plans` e executar uma tarefa por vez, preservando seus gates de teste e commit.
