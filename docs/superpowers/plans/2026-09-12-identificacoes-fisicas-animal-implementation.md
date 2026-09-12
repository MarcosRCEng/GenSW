# NA-04 — Identificações físicas do Animal Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver physical Animal identifiers as a historical, globally searchable, authenticated resource with PostgreSQL-backed uniqueness and principal-state concurrency safety.

**Architecture:** `IdentificacaoAnimal` is a separate Domain entity keyed by `Guid` and owned operationally by `AnimalId`; it has no mutable identifier fields after creation. A focused Application/Infrastructure module follows the existing Animal vertical slice, while PostgreSQL owns final integrity through named checks and expression/partial indexes. The React edit screen hosts a dedicated identifiers panel only after the Animal has an `Id`; the creation screen remains unchanged.

**Tech Stack:** .NET 8/C#; EF Core 8.0.10; Npgsql 8.0.10/PostgreSQL; ASP.NET Core controllers and `ProblemDetails`; xUnit; React 18; TypeScript; Vitest; Testing Library; Vite; ESLint.

**Spec:** docs/superpowers/specs/2026-09-12-identificacoes-fisicas-animal-design.md

## Global Constraints

- Work only on `feature/296-identificacoes-animal`, whose approved SPEC tip is `20484e7`; do not rebase without an explicit new-base decision.
- `Animal.Id` remains the technical identity and `CodigoInterno` remains the operational reference. `IdentificacaoAnimal` is a separate physical marker resource with zero-or-many rows per Animal.
- Initial `TipoIdentificacaoAnimal` values are `Anilha = 1`, `Brinco = 2`, `Microchip = 3`, `Tatuagem = 4`, `Marca = 5`, and `Outro = 6`.
- Preserve only edge `Trim` for `Valor` and `DescricaoTipo`; preserve internal spaces, punctuation, zeros, prefixes, and supplied casing. Comparisons are case-insensitive.
- `Tipo`, `DescricaoTipo`, and `Valor` are immutable after creation. Physical replacement is inactivate-plus-create; no physical deletion, `DELETE`, generic `PUT`, global mutation, RFID, QR Code, reader, marker inventory, institutional registration (#297), pedigree, crossing, ownership, location, lot, or #298/#299 work is permitted.
- `Valor` is required with maximum length 128; `DescricaoTipo` is nullable with maximum length 100; `Observacao` is nullable with maximum length 1,000. These conservative operational limits follow the existing explicit `HasMaxLength` convention while keeping identifiers short and notes bounded.
- `DescricaoTipo` is required only for `Outro` and is persisted as `NULL` for all other types. `Principal` requires an already active row; setting principal never activates a row; reactivation never promotes; inactivating a principal clears it and never promotes another row.
- All nested routes validate the pair `(animalId, identificacaoId)` and return 404 when the row belongs to another Animal, without revealing its existence. All routes in this feature use `[Authorize]`.
- PostgreSQL named violations are translated only by `SqlState` plus `ConstraintName`, never exception text, `Detail`, or substring parsing. Known duplicate and principal conflicts map to 409; missing Animal or nested row map to 404; invalid request/domain input maps to 400; other database failures remain unmasked by a false 409 mapping.
- Principal-changing work uses one Npgsql transaction, `SELECT ... FOR UPDATE` over the parent `Animais` row, then the named partial unique index as final persistence defense. It serializes only requests for the same Animal.
- PATCH metadata uses explicit presence flags for each nullable field. A missing property leaves a field unchanged; a supplied value replaces it; a supplied JSON `null` clears it. Do not add a package or generic optional-value framework.
- Every functional task follows red test, minimal implementation, green test, and review checkpoint. Do not create production commits per task. Only Task 17 creates the integrated implementation commit, then pushes and opens a PR after every gate passes.

## Execution dependency order

`1 → 2 → 3 → 4 → 5 → 6 → 7 → 8 → 9 → 10 → 11 → 12 → 13 → 14 → 15 → 16 → 17`.

Tasks 1–16 own functional contracts and tests. Tasks 7 and 8 are PostgreSQL evidence gates, Task 15 is the frontend integration gate, Task 16 is focused regression evidence, and Task 17 is the only integrated release gate. A failure in a gate returns to its named owner task; no gate authorizes unrelated refactoring.

## File Map

| Area | Planned paths and responsibility |
| --- | --- |
| Domain | `src/Backend/GenSW.Domain/Animals/TipoIdentificacaoAnimal.cs`, `IdentificacaoAnimal.cs`, and `tests/GenSW.Domain.Tests/IdentificacaoAnimalTests.cs`: immutable marker identity and local lifecycle invariants. |
| Application | `src/Backend/GenSW.Application/Animals/Identificacoes/`: commands, results, local/global queries, repository/service contracts, explicit exceptions, and `IdentificacaoAnimalService.cs`; `tests/GenSW.Application.Tests/IdentificacaoAnimalContractsTests.cs` and `IdentificacaoAnimalServiceTests.cs`. |
| Infrastructure | `src/Backend/GenSW.Infrastructure/Animals/Identificacoes/IdentificacaoAnimalRepository.cs`; `GenSWDbContext.cs`; both DI files; one EF migration pair ending `_AddIdentificacoesAnimal`; snapshot; Infrastructure tests for mapping, repository, migration, and named constraint conversion. |
| API | `src/Backend/GenSW.API/Contracts/AnimalIdentifications/` request/response classes, `Controllers/IdentificacoesAnimaisController.cs`, `Controllers/IdentificacoesAnimalController.cs`, and `tests/GenSW.API.Tests/IdentificacoesAnimaisApiTests.cs`. |
| PostgreSQL concurrency | `tests/GenSW.API.Tests/PostgreSqlIdentificacoesAnimalConcurrencyTests.cs`, using the existing `EphemeralPostgreSql` and `AnimalApiPostgreSqlFixture` style. |
| Frontend | `src/Frontend/GenSW.Web/src/features/animals/identifications/types.ts`, `services/identificationsService.ts`, `services/identificationsContractParsers.ts`, `components/AnimalIdentificationsPanel.tsx`, their `.test.ts`/`.test.tsx` files, plus `AnimalFormPage.tsx` and `AnimalFormPage.test.tsx`. |

## Cross-task contracts

Task 2 creates these names; later tasks must consume them without renaming.

```csharp
public sealed record CreateIdentificacaoAnimalCommand(
    TipoIdentificacaoAnimal Tipo, string? DescricaoTipo, string Valor,
    bool Principal, DateOnly? DataAplicacao, string? Observacao);

public sealed record UpdateIdentificacaoAnimalMetadataCommand(
    bool HasDataAplicacao, DateOnly? DataAplicacao,
    bool HasObservacao, string? Observacao);

public sealed record SetIdentificacaoAnimalAtivoCommand(bool Ativo);
public sealed record SetIdentificacaoAnimalPrincipalCommand(bool Principal);

public sealed record IdentificacaoAnimalResult(
    Guid Id, Guid AnimalId, TipoIdentificacaoAnimal Tipo, string? DescricaoTipo,
    string Valor, bool Principal, DateOnly? DataAplicacao, string? Observacao,
    bool Ativo, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);

public sealed record IdentificacaoAnimalAnimalResumo(Guid Id, string CodigoInterno, string? Nome);
public sealed record IdentificacaoAnimalGlobalResult(IdentificacaoAnimalResult Identificacao, IdentificacaoAnimalAnimalResumo Animal);
public sealed record IdentificacaoAnimalListQuery(
    int Page = 1, int PageSize = 25, TipoIdentificacaoAnimal? Tipo = null,
    string? Valor = null, bool? Ativo = null, bool? Principal = null);
public sealed record PagedIdentificacaoAnimalResult(
    IReadOnlyList<IdentificacaoAnimalResult> Items, int Page, int PageSize, int TotalItems, int TotalPages);
public sealed record PagedIdentificacaoAnimalGlobalResult(
    IReadOnlyList<IdentificacaoAnimalGlobalResult> Items, int Page, int PageSize, int TotalItems, int TotalPages);
```

```csharp
public interface IIdentificacaoAnimalService
{
    Task<IdentificacaoAnimalResult> CreateAsync(Guid animalId, CreateIdentificacaoAnimalCommand command, CancellationToken cancellationToken = default);
    Task<PagedIdentificacaoAnimalResult> ListByAnimalAsync(Guid animalId, IdentificacaoAnimalListQuery query, CancellationToken cancellationToken = default);
    Task<IdentificacaoAnimalResult> GetByAnimalAsync(Guid animalId, Guid identificacaoId, CancellationToken cancellationToken = default);
    Task<IdentificacaoAnimalResult> UpdateMetadataAsync(Guid animalId, Guid identificacaoId, UpdateIdentificacaoAnimalMetadataCommand command, CancellationToken cancellationToken = default);
    Task<IdentificacaoAnimalResult> SetAtivoAsync(Guid animalId, Guid identificacaoId, SetIdentificacaoAnimalAtivoCommand command, CancellationToken cancellationToken = default);
    Task<IdentificacaoAnimalResult> SetPrincipalAsync(Guid animalId, Guid identificacaoId, SetIdentificacaoAnimalPrincipalCommand command, CancellationToken cancellationToken = default);
    Task<PagedIdentificacaoAnimalGlobalResult> ListGlobalAsync(IdentificacaoAnimalListQuery query, CancellationToken cancellationToken = default);
}
```

The repository contract uses a transaction scope only for principal-changing operations:

```csharp
public interface IIdentificacaoAnimalMutationScope : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}

public interface IIdentificacaoAnimalRepository
{
    Task<IIdentificacaoAnimalMutationScope> BeginMutationAsync(CancellationToken cancellationToken = default);
    Task<Animal?> LockAnimalAsync(Guid animalId, CancellationToken cancellationToken = default);
    Task<bool> AnimalExistsAsync(Guid animalId, CancellationToken cancellationToken = default);
    Task AddAsync(IdentificacaoAnimal identificacao, CancellationToken cancellationToken = default);
    Task<IdentificacaoAnimal?> GetByAnimalForUpdateAsync(Guid animalId, Guid identificacaoId, CancellationToken cancellationToken = default);
    Task<IdentificacaoAnimalResult?> GetByAnimalReadOnlyAsync(Guid animalId, Guid identificacaoId, CancellationToken cancellationToken = default);
    Task<PagedIdentificacaoAnimalResult> ListByAnimalAsync(Guid animalId, IdentificacaoAnimalListQuery query, CancellationToken cancellationToken = default);
    Task<PagedIdentificacaoAnimalGlobalResult> ListGlobalAsync(IdentificacaoAnimalListQuery query, CancellationToken cancellationToken = default);
    Task<bool> HasDuplicateAsync(TipoIdentificacaoAnimal tipo, string? descricaoTipo, string valor, CancellationToken cancellationToken = default);
    Task<IdentificacaoAnimal?> GetCurrentPrincipalForUpdateAsync(Guid animalId, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
```

`BeginMutationAsync` must call `Database.BeginTransactionAsync`; `LockAnimalAsync` must run `context.Animais.FromSqlInterpolated($"SELECT * FROM \"Animais\" WHERE \"Id\" = {animalId} FOR UPDATE")` while that transaction is active. `IIdentificacaoAnimalMutationScope.CommitAsync` commits the same scoped `GenSWDbContext` transaction; disposing an uncommitted scope rolls it back. This local concrete scope avoids a generic unit-of-work abstraction.

### Task 1: Domain identifier aggregate and invariant tests

**Files:**
- Create: `src/Backend/GenSW.Domain/Animals/TipoIdentificacaoAnimal.cs`
- Create: `src/Backend/GenSW.Domain/Animals/IdentificacaoAnimal.cs`
- Create: `tests/GenSW.Domain.Tests/IdentificacaoAnimalTests.cs`

**Interfaces:**
- Consumes: BCL `Guid`, `DateOnly`, `DateTimeOffset` and existing Animal timestamp conventions.
- Produces: `TipoIdentificacaoAnimal`; `IdentificacaoAnimal.Criar`, `AlterarMetadados`, `DefinirPrincipal`, `RemoverPrincipal`, `Inativar`, and `Reativar` for Tasks 2–6.

- [ ] **Step 1: Write the failing Domain tests**

  Cover all six enum values and an undefined cast; valid Anilha and Outro; edge trim while preserving `A  01-0`; empty/over-128 value; valid/empty/over-100 Outro description; forbidden description for non-Outro; nullable/over-1,000 observation; immutable identifier properties by exposing no setters or identifier mutation method; principal active requirement; idempotent removal; inactivation clearing principal; reactivation retaining non-principal state; timestamp changes only on state changes.

- [ ] **Step 2: Run the red test**

  Run: `dotnet test tests/GenSW.Domain.Tests/GenSW.Domain.Tests.csproj --filter FullyQualifiedName~IdentificacaoAnimalTests`

  Expected: FAIL because `TipoIdentificacaoAnimal` and `IdentificacaoAnimal` do not exist.

- [ ] **Step 3: Implement the minimum local rules**

  Give the entity a private EF constructor and `Guid.NewGuid()` creation. `Criar` applies `.Trim()` only, validates enum and length rules, persists `DescricaoTipo = null` outside Outro, sets `Ativo = true`, and accepts `Principal = true` only because a newly created row is active. `AlterarMetadados(bool hasDataAplicacao, DateOnly? dataAplicacao, bool hasObservacao, string? observacao, DateTimeOffset nowUtc)` changes only supplied metadata; `DefinirPrincipal` rejects inactive state; `Inativar` clears principal; `Reativar` never sets it.

- [ ] **Step 4: Run the Domain test green**

  Run the Step 2 command.

  Expected: PASS for all creation, normalization, immutability, principal, lifecycle, and timestamp assertions.

- [ ] **Step 5: Review checkpoint**

  Run: `git diff --check; git diff --stat; git status --short`

  Expected: only the three Task 1 paths changed; no commit or push.

### Task 2: Application contracts, errors, and DI

**Files:**
- Create: `src/Backend/GenSW.Application/Animals/Identificacoes/` containing the commands/results/query/page/service/repository contracts declared above, `IdentificacaoAnimalNotFoundException.cs`, `IdentificacaoAnimalDuplicateException.cs`, and `IdentificacaoAnimalPrincipalConflictException.cs`
- Create: `tests/GenSW.Application.Tests/IdentificacaoAnimalContractsTests.cs`
- Modify: `src/Backend/GenSW.Application/DependencyInjection.cs`

**Interfaces:**
- Consumes: Task 1 entity and enum; existing `AnimalNotFoundException`.
- Produces: exact cross-task contract names and scoped `IIdentificacaoAnimalService` registration for Tasks 3–10.

- [ ] **Step 1: Write failing contract tests**

  Construct every command, result, global result, page, and query. Assert defaults `page=1`, `pageSize=25`; each enum is distinct; metadata command preserves every absent/value/null combination through booleans; `IdentificacaoAnimalDuplicateException` carries `Tipo`, `DescricaoTipo`, `Valor`, and `IdentificacaoAnimalDuplicateConflictSource`; `IdentificacaoAnimalPrincipalConflictException` carries only structured persisted-constraint provenance.

- [ ] **Step 2: Run the red test**

  Run: `dotnet test tests/GenSW.Application.Tests/GenSW.Application.Tests.csproj --filter FullyQualifiedName~IdentificacaoAnimalContractsTests`

  Expected: FAIL because the identification Application contracts are absent.

- [ ] **Step 3: Implement contracts and narrow exception taxonomy**

  Define `IdentificacaoAnimalDuplicateConflictSource` as `PreCheck`, `PersistedNamedTipoValorUniqueConstraint`, and `PersistedNamedOutroDescricaoTipoValorUniqueConstraint`; define `IdentificacaoAnimalPrincipalConflictSource` as `PersistedNamedPrincipalAtivaUniqueConstraint`. Register `IIdentificacaoAnimalService` as scoped using the concrete repository and `TimeProvider`; do not register a generic repository.

- [ ] **Step 4: Run the contract test green**

  Run the Step 2 command.

  Expected: PASS and compilation of the exact inter-task contracts.

- [ ] **Step 5: Review checkpoint**

  Run: `git diff --check; git diff --stat; git status --short`

  Expected: only Task 2 contracts, exception classes, test, and DI registration are changed.

### Task 3: Application service rules and nested scope

**Files:**
- Create: `src/Backend/GenSW.Application/Animals/Identificacoes/IdentificacaoAnimalService.cs`
- Create: `tests/GenSW.Application.Tests/IdentificacaoAnimalServiceTests.cs`

**Interfaces:**
- Consumes: Tasks 1–2 and the `IIdentificacaoAnimalRepository` contract.
- Produces: behavior for Tasks 6, 8, and 9.

- [ ] **Step 1: Write failing service tests using a focused fake repository**

  Cover missing Animal; missing row; row belonging to another Animal; duplicate non-Outro and Outro pre-checks; creation with and without principal; replacement of an active principal; removal of principal; refusal to make inactive row principal; inactivation clearing principal; reactivation without promotion; partial metadata update with absent/value/null per property; invalid page/size/enum; nested and global query delegation.

- [ ] **Step 2: Run the red test**

  Run: `dotnet test tests/GenSW.Application.Tests/GenSW.Application.Tests.csproj --filter FullyQualifiedName~IdentificacaoAnimalServiceTests`

  Expected: FAIL because the service implementation is absent.

- [ ] **Step 3: Implement ordered Application behavior**

  For create, pre-check the normalized domain values then, if `Principal`, open mutation scope, lock parent, create, clear prior principal, save, commit. For set-principal and deactivate, open scope, lock parent first, locate by both IDs, mutate, save, commit. For reactivation and metadata, verify parent/row scope and do not set principal. `GetByAnimalAsync` and nested list first confirm Animal existence so an empty unknown Animal is 404; global list never requires AnimalId. Reject `HasDataAplicacao == false && HasObservacao == false` as 400 input. Do not translate generic database errors here.

- [ ] **Step 4: Run the service test green**

  Run the Step 2 command.

  Expected: PASS, including no implicit activation or promotion.

- [ ] **Step 5: Review checkpoint**

  Run: `git diff --check; git diff --stat; git status --short`

  Expected: only the service and Task 3 test changed after Task 2.

### Task 4: EF mapping and additive migration

**Files:**
- Modify: `src/Backend/GenSW.Infrastructure/Persistence/GenSWDbContext.cs`
- Create: one EF-generated `src/Backend/GenSW.Infrastructure/Persistence/Migrations/<MigrationId>_AddIdentificacoesAnimal.cs`
- Create: matching `<MigrationId>_AddIdentificacoesAnimal.Designer.cs`
- Modify: `src/Backend/GenSW.Infrastructure/Persistence/Migrations/GenSWDbContextModelSnapshot.cs`
- Create: `tests/GenSW.Infrastructure.Tests/IdentificacaoAnimalPersistenceModelTests.cs`
- Create: `tests/GenSW.Infrastructure.Tests/IdentificacaoAnimalMigrationTests.cs`

**Interfaces:**
- Consumes: Task 1 entity/enum and existing DbContext mappings.
- Produces: `DbSet<IdentificacaoAnimal> IdentificacoesAnimal` and the exact named database artifacts for Tasks 5–8.

- [ ] **Step 1: Write failing model and migration tests**

  Assert table `IdentificacoesAnimal`, `uuid` primary/FK columns, `date` DataAplicacao, integer enum conversion, required booleans/timestamps, 128/100/1,000 lengths, `DeleteBehavior.Restrict`, all named checks/indexes, and that migration Up creates and Down drops each artifact without touching historical migrations.

- [ ] **Step 2: Run the red tests**

  Run: `dotnet test tests/GenSW.Infrastructure.Tests/GenSW.Infrastructure.Tests.csproj --filter "FullyQualifiedName~IdentificacaoAnimalPersistenceModelTests|FullyQualifiedName~IdentificacaoAnimalMigrationTests"`

  Expected: FAIL because the DbSet and migration do not exist.

- [ ] **Step 3: Implement mapping, generate migration, and inspect generated SQL**

  Map FK `AnimalId` to `Animais(Id)` with `Restrict`; use `HasConversion<int>()`. Add named checks: `CK_IdentificacoesAnimal_Tipo` for `(1,2,3,4,5,6)`; `CK_IdentificacoesAnimal_Valor_Canonical` requiring nonempty and `Valor = btrim(Valor)` under Npgsql (the SQLite fallback uses `trim`); and `CK_IdentificacoesAnimal_DescricaoTipo_Semantics` requiring `DescricaoTipo IS NULL` for types 1–5 and nonempty/btrim-canonical text for type 6. Generate the migration with `dotnet ef migrations add AddIdentificacoesAnimal --project src/Backend/GenSW.Infrastructure --startup-project src/Backend/GenSW.API`; preserve generated migration naming.

  In migration `Up`, create expression indexes via `migrationBuilder.Sql`: `UX_IdentificacoesAnimal_Tipo_Valor_CaseInsensitive` on `("Tipo", lower("Valor")) WHERE "Tipo" <> 6`; `UX_IdentificacoesAnimal_Outro_DescricaoTipo_Valor_CaseInsensitive` on `("Tipo", lower("DescricaoTipo"), lower("Valor")) WHERE "Tipo" = 6`; and `UX_IdentificacoesAnimal_Animal_PrincipalAtiva` on `("AnimalId") WHERE "Ativo" AND "Principal"`. In `Down`, execute exact `DROP INDEX IF EXISTS` statements before dropping the table. Preserve historical uniqueness even when `Ativo=false` by deliberately excluding `Ativo` from the first two predicates.

- [ ] **Step 4: Run the mapping/migration test green**

  Run the Step 2 command.

  Expected: PASS; migration Up and Down prove reversibility and artifact names.

- [ ] **Step 5: Review checkpoint**

  Run: `git diff --check; git diff -- src/Backend/GenSW.Infrastructure/Persistence/Migrations; git status --short`

  Expected: one new migration pair, changed snapshot, mapping/tests; no historical migration edit.

### Task 5: PostgreSQL repository, read models, and named-error conversion

**Files:**
- Create: `src/Backend/GenSW.Infrastructure/Animals/Identificacoes/IdentificacaoAnimalRepository.cs`
- Modify: `src/Backend/GenSW.Infrastructure/DependencyInjection.cs`
- Create: `tests/GenSW.Infrastructure.Tests/IdentificacaoAnimalRepositoryTests.cs`

**Interfaces:**
- Consumes: Tasks 2–4, `GenSWDbContext`, `Npgsql.PostgresException`, and existing `EF.Functions.ILike`/escaping pattern.
- Produces: concrete repository, transaction scope, local/global projections, and structured constraint translation for Tasks 6–8.

- [ ] **Step 1: Write failing repository tests**

  Against the repository's existing test database pattern, cover case-insensitive exact pre-check without wildcard leakage, local/global `Valor` filters with escaped `%`, boolean/type filters, deterministic pagination, animal summary projection, lookup by `(AnimalId, Id)`, `FOR UPDATE` parent lock, and conversions for precisely the three named indexes.

- [ ] **Step 2: Run the red test**

  Run: `dotnet test tests/GenSW.Infrastructure.Tests/GenSW.Infrastructure.Tests.csproj --filter FullyQualifiedName~IdentificacaoAnimalRepositoryTests`

  Expected: FAIL because the identification repository is absent.

- [ ] **Step 3: Implement only the concrete repository**

  Use `ILike` plus the Animal repository's `EscapeLikePattern` algorithm for `Valor`; equality uniqueness checks use `ILike(value, escapedValue, "\\")`. Global reads join `Animais` and project `Id`, `CodigoInterno`, and `Nome` only. Order local/global pages by `CreatedAtUtc` descending then `Id` descending. `SaveChangesAsync` catches only `DbUpdateException` whose inner `PostgresException` is `UniqueViolation` and whose `ConstraintName` equals one of the three exact index constants; map duplicate indexes to `IdentificacaoAnimalDuplicateException`, principal index to `IdentificacaoAnimalPrincipalConflictException`; let all other exceptions escape.

- [ ] **Step 4: Run the repository test green**

  Run the Step 2 command.

  Expected: PASS; projections are read-only and unknown database errors are not reclassified.

- [ ] **Step 5: Review checkpoint**

  Run: `git diff --check; git diff --stat; git status --short`

  Expected: only repository/DI/test files changed beyond the mapping artifacts.

### Task 6: API contracts and explicit PATCH presence tracking

**Files:**
- Create: `src/Backend/GenSW.API/Contracts/AnimalIdentifications/CreateIdentificacaoAnimalRequest.cs`
- Create: `src/Backend/GenSW.API/Contracts/AnimalIdentifications/UpdateIdentificacaoAnimalMetadataRequest.cs`
- Create: `src/Backend/GenSW.API/Contracts/AnimalIdentifications/UpdateIdentificacaoAnimalAtivoRequest.cs`
- Create: `src/Backend/GenSW.API/Contracts/AnimalIdentifications/UpdateIdentificacaoAnimalPrincipalRequest.cs`
- Create: `src/Backend/GenSW.API/Contracts/AnimalIdentifications/IdentificacaoAnimalResponse.cs`
- Create: `src/Backend/GenSW.API/Contracts/AnimalIdentifications/IdentificacoesAnimalListResponse.cs`
- Create: `src/Backend/GenSW.API/Contracts/AnimalIdentifications/IdentificacaoAnimalGlobalResponse.cs`
- Create: `tests/GenSW.API.Tests/IdentificacoesAnimaisContractTests.cs`

**Interfaces:**
- Consumes: Task 2 commands/results.
- Produces: stable JSON shapes and tri-state request semantics for Task 9 and frontend Tasks 12–15.

- [ ] **Step 1: Write failing JSON contract tests**

  Deserialize `{}`, `{"dataAplicacao":"2026-09-12"}`, `{"dataAplicacao":null}`, and corresponding `observacao` payloads. Assert `HasDataAplicacao`/`HasObservacao` are false only when omitted and true when null/value appears. Assert result and global response field names/casing, page metadata, Animal summary shape, and integer enum serialisation.

- [ ] **Step 2: Run the red test**

  Run: `dotnet test tests/GenSW.API.Tests/GenSW.API.Tests.csproj --filter FullyQualifiedName~IdentificacoesAnimaisContractTests`

  Expected: FAIL because the API contract folder is absent.

- [ ] **Step 3: Implement local tri-state DTOs**

  Make `UpdateIdentificacaoAnimalMetadataRequest` a sealed mutable class, not a nullable positional record. Each nullable property setter sets its paired `[JsonIgnore]` presence flag: `DateOnly? DataAplicacao` sets `HasDataAplicacao`; `string? Observacao` sets `HasObservacao`. Its default constructor leaves both false. The controller converts all four values to `UpdateIdentificacaoAnimalMetadataCommand`; this is the smallest feature-local representation that distinguishes missing and explicit null.

- [ ] **Step 4: Run the contract test green**

  Run the Step 2 command.

  Expected: PASS for missing, value, and explicit-null JSON.

- [ ] **Step 5: Review checkpoint**

  Run: `git diff --check; git diff --stat; git status --short`

  Expected: only feature-local API DTOs and contract tests changed.

### Task 7: PostgreSQL schema, constraint, and historical-uniqueness gate

**Files:**
- Modify: `tests/GenSW.Infrastructure.Tests/IdentificacaoAnimalMigrationTests.cs`
- Modify: `tests/GenSW.Infrastructure.Tests/IdentificacaoAnimalRepositoryTests.cs`

**Interfaces:**
- Consumes: Tasks 4–5 migration and repository.
- Produces: real PostgreSQL proof for persistence requirements before API work.

- [ ] **Step 1: Add focused real-PostgreSQL tests**

  Use direct Npgsql/EF SQL under the existing real harness to assert FK violation and `Restrict`; each named check; duplicate non-Outro across different Animals; duplicate Outro keyed by type/description/value; case-only duplicates; duplicate reuse after inactivation; non-conflicting same value when the `Outro` description differs; and second active principal failing with the named partial index.

- [ ] **Step 2: Run the gate**

  Run: `dotnet test tests/GenSW.Infrastructure.Tests/GenSW.Infrastructure.Tests.csproj --filter "FullyQualifiedName~IdentificacaoAnimalMigrationTests|FullyQualifiedName~IdentificacaoAnimalRepositoryTests"`

  Expected: PASS after Tasks 4–5; if PostgreSQL executables are unavailable, report the existing harness skip rather than substituting SQLite.

- [ ] **Step 3: Repair only named owner artifacts on a verified failure**

  Correct only Task 4 mapping/migration SQL or Task 5 repository conversion, rerun its focused test, then rerun Step 2. Preserve the exact approved constraint/index names.

- [ ] **Step 4: Capture the green gate**

  Run: `git diff --check; git status --short`

  Expected: schema evidence remains limited to the Task 4/5 owners and their tests.

- [ ] **Step 5: Review checkpoint**

  Inspect `git diff -- src/Backend/GenSW.Infrastructure/Persistence/Migrations`.

  Expected: Up/Down contain only additive identification artifacts.

### Task 8: PostgreSQL concurrency gate A–D

**Files:**
- Create: `tests/GenSW.API.Tests/PostgreSqlIdentificacoesAnimalConcurrencyTests.cs`

**Interfaces:**
- Consumes: Tasks 3–5 and existing `EphemeralPostgreSql`/`AnimalApiPostgreSqlFixture` support.
- Produces: real-database evidence of serialized per-Animal principal decisions and deterministic conflicts.

- [ ] **Step 1: Write failing concurrent tests**

  Coordinate independent service scopes with barriers: A, concurrent `SetPrincipalAsync` for different rows of one Animal ends with exactly one active principal; B, concurrent creation of the same normalized marker produces one success and one structured duplicate conflict; C, hold a lock for Animal A and prove Animal B principal mutation completes before A is released; D, concurrent deactivate/reactivate/define-principal sequences never persist `Ativo=false && Principal=true` and defining a still-inactive row returns invalid input.

- [ ] **Step 2: Run the red test**

  Run: `dotnet test tests/GenSW.API.Tests/GenSW.API.Tests.csproj --filter FullyQualifiedName~PostgreSqlIdentificacoesAnimalConcurrencyTests`

  Expected: FAIL before Tasks 3–5 complete because transaction scope and marker operations do not exist.

- [ ] **Step 3: Implement only verified concurrency gaps**

  Use the Task 5 transaction scope and exact parent-row `FOR UPDATE` lock. Never replace this with a pre-check, process lock, SQLite test, or broad global advisory lock. Let `UX_IdentificacoesAnimal_Animal_PrincipalAtiva` defend an unforeseen interleaving.

- [ ] **Step 4: Run the concurrency test green**

  Run the Step 2 command.

  Expected: PASS on real PostgreSQL; unavailable binaries yield the pre-existing explicit skip only.

- [ ] **Step 5: Review checkpoint**

  Run: `git diff --check; git diff --stat; git status --short`

  Expected: concurrency evidence is isolated to the new PostgreSQL test plus necessary owner fixes.

### Task 9: Authenticated nested mutation and local-list API

**Files:**
- Create: `src/Backend/GenSW.API/Controllers/IdentificacoesAnimaisController.cs`
- Create: `tests/GenSW.API.Tests/IdentificacoesAnimaisApiTests.cs`

**Interfaces:**
- Consumes: Tasks 2, 3, and 6.
- Produces: authenticated `/api/v1/animais/{animalId}/identificacoes` API for Tasks 12–15.

- [ ] **Step 1: Write failing API tests**

  Cover unauthenticated 401; POST 201 and `CreatedAtAction`; GET list/get 200; PATCH metadata/active/principal 200; 400 malformed enum, empty identifier, no metadata fields, inactive-principal; 404 missing Animal/missing nested row/row of another Animal; 409 duplicate/principal; list filters and pagination; and `DELETE` returning 405.

- [ ] **Step 2: Run the red test**

  Run: `dotnet test tests/GenSW.API.Tests/GenSW.API.Tests.csproj --filter FullyQualifiedName~IdentificacoesAnimaisApiTests`

  Expected: FAIL because the nested controller is absent.

- [ ] **Step 3: Implement the controller with stable errors**

  Apply `[ApiController]`, `[Authorize]`, and route `api/v1/animais/{animalId:guid}/identificacoes`. Implement exactly POST `/`, GET `/`, GET `/{identificacaoId:guid}`, PATCH `/{identificacaoId:guid}`, PATCH `/{identificacaoId:guid}/ativo`, and PATCH `/{identificacaoId:guid}/principal`. Map `AnimalNotFoundException` and `IdentificacaoAnimalNotFoundException` to empty 404; duplicate/principal exceptions to 409 `ProblemDetails` without database text; `ArgumentException` to safe 400 `ProblemDetails`. Do not add a DELETE/PUT action.

- [ ] **Step 4: Run the API test green**

  Run the Step 2 command.

  Expected: PASS for every authenticated nested route and status boundary.

- [ ] **Step 5: Review checkpoint**

  Run: `git diff --check; git diff --stat; git status --short`

  Expected: only nested API/controller/test paths changed; no global mutation route exists.

### Task 10: Authenticated global lookup API

**Files:**
- Create: `src/Backend/GenSW.API/Controllers/IdentificacoesAnimalController.cs`
- Modify: `tests/GenSW.API.Tests/IdentificacoesAnimaisApiTests.cs`

**Interfaces:**
- Consumes: Task 3 `ListGlobalAsync`, Task 6 global response, and Task 5 projection.
- Produces: GET-only `/api/v1/identificacoes-animal` contract for frontend client and lookup consumers.

- [ ] **Step 1: Write failing endpoint tests**

  Assert 401 unauthenticated; 200 paginated result with identifier plus only `id`, `codigoInterno`, `nome` Animal summary; case-insensitive value search; all `tipo`, `valor`, `ativo`, `principal`, `page`, `pageSize` filters; invalid enum/page returns 400; and POST/PATCH/PUT/DELETE return 405.

- [ ] **Step 2: Run the red test**

  Run: `dotnet test tests/GenSW.API.Tests/GenSW.API.Tests.csproj --filter FullyQualifiedName~IdentificacoesAnimaisApiTests`

  Expected: FAIL because the global lookup controller is absent.

- [ ] **Step 3: Implement GET-only global controller**

  Add `[Authorize]`, `[Route("api/v1/identificacoes-animal")]`, and only `[HttpGet]`. Parse numeric enum/query values using the same controller validation style as `AnimaisController`; map result to `IdentificacaoAnimalGlobalResponse`. Do not accept `animalId` and do not expose a mutable global route.

- [ ] **Step 4: Run the global API test green**

  Run the Step 2 command.

  Expected: PASS, with global discovery remaining read-only.

- [ ] **Step 5: Review checkpoint**

  Run: `git diff --check; git diff --stat; git status --short`

  Expected: GET is the sole action in the global controller.

### Task 11: API safety and scope regression gate

**Files:**
- Modify: `tests/GenSW.API.Tests/AnimaisApiTests.cs`
- Modify: `tests/GenSW.API.Tests/EspeciesApiTests.cs`
- Modify: `tests/GenSW.API.Tests/RacasApiTests.cs`
- Modify: `tests/GenSW.API.Tests/VariedadesApiTests.cs`

**Interfaces:**
- Consumes: Tasks 4, 9, and 10.
- Produces: evidence that Animal, Especie, Raca, and Variedade behavior remains stable.

- [ ] **Step 1: Add regression assertions**

  Confirm Animal base creation/edit/lifecycle remains independent of any identifiers; Especie/Raca/Variedade pagination/lifecycle routes keep their existing behavior; and identifier FK restriction does not add unexpected changes to classification endpoints. Keep this task tests-only unless an identified owner defect requires a minimal fix.

- [ ] **Step 2: Run the focused regression gate**

  Run: `dotnet test tests/GenSW.API.Tests/GenSW.API.Tests.csproj --filter "FullyQualifiedName~AnimaisApiTests|FullyQualifiedName~EspeciesApiTests|FullyQualifiedName~RacasApiTests|FullyQualifiedName~VariedadesApiTests"`

  Expected: PASS; newly written regression tests may already be green.

- [ ] **Step 3: Correct only the responsible feature on a failure**

  Return a reproduced failure to its owning Task 4, 9, or 10 implementation; do not alter unrelated Species, Breed, or Variety design.

- [ ] **Step 4: Re-run the focused regression gate**

  Run the Step 2 command.

  Expected: PASS for all four existing modules.

- [ ] **Step 5: Review checkpoint**

  Run: `git diff --check; git status --short`

  Expected: every non-identification touch is a focused regression assertion.

### Task 12: Frontend types, strict parsers, and authenticated client

**Files:**
- Create: `src/Frontend/GenSW.Web/src/features/animals/identifications/types.ts`
- Create: `src/Frontend/GenSW.Web/src/features/animals/identifications/services/identificationsContractParsers.ts`
- Create: `src/Frontend/GenSW.Web/src/features/animals/identifications/services/identificationsService.ts`
- Create: matching `.test.ts` files

**Interfaces:**
- Consumes: Tasks 6, 9, and 10 JSON contracts; existing `httpRequest` and `InvalidApiResponseError` patterns.
- Produces: exact typed functions `listAnimalIdentifications`, `getAnimalIdentification`, `createAnimalIdentification`, `updateAnimalIdentificationMetadata`, `setAnimalIdentificationAtivo`, `setAnimalIdentificationPrincipal`, and `listGlobalAnimalIdentifications` for Tasks 13–15.

- [ ] **Step 1: Write failing client/parser tests**

  Parse all enum values, nullable data/observation/description, ISO timestamps, page metadata, and global Animal summary; reject malformed responses. Assert authenticated paths/methods/body for every nested route and GET-only global path. Assert omitted metadata properties remain omitted while explicit `null` survives JSON serialisation.

- [ ] **Step 2: Run the red test**

  Run: `npm --prefix src/Frontend/GenSW.Web test -- src/features/animals/identifications/services`

  Expected: FAIL because the identification frontend module is absent.

- [ ] **Step 3: Implement strict feature-local client**

  Use endpoint base `/animais/${animalId}/identificacoes` and global `/identificacoes-animal`; all calls use `authenticated: true`. Build `URLSearchParams` only for supplied query members. Preserve an update request object as passed, so JavaScript omits `undefined` and transmits `null`; do not normalize identifier values in the client.

- [ ] **Step 4: Run the frontend client test green**

  Run the Step 2 command.

  Expected: PASS for strict parsing, path scope, and tri-state serialization.

- [ ] **Step 5: Review checkpoint**

  Run: `git diff --check; git diff --stat; git status --short`

  Expected: only feature-local types, client, parser, and tests changed; no dependency added.

### Task 13: Animal edit identifiers panel

**Files:**
- Create: `src/Frontend/GenSW.Web/src/features/animals/identifications/components/AnimalIdentificationsPanel.tsx`
- Create: `src/Frontend/GenSW.Web/src/features/animals/identifications/components/AnimalIdentificationsPanel.test.tsx`
- Modify: `src/Frontend/GenSW.Web/src/features/animals/pages/AnimalFormPage.tsx`
- Modify: `src/Frontend/GenSW.Web/src/features/animals/pages/AnimalFormPage.test.tsx`

**Interfaces:**
- Consumes: Task 12 typed client and `Animal.id` from the existing edit page.
- Produces: edit-only visual and semantic identifier management.

- [ ] **Step 1: Write failing panel/page tests**

  Assert no panel on `/animais/nova`; an edit page renders the heading `Identificações físicas` separate from `Código interno`; loading, retryable load error, empty state, history/inactive label, and principal highlight; create Anilha/Microchip/Outro with conditional description; active-only principal set/remove; inactivate/reactivate; metadata-only edit; request error text for 400/404/409 without technical details; optimistic state is not assumed and successful mutation refreshes/list state.

- [ ] **Step 2: Run the red test**

  Run: `npm --prefix src/Frontend/GenSW.Web test -- src/features/animals/identifications/components/AnimalIdentificationsPanel.test.tsx src/features/animals/pages/AnimalFormPage.test.tsx`

  Expected: FAIL because the panel and edit integration do not exist.

- [ ] **Step 3: Implement focused panel behavior**

  Render the panel only when `id` and loaded `currentAnimal` exist. Keep its state local: `loadState`, retry key, page, modal/inline form values, selected row, and pending mutation ID. The create form offers six types, shows required `DescricaoTipo` only for Outro, and never offers controls to edit Tipo/DescricaoTipo/Valor after creation. List inactive records as history; make principal action unavailable for inactive rows; expose explicit removal of principal, no DELETE control, and page controls when `totalPages > 1`.

- [ ] **Step 4: Run the panel test green**

  Run the Step 2 command.

  Expected: PASS for loading/error/empty/history/mutations and creation-page absence.

- [ ] **Step 5: Review checkpoint**

  Run: `git diff --check; git diff --stat; git status --short`

  Expected: panel changes do not change Animal base field semantics or introduce a generic form abstraction.

### Task 14: Frontend metadata tri-state and prohibited-control gate

**Files:**
- Modify: `src/Frontend/GenSW.Web/src/features/animals/identifications/components/AnimalIdentificationsPanel.test.tsx`
- Modify: `src/Frontend/GenSW.Web/src/features/animals/identifications/services/identificationsService.test.ts`

**Interfaces:**
- Consumes: Tasks 6 and 12–13.
- Produces: evidence that UI metadata PATCH preserves absent/value/null meaning and immutable fields remain absent.

- [ ] **Step 1: Add failing interaction tests**

  For a selected record, submit data/observation unchanged and assert absent keys; set each field and assert replacement; clear each field and assert explicit `null`; verify historical record remains listed after reactivation; assert no edit input or PATCH body member for Tipo, DescricaoTipo, or Valor; assert no delete control or service function is reachable.

- [ ] **Step 2: Run the red test**

  Run: `npm --prefix src/Frontend/GenSW.Web test -- src/features/animals/identifications/components/AnimalIdentificationsPanel.test.tsx src/features/animals/identifications/services/identificationsService.test.ts`

  Expected: FAIL until the Task 13 form deliberately omits untouched keys and emits `null` on clear.

- [ ] **Step 3: Implement only request construction required by the tests**

  Track edit-field dirty state independently from displayed values. Build `{}` for an unchanged field, `{ dataAplicacao: value }` or `{ observacao: value }` for a value, and `{ dataAplicacao: null }` or `{ observacao: null }` for an explicit clear. Keep immutable marker identity display-only.

- [ ] **Step 4: Run the tri-state gate green**

  Run the Step 2 command.

  Expected: PASS for each absent/value/null state and prohibited control assertion.

- [ ] **Step 5: Review checkpoint**

  Run: `git diff --check; git diff --stat; git status --short`

  Expected: no generic optional-state library or form framework added.

### Task 15: Frontend integrated identifiers gate

**Files:**
- Modify: relevant Task 12–14 frontend tests only when a missing integration case is proven.

**Interfaces:**
- Consumes: Tasks 12–14.
- Produces: a coherent Animal edit UI with safe client behavior.

- [ ] **Step 1: Complete end-to-end mocked UI coverage**

  In one panel workflow cover Anilha, Microchip, Outro required description, duplicate conflict message, history after inactivation, principal switch/removal, invalid inactive-principal feedback, pagination, retry, and API refresh after every successful mutation.

- [ ] **Step 2: Run the frontend identifiers gate**

  Run: `npm --prefix src/Frontend/GenSW.Web test -- src/features/animals/identifications src/features/animals/pages/AnimalFormPage.test.tsx`

  Expected: PASS; test a user-visible behavior rather than implementation-state internals.

- [ ] **Step 3: Repair only the failing frontend owner**

  Return parser/client failures to Task 12 and panel/form failures to Tasks 13–14. Preserve existing Animal page catalog-loading and save behavior.

- [ ] **Step 4: Re-run the frontend identifiers gate**

  Run the Step 2 command.

  Expected: PASS with no TypeScript diagnostic.

- [ ] **Step 5: Review checkpoint**

  Run: `git diff --check; git status --short`

  Expected: no unrelated routes, package files, or shared HTTP semantics changed.

### Task 16: Focused Domain/Application/Infrastructure regression gate

**Files:**
- Modify: relevant Task 1–8 tests only when an explicitly identified gap exists.

**Interfaces:**
- Consumes: Domain, Application, persistence, and concurrency work.
- Produces: targeted regression proof for Animal, Especie, Raca, and Variedade.

- [ ] **Step 1: Run focused backend suites**

  Run: `dotnet test tests/GenSW.Domain.Tests/GenSW.Domain.Tests.csproj --filter "FullyQualifiedName~AnimalTests|FullyQualifiedName~IdentificacaoAnimalTests"; dotnet test tests/GenSW.Application.Tests/GenSW.Application.Tests.csproj --filter "FullyQualifiedName~AnimalServiceTests|FullyQualifiedName~IdentificacaoAnimal"; dotnet test tests/GenSW.Infrastructure.Tests/GenSW.Infrastructure.Tests.csproj --filter "FullyQualifiedName~Animal|FullyQualifiedName~IdentificacaoAnimal"; dotnet test tests/GenSW.API.Tests/GenSW.API.Tests.csproj --filter "FullyQualifiedName~PostgreSqlAnimalConcurrencyTests|FullyQualifiedName~PostgreSqlIdentificacoesAnimalConcurrencyTests"`

  Expected: PASS; real PostgreSQL suites can show the pre-existing explicit skip only when binaries are unavailable.

- [ ] **Step 2: Diagnose any failure at its owner**

  Reproduce with the narrowest test class, correct only its owning Task 1–10 behavior, then rerun the failed focused test before Step 1.

- [ ] **Step 3: Confirm regression boundaries**

  Run: `dotnet test tests/GenSW.Application.Tests/GenSW.Application.Tests.csproj --filter "FullyQualifiedName~EspecieServiceTests|FullyQualifiedName~RacaServiceTests|FullyQualifiedName~VariedadeServiceTests"`

  Expected: PASS; no classification contract change is introduced by NA-04.

- [ ] **Step 4: Re-run the focused backend suites**

  Run the Step 1 command.

  Expected: PASS with both legacy Animal and new marker evidence.

- [ ] **Step 5: Review checkpoint**

  Run: `git diff --check; git status --short`

  Expected: all changes have a feature owner or focused regression justification.

### Task 17: Integrated final gate, documentation evidence, commit, push, and handoff

**Files:**
- Create: no source file.
- Modify: only the owner of a verified gate failure.
- Test: solution, frontend suite, real PostgreSQL suites, migration artifacts, and scope.

**Interfaces:**
- Consumes: Tasks 1–16.
- Produces: one implementation commit, remote branch, PR/CI handoff, and Redmine evidence while keeping #296 in `Em validação`.

- [ ] **Step 1: Run complete backend and PostgreSQL checks**

  Run: `dotnet test GenSW.sln; dotnet test tests/GenSW.Infrastructure.Tests/GenSW.Infrastructure.Tests.csproj --filter "FullyQualifiedName~IdentificacaoAnimal"; dotnet test tests/GenSW.API.Tests/GenSW.API.Tests.csproj --filter "FullyQualifiedName~PostgreSqlIdentificacoesAnimalConcurrencyTests|FullyQualifiedName~IdentificacoesAnimaisApiTests"`

  Expected: all runnable tests pass; document a harness skip only when `EphemeralPostgreSql` reports unavailable PostgreSQL executables.

- [ ] **Step 2: Run complete frontend checks**

  Run: `npm --prefix src/Frontend/GenSW.Web test; npm --prefix src/Frontend/GenSW.Web run lint; npm --prefix src/Frontend/GenSW.Web run build`

  Expected: all commands exit 0.

- [ ] **Step 3: Inspect diff, migration, and scope**

  Run: `$base = git merge-base main HEAD; git diff --check "$base...HEAD"; git diff --name-only "$base...HEAD"; git diff "$base...HEAD" -- src/Backend/GenSW.Infrastructure/Persistence/Migrations; git status --short`

  Expected: one additive identification migration pair and snapshot update; no historical migration change; no DELETE, #297, Registro Institucional, RFID, QR Code, reader, marker inventory, ownership/location/lot, pedigree, or crossing implementation.

- [ ] **Step 4: Create the single implementation commit**

  Run: `git add src/Backend tests src/Frontend/GenSW.Web/src; git commit -m "feat: add animal physical identifiers"`

  Expected: one commit on `feature/296-identificacoes-animal`, after every prior command is green. Do not amend the plan commit, merge, or force-push.

- [ ] **Step 5: Push and create human-validation handoff**

  Run: `git push origin feature/296-identificacoes-animal`

  Expected: remote branch updated. Open a PR, wait for CI, append objective evidence and commit/PR reference to Redmine #296, retain `Em validação`, and leave human homologation pending.

## SPEC to Tasks matrix

| SPEC section | Requirement carried into the plan | Task(s) |
| --- | --- | --- |
| Objective and Decision | Separate 0..N physical resource; technical and operational identities unchanged | 1, 2, 3, 4, 9, 13, 17 |
| Domain model and normalization | Fields, enum, trim-only, Outro semantics, immutable identity, lifecycle | 1, 2, 3, 4, 12, 14 |
| Persistence and concurrency | Table, FK/Restrict, checks, named expression/partial indexes, transaction/parent lock | 4, 5, 7, 8, 17 |
| Application and API | Nested authenticated routes, 400/404/409, no destructive route | 2, 3, 6, 9, 11 |
| Global lookup | GET-only search, filters, pagination, minimal Animal summary | 3, 5, 6, 10, 12 |
| PATCH semantics | independent missing/value/null DataAplicacao and Observacao | 1, 2, 3, 6, 9, 12, 14 |
| Interface | edit-only panel, history, principal/lifecycle actions, safe messages | 12, 13, 14, 15 |
| Tests and validation | Domain, Application, PostgreSQL, API, frontend, gates | 1–17 |
| Outside scope | exclude #297, #298, #299 and all deferred feature areas | 9, 10, 14, 17 |
| Acceptance criteria | mixed active/history example, global discovery, tri-state, integrity | 3, 7, 8, 9, 10, 13–17 |

## Human homologation after PR and CI

The developer, not Codex, performs these checks after the implementation PR is available. Automated real-PostgreSQL tests remain the primary evidence for concurrency.

- H01: Open an Animal with no markers and confirm the empty `Identificações físicas` state.
- H02: Add an Anilha with only required fields.
- H03: Add a Microchip and confirm both active markers remain visible.
- H04: Add `Outro` with a description and confirm the description is shown.
- H05: Submit `Outro` without a description and confirm a clear validation error.
- H06: Attempt same Anilha with casing changed and confirm a friendly duplicate conflict.
- H07: Inactivate an identifier and confirm its historical/inactive label remains visible.
- H08: Attempt to reuse that inactive marker and confirm historical uniqueness blocks it.
- H09: Mark an active identifier principal and confirm its highlight.
- H10: Change principal to another active identifier and confirm only the latter is highlighted.
- H11: Remove principal and confirm none is promoted.
- H12: Inactivate a principal and confirm it becomes inactive and non-principal.
- H13: Reactivate it and confirm it remains non-principal.
- H14: Try to make an inactive marker principal and confirm the request is rejected.
- H15: Edit DataAplicacao to a calendar date.
- H16: Clear DataAplicacao and confirm explicit null removal persists after reload.
- H17: Edit Observacao to text.
- H18: Clear Observacao and confirm explicit null removal persists after reload.
- H19: Confirm Tipo, DescricaoTipo, and Valor have no post-creation editing control.
- H20: List the Animal's markers and confirm its scoped history/list filters.
- H21: Use global lookup by marker value and confirm minimal Animal summary appears.
- H22: Exercise global `tipo`, `valor`, `ativo`, and `principal` filters.
- H23: Exercise global page navigation and page size behavior.
- H24: Observe clear 400, 404, and 409 messages with no database diagnostics.
- H25: Confirm neither nested nor global UI/API exposes delete behavior.
- H26: Create/edit the base Animal fields and confirm the identifier panel remains separate from Código interno.
- H27: Recheck existing Especie, Raca, and Variedade create/list/lifecycle flows.

## Plan self-review record

- **SPEC coverage:** PASS. Every SPEC section maps to one or more owner Tasks in the matrix; persistence, concurrency, lookup, tri-state, UI, and exclusions each have explicit owners.
- **Placeholder scan:** PASS. No unresolved marker, vague validation instruction, or delegated technical decision remains in task steps.
- **Interface consistency:** PASS. Tasks 2 and 6 declare the C# and JSON contracts consumed by all later tasks; named constraints and error classes retain one spelling throughout.
- **Scope:** PASS. #297, #298, and #299 are not implemented by this plan; no institutional registration or deferred marker technology is introduced.
- **Commit policy:** PASS. Task checkpoints produce no production commits; Task 17 creates exactly one implementation commit after integrated gates, then push/PR/CI/human validation.
