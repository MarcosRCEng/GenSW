# Animal Base Implementation Plan

> **For agentic workers:** execute one checked task at a time, using TDD and an ephemeral review checkpoint after each task. This plan is a design artifact only; it does not authorize implementation before human review.

**Goal:** Deliver the NA-03 Animal base aggregate with safe operational-code allocation, classifications, lifecycle, PostgreSQL integrity, authenticated API, React UI, and complete regression coverage.

**Architecture:** Animal is a dedicated vertical module. The Domain owns only local invariants; the Application validates Species, Breed, and Variety relationships; EF Core and PostgreSQL enforce structural compatibility with composite foreign keys. Automatic code creation uses one new PostgreSQL transaction per candidate through a scoped allocator and retries only a persisted, named `CodigoInterno` constraint violation with structured provenance that matches the candidate of the current automatic attempt.

**Tech Stack:** .NET 8/C#; EF Core 8.0.10; Npgsql/PostgreSQL; ASP.NET Core controllers and ProblemDetails; xUnit; React 18; TypeScript; React Router; Vitest; Testing Library; Vite; ESLint.

**Spec:** docs/superpowers/specs/2026-09-03-animal-base-design.md

## Global Constraints

- The SPEC above is the behavior authority. Read it with this plan before executing any task.
- Work only on feature/295-animal-base, whose approved SPEC tip is 872a9a6db39e887207a5636daee8de9a7bd0b13e. Rebase it only after an explicit new-base decision.
- Preserve the existing vertical modules and concrete repositories. Do not add IRepository<T>, MediatR, CQRS, a generic classification abstraction, an EAV model, a form/state library, or a package dependency.
- Animal has no Domain navigation to Especie, Raca, or Variedade. Relationship state and active-destination rules belong to Application; the database supplies the final structural defense.
- Do not modify historical migrations. The Animal migration is additive, and its Down path must remove Animal artifacts and the two new alternate keys completely.
- Do not create a DELETE route, physical deletion flow, StatusAnimal, or any implementation from #296, #297, #298, or #299.
- Automatic CodigoInterno allocation is PostgreSQL-only: bigint sequence, CACHE 1, NO CYCLE, nextval, AN- plus invariant D6 minimum-width formatting. Never use MAX+1, a process counter, lpad alone, or setval for a manual code.
- AnimalDuplicateException means only a CodigoInterno conflict. A HasCodigoInternoConflictAsync pre-check creates it with structured PreCheck provenance; AnimalRepository creates it with persisted named-constraint provenance only for PostgreSQL 23505 on UX_Animais_CodigoInterno_CaseInsensitive. The automatic creator retries only the latter provenance when its CodigoInterno matches its current automatic candidate; it never parses exception text or PostgreSQL Detail.
- MAX_AUTOMATIC_CODE_ATTEMPTS=5 is a fixed plan invariant; implement it as private const int MaxAutomaticCodeAttempts = 5 in AnimalAutomaticCreator. A manual code has zero automatic retries; a pre-check conflict, every other SQLSTATE, other constraint, cleanup failure, rollback failure, or cancellation has zero automatic retries.
- Each automatic candidate has a new transaction: begin transaction, nextval, insert/SaveChanges, commit; or rollback, detach the failed entity, dispose the transaction, then begin a new transaction. Never run nextval in an aborted PostgreSQL transaction.
- Keep the working tree free of .gensw/ and .env.local changes. Do not add either path to Git or expose a secret.
- Implementation tasks use red test, minimal implementation, green test, and a review checkpoint. Task 8 is a PostgreSQL integration verification gate, Task 15 is an NA-01/NA-02 regression gate, and Task 16 is the integrated final gate: newly added gate coverage may already pass, and no gate requires an artificial red failure. A gate failure is corrected only in its owning task before the gate is rerun. Do not commit implementation after individual tasks. Only Task 16 may create the one implementation commit after every integrated gate passes.

## Execution dependency order

The task numbers preserve the requested audit grouping. The executable topological order is 1, 2, 3, 5, 4, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16. Tasks 1 through 7 and 9 through 14 are implementation tasks; Tasks 8, 15, and 16 are verification gates. Task 5 precedes Task 4 because IsReferencedByAnimalAsync and the API regression must run against the mapped Animal table and composite foreign keys; this is the minimum ordering change needed to keep Task 4 independently green.

## File Map

| Area | Planned paths |
| --- | --- | --- |
| Domain | src/Backend/GenSW.Domain/Animals/Animal.cs; SexoAnimal.cs; EscopoAnimal.cs; tests/GenSW.Domain.Tests/AnimalTests.cs |
| Application/Animals | src/Backend/GenSW.Application/Animals/ with commands, results, query/sort types, repository/service/allocator contracts, exceptions, AnimalClassificationValidator.cs, AnimalAutomaticCreator.cs, and AnimalService.cs; tests/GenSW.Application.Tests/AnimalClassificationValidatorTests.cs, AnimalAutomaticCreatorTests.cs, AnimalServiceTests.cs |
| Existing Application modules | src/Backend/GenSW.Application/Breeds/IRacaRepository.cs, RacaService.cs, RacaInUseByAnimalException.cs; src/Backend/GenSW.Application/Varieties/IVariedadeRepository.cs, VariedadeService.cs, VariedadeInUseByAnimalException.cs; src/Backend/GenSW.Application/DependencyInjection.cs |
| Infrastructure | src/Backend/GenSW.Infrastructure/Animals/AnimalRepository.cs and PostgreSqlAnimalCodeAllocator.cs; src/Backend/GenSW.Infrastructure/Breeds/RacaRepository.cs; src/Backend/GenSW.Infrastructure/Varieties/VariedadeRepository.cs; src/Backend/GenSW.Infrastructure/Persistence/GenSWDbContext.cs; src/Backend/GenSW.Infrastructure/DependencyInjection.cs; one EF-generated migration pair named by the actual MigrationId ending in _AddAnimalBase.cs and _AddAnimalBase.Designer.cs; GenSWDbContextModelSnapshot.cs |
| Backend tests | tests/GenSW.Infrastructure.Tests/AnimalPersistenceModelTests.cs, AnimalRepositoryTests.cs, AnimalMigrationTests.cs, AnimalCodeAllocatorTests.cs; tests/GenSW.API.Tests/AnimaisApiTests.cs, PostgreSqlAnimalConcurrencyTests.cs; existing Raca/Variedade Application, Infrastructure, and API test files |
| API | src/Backend/GenSW.API/Contracts/Animals/CreateAnimalRequest.cs, UpdateAnimalRequest.cs, UpdateAnimalStatusRequest.cs, AnimalResponse.cs, AnimalsListResponse.cs; src/Backend/GenSW.API/Controllers/AnimaisController.cs |
| Frontend | src/Frontend/GenSW.Web/src/features/animals/types/animals.ts; services/animalsContractParsers.ts, animalsService.ts and their tests; pages/AnimalsListPage.tsx, AnimalsListPage.test.tsx, AnimalFormPage.tsx, AnimalFormPage.test.tsx; routes/AppRoutes.tsx and AppRoutes.test.tsx; features/auth/pages/AuthenticatedHomePage.tsx |

## Cross-task contracts

Task 2 establishes these names. Later tasks must use them exactly.

~~~csharp
public sealed record CreateAnimalCommand(
    string? CodigoInterno,
    string? Nome,
    Guid EspecieId,
    Guid? RacaId,
    Guid? VariedadeId,
    SexoAnimal Sexo,
    DateOnly? DataNascimento,
    EscopoAnimal Escopo);

public sealed record UpdateAnimalCommand(
    string CodigoInterno,
    string? Nome,
    Guid EspecieId,
    Guid? RacaId,
    Guid? VariedadeId,
    SexoAnimal Sexo,
    DateOnly? DataNascimento,
    EscopoAnimal Escopo);

public interface IAnimalService
{
    Task<AnimalResult> CreateAsync(CreateAnimalCommand command, CancellationToken cancellationToken = default);
    Task<AnimalResult?> GetByIdAsync(Guid animalId, CancellationToken cancellationToken = default);
    Task<PagedAnimalResult> ListAsync(AnimalListQuery query, CancellationToken cancellationToken = default);
    Task<AnimalResult> UpdateAsync(Guid animalId, UpdateAnimalCommand command, CancellationToken cancellationToken = default);
    Task<AnimalResult> SetActiveAsync(Guid animalId, bool ativo, CancellationToken cancellationToken = default);
}

public interface IAnimalCodeAllocator
{
    Task<IAnimalAutomaticCodeAttempt> BeginAttemptAsync(CancellationToken cancellationToken = default);
}

public interface IAnimalAutomaticCodeAttempt : IAsyncDisposable
{
    Task<string> AllocateNextCodigoInternoAsync(CancellationToken cancellationToken = default);
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAndDetachAsync(Animal failedAnimal, CancellationToken cancellationToken = default);
}

public enum AnimalDuplicateConflictSource
{
    PreCheck,
    PersistedNamedCodigoInternoUniqueConstraint
}

public sealed class AnimalDuplicateException : Exception
{
    public AnimalDuplicateException(
        string codigoInterno,
        AnimalDuplicateConflictSource source,
        Exception? innerException = null)
        : base("CodigoInterno conflict.", innerException)
    {
        CodigoInterno = codigoInterno;
        ConflictSource = source;
    }

    public string CodigoInterno { get; }
    public AnimalDuplicateConflictSource ConflictSource { get; }
}
~~~

The concrete allocator and AnimalRepository must be scoped and receive the same scoped GenSWDbContext. That shared context is intentional: the allocator transaction is the transaction observed by AddAsync and SaveChangesAsync. It is not a generic unit-of-work abstraction.

### Task 1: Domain Animal

**Files:**

- Create: src/Backend/GenSW.Domain/Animals/SexoAnimal.cs
- Create: src/Backend/GenSW.Domain/Animals/EscopoAnimal.cs
- Create: src/Backend/GenSW.Domain/Animals/Animal.cs
- Create: tests/GenSW.Domain.Tests/AnimalTests.cs

**Interfaces:**

- Consumes: only BCL types and the existing Domain normalization/lifecycle conventions.
- Produces: Animal, SexoAnimal, and EscopoAnimal for Tasks 2, 5, 7, and 9.

~~~csharp
public enum SexoAnimal { Macho = 1, Femea = 2, Indeterminado = 3 }
public enum EscopoAnimal { Operacional = 1, Referencia = 2 }

public sealed class Animal
{
    public static Animal Criar(
        string codigoInterno, string? nome, Guid especieId, Guid? racaId, Guid? variedadeId,
        SexoAnimal sexo, DateOnly? dataNascimento, EscopoAnimal escopo,
        DateOnly utcToday, DateTimeOffset nowUtc);

    public void AlterarCadastro(
        string codigoInterno, string? nome, Guid especieId, Guid? racaId, Guid? variedadeId,
        SexoAnimal sexo, DateOnly? dataNascimento, EscopoAnimal escopo,
        DateOnly utcToday, DateTimeOffset nowUtc);

    public void Inativar(DateTimeOffset nowUtc);
    public void Reativar(DateTimeOffset nowUtc);
}
~~~

- [ ] **Step 1: Write failing Domain tests**

  In AnimalTests.cs, cover a minimum valid Animal; every enum value and an undefined cast; required non-empty EspecieId; optional non-empty RacaId and VariedadeId; canonical CodigoInterno normalization and lengths 1/64/65; null, blank, canonical, and over-200 Nome; future birth date against an injected DateOnly UTC boundary; editable code/name/sex/scope/classification fields; idempotent lifecycle; inactive editing; and unchanged snapshot preserving UpdatedAtUtc.

- [ ] **Step 2: Run the red test**

  Run:

  ~~~powershell
  dotnet test tests/GenSW.Domain.Tests/GenSW.Domain.Tests.csproj --filter FullyQualifiedName~AnimalTests
  ~~~

  Expected: compilation failure because the Animals Domain module does not exist.

- [ ] **Step 3: Implement only local invariants**

  Implement whitespace trim/collapse with the same culture-invariant regex convention used by Especie, Raca, and Variedade. CodigoInterno is required and canonical after normalization; Nome becomes null when blank after normalization. Reject Guid.Empty for required or supplied optional IDs, reject undefined enums, and reject DataNascimento after utcToday. Do not read repositories, add relationship navigation, infer a Breed from a Variety, or validate classification existence/active state.

- [ ] **Step 4: Run the Domain test green**

  Run the same focused command. Expected: all AnimalTests pass.

- [ ] **Step 5: Review checkpoint without commit**

  Run:

  ~~~powershell
  git diff --check
  git diff --stat
  git status --short
  ~~~

  Expected: only Task 1 Domain and test files are changed; no commit and no push.

### Task 2: Application contracts Animal

**Files:**

- Create: src/Backend/GenSW.Application/Animals/CreateAnimalCommand.cs
- Create: src/Backend/GenSW.Application/Animals/UpdateAnimalCommand.cs
- Create: src/Backend/GenSW.Application/Animals/AnimalResult.cs
- Create: src/Backend/GenSW.Application/Animals/PagedAnimalResult.cs
- Create: src/Backend/GenSW.Application/Animals/AnimalListQuery.cs
- Create: src/Backend/GenSW.Application/Animals/AnimalSortField.cs
- Create: src/Backend/GenSW.Application/Animals/IAnimalService.cs
- Create: src/Backend/GenSW.Application/Animals/IAnimalRepository.cs
- Create: src/Backend/GenSW.Application/Animals/IAnimalCodeAllocator.cs
- Create: src/Backend/GenSW.Application/Animals/AnimalNotFoundException.cs
- Create: src/Backend/GenSW.Application/Animals/AnimalDuplicateException.cs
- Create: src/Backend/GenSW.Application/Animals/AnimalAutomaticCodeCollisionLimitExceededException.cs
- Create: src/Backend/GenSW.Application/Animals/AnimalCodeSequenceExhaustedException.cs
- Create: tests/GenSW.Application.Tests/AnimalContractsTests.cs

**Interfaces:**

- Consumes: Animal, SexoAnimal, and EscopoAnimal from Task 1.
- Produces: all contract names in the Cross-task contracts section, plus the read models below, for Tasks 3, 6, 7, 9, and 10.

~~~csharp
public enum AnimalSortField
{
    CodigoInterno, Nome, Sexo, Escopo, Ativo, CreatedAtUtc
}

public sealed record AnimalListQuery(
    int Page = 1, int PageSize = 25, string? Search = null,
    Guid? EspecieId = null, Guid? RacaId = null, Guid? VariedadeId = null,
    SexoAnimal? Sexo = null, EscopoAnimal? Escopo = null, bool? Ativo = null,
    AnimalSortField SortBy = AnimalSortField.CodigoInterno, bool SortDescending = false);

public sealed record AnimalEspecieResumo(Guid Id, string NomeComum, bool Ativo);
public sealed record AnimalRacaResumo(Guid Id, string Nome, bool Ativo);
public sealed record AnimalVariedadeResumo(Guid Id, string Nome, bool Ativo);

public sealed record AnimalResult(
    Guid Id, string CodigoInterno, string? Nome,
    Guid EspecieId, Guid? RacaId, Guid? VariedadeId,
    SexoAnimal Sexo, DateOnly? DataNascimento, EscopoAnimal Escopo, bool Ativo,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc,
    AnimalEspecieResumo Especie, AnimalRacaResumo? Raca, AnimalVariedadeResumo? Variedade);

public sealed record AnimalReadModel(
    Guid Id, string CodigoInterno, string? Nome,
    Guid EspecieId, Guid? RacaId, Guid? VariedadeId,
    SexoAnimal Sexo, DateOnly? DataNascimento, EscopoAnimal Escopo, bool Ativo,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc,
    AnimalEspecieResumo Especie, AnimalRacaResumo? Raca, AnimalVariedadeResumo? Variedade);

public sealed record AnimalListPage(IReadOnlyList<AnimalReadModel> Items, int TotalItems);
public sealed record PagedAnimalResult(
    IReadOnlyList<AnimalResult> Items, int Page, int PageSize, int TotalItems, int TotalPages);
~~~

~~~csharp
public interface IAnimalRepository
{
    Task AddAsync(Animal animal, CancellationToken cancellationToken = default);
    Task<AnimalReadModel?> GetByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Animal?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default);
    Task<AnimalListPage> ListAsync(AnimalListQuery query, CancellationToken cancellationToken = default);
    Task<bool> HasCodigoInternoConflictAsync(
        string codigoInterno, Guid? excludingId = null, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
~~~

- [ ] **Step 1: Write failing contract tests**

  In AnimalContractsTests.cs construct every command, summary, result, page, query, and typed exception. Assert AnimalListQuery defaults are page 1, pageSize 25, CodigoInterno ascending sort; assert all six AnimalSortField values are distinct; assert the Create command preserves null CodigoInterno distinctly from an empty string; and assert AnimalDuplicateException preserves both the conflicting CodigoInterno and its explicit PreCheck or PersistedNamedCodigoInternoUniqueConstraint source without deriving either from exception text.

- [ ] **Step 2: Confirm the contract stage is red**

  Run:

  ~~~powershell
  dotnet test tests/GenSW.Application.Tests/GenSW.Application.Tests.csproj --filter FullyQualifiedName~AnimalContractsTests
  ~~~

  Expected: compilation failure because the Animal contract types do not exist.

- [ ] **Step 3: Define immutable records and narrow exceptions**

  Create the exact contracts above. AnimalDuplicateException represents a CodigoInterno conflict, never a generic database conflict, and carries its conflicting code plus AnimalDuplicateConflictSource. AnimalService uses PreCheck only after HasCodigoInternoConflictAsync reports a conflict. AnimalRepository uses PersistedNamedCodigoInternoUniqueConstraint only after translating PostgreSQL 23505 on UX_Animais_CodigoInterno_CaseInsensitive; every other database failure remains untransformed. AnimalAutomaticCodeCollisionLimitExceededException represents a fifth qualifying automatic collision. AnimalCodeSequenceExhaustedException represents PostgreSQL sequence exhaustion. Keep the exceptions distinct so API mapping and retry eligibility never guess from exception text.

- [ ] **Step 4: Run the contract test green**

  Run:

  ~~~powershell
  dotnet test tests/GenSW.Application.Tests/GenSW.Application.Tests.csproj --filter FullyQualifiedName~AnimalContractsTests
  ~~~

  Expected: all Animal contract tests pass.

- [ ] **Step 5: Review checkpoint without commit**

  Run git diff --check, git diff --stat, and git status --short. Expected: contract-only changes, no commit, and no push.

### Task 3: Application classification rules

**Files:**

- Create: src/Backend/GenSW.Application/Animals/AnimalClassificationValidator.cs
- Create: tests/GenSW.Application.Tests/AnimalClassificationValidatorTests.cs
- Modify: src/Backend/GenSW.Application/DependencyInjection.cs

**Interfaces:**

- Consumes: Task 1 Animal; Task 2 commands; IEspecieRepository, IRacaRepository, IVariedadeRepository, EspecieNotFoundException, RacaNotFoundException, and VariedadeNotFoundException.
- Produces: the internal scoped AnimalClassificationValidator consumed by AnimalService in Task 9.

~~~csharp
internal sealed class AnimalClassificationValidator(
    IEspecieRepository especies,
    IRacaRepository racas,
    IVariedadeRepository variedades)
{
    public Task ValidateCreateAsync(
        Guid especieId, Guid? racaId, Guid? variedadeId,
        CancellationToken cancellationToken = default);

    public Task ValidateUpdateAsync(
        Animal current, Guid especieId, Guid? racaId, Guid? variedadeId,
        CancellationToken cancellationToken = default);
}
~~~

- [ ] **Step 1: Write failing rule tests**

  In AnimalClassificationValidatorTests.cs use concrete fakes for the three existing repositories. Cover: missing Species/Breed/Variety maps to its existing not-found exception; create requires active Species and active supplied classifications; Breed and Variety each independently require the requested Species; either, both, or neither classification is valid; existing inactive Species/Breed/Variety links may be preserved; a new inactive destination is rejected; and Policy B rejects a changed Species combined with a retained incompatible classification. Add explicit update cases where the retained Especie A became inactive after the Animal was linked: a newly selected active Raca of Especie A is valid, and independently a newly selected active Variedade of Especie A is valid; a new inactive Raca/Variedade, a classification of another Species, or a change to another inactive Species is rejected.

- [ ] **Step 2: Run the red validator tests**

  Run:

  ~~~powershell
  dotnet test tests/GenSW.Application.Tests/GenSW.Application.Tests.csproj --filter FullyQualifiedName~AnimalClassificationValidatorTests
  ~~~

  Expected: compilation failure because AnimalClassificationValidator does not exist.

- [ ] **Step 3: Implement the explicit snapshot policy**

  Validate the complete incoming snapshot, never one classification by inference from the other. For create, active Species is mandatory and each non-null classification must exist, be active, and match that Species. For update, permit the current inactive historical IDs only when the same link is retained. When the current EspecieId is retained even though that Species is now inactive, a newly supplied or changed Raca and/or Variedade is still permitted only when it is active and compatible with that same retained Species. A newly supplied or changed classification is otherwise required to be active and compatible. When Species changes, require the new Species active and require each non-null Breed/Variety in the same request to be active and compatible; return ArgumentException with the conflicting parameter name instead of silently clearing it. Preserve Policy B.

  Register AnimalClassificationValidator as scoped in AddApplication. Do not create a generic classification service.

- [ ] **Step 4: Run the validator tests green**

  Run the focused command from Step 2. Expected: all matrix cases pass.

- [ ] **Step 5: Review checkpoint without commit**

  Run git diff --check, git diff --stat, and git status --short. Expected: only Animal Application validation and its tests are added; no commit and no push.

### Task 4: Protection of Breed and Variety used by Animal

**Files:**

- Create: src/Backend/GenSW.Application/Breeds/RacaInUseByAnimalException.cs
- Create: src/Backend/GenSW.Application/Varieties/VariedadeInUseByAnimalException.cs
- Modify: src/Backend/GenSW.Application/Breeds/IRacaRepository.cs
- Modify: src/Backend/GenSW.Application/Breeds/RacaService.cs
- Modify: src/Backend/GenSW.Application/Varieties/IVariedadeRepository.cs
- Modify: src/Backend/GenSW.Application/Varieties/VariedadeService.cs
- Modify: src/Backend/GenSW.Infrastructure/Breeds/RacaRepository.cs
- Modify: src/Backend/GenSW.Infrastructure/Varieties/VariedadeRepository.cs
- Modify: src/Backend/GenSW.API/Controllers/RacasController.cs
- Modify: src/Backend/GenSW.API/Controllers/VariedadesController.cs
- Modify: tests/GenSW.Application.Tests/RacaServiceTests.cs
- Modify: tests/GenSW.Application.Tests/VariedadeServiceTests.cs
- Modify: tests/GenSW.Infrastructure.Tests/RacaRepositoryTests.cs
- Modify: tests/GenSW.Infrastructure.Tests/VariedadeRepositoryTests.cs
- Modify: tests/GenSW.API.Tests/RacasApiTests.cs
- Modify: tests/GenSW.API.Tests/VariedadesApiTests.cs

**Interfaces:**

- Consumes: Task 1 Animal; the mapped Animal DbSet and composite foreign-key names from Task 5.
- Produces: the protected existing Breed/Variety update behavior used by all later regression gates.

~~~csharp
// Add identically named intent methods to the existing concrete repository contracts.
Task<bool> IsReferencedByAnimalAsync(Guid racaId, CancellationToken cancellationToken = default);
Task<bool> IsReferencedByAnimalAsync(Guid variedadeId, CancellationToken cancellationToken = default);
~~~

- [ ] **Step 1: Write red service and API regression tests**

  Add fake-repository tests proving a Species change is rejected only when the Raca or Variedade is referenced by an Animal. Add API tests that seed a referenced classification, PUT it with a different EspecieId, and expect 409. In the same API tests prove name edits, inactivation, and reactivation of that referenced classification still return 200.

- [ ] **Step 2: Run the red checks after Task 5 is available**

  Run:

  ~~~powershell
  dotnet test tests/GenSW.Application.Tests/GenSW.Application.Tests.csproj --filter "FullyQualifiedName~RacaServiceTests|FullyQualifiedName~VariedadeServiceTests"
  dotnet test tests/GenSW.API.Tests/GenSW.API.Tests.csproj --filter "FullyQualifiedName~RacasApiTests|FullyQualifiedName~VariedadesApiTests"
  ~~~

  Expected: the new 409 expectations fail before this protection is implemented. Task 5 is a required dependency because real API tests use the Animal model.

- [ ] **Step 3: Add the two-layer protection**

  Before Raca.AlterarCadastro or Variedade.AlterarCadastro changes EspecieId, call IsReferencedByAnimalAsync and throw the corresponding in-use exception when true. Implement each query with context.Animais.AsNoTracking().AnyAsync against the correct nullable foreign-key field. In SaveChangesAsync, translate only PostgreSQL foreign-key violation for the explicitly named planned composite FK into the same exception; preserve all other database failures.

  Controllers map the two in-use exceptions to 409 ProblemDetails. Do not block name, Ativo, or unchanged-EspecieId updates.

- [ ] **Step 4: Run green service, repository, and API checks**

  Run the commands from Step 2 and the two focused Infrastructure repository test classes. Expected: changing a used classification Species returns 409; allowed edits remain green.

- [ ] **Step 5: Review checkpoint without commit**

  Run git diff --check, git diff --stat, and git status --short. Expected: only explicit NA-03 protection changes are present; no commit and no push.

### Task 5: EF model and additive migration

**Files:**

- Modify: src/Backend/GenSW.Infrastructure/Persistence/GenSWDbContext.cs
- Create: src/Backend/GenSW.Infrastructure/Persistence/Migrations/<MigrationId>_AddAnimalBase.cs (the exact filename generated by EF Core)
- Create: src/Backend/GenSW.Infrastructure/Persistence/Migrations/<MigrationId>_AddAnimalBase.Designer.cs (the matching exact filename generated by EF Core)
- Modify: src/Backend/GenSW.Infrastructure/Persistence/Migrations/GenSWDbContextModelSnapshot.cs
- Create: tests/GenSW.Infrastructure.Tests/AnimalPersistenceModelTests.cs
- Create: tests/GenSW.Infrastructure.Tests/AnimalMigrationTests.cs

**Interfaces:**

- Consumes: Task 1 Animal and enums.
- Produces: GenSWDbContext.Animais, exact database constraint names, alternate keys, composite FKs, and migration schema required by Tasks 4, 6, 7, and 8.

The migration source pair is the exact <MigrationId>_AddAnimalBase.cs and <MigrationId>_AddAnimalBase.Designer.cs pair produced by the scaffold command in Step 3. Preserve EF Core's generated filenames, partial classes, MigrationAttribute, and normal conventions; do not rename the generated migration manually.

- [ ] **Step 1: Write red model and migration tests**

  In AnimalPersistenceModelTests.cs inspect both runtime and design-time models. Assert table Animais; all nullable/required columns and maximum lengths; integer enum conversions; true default for Ativo; all four named checks; the case-insensitive code index; indexes for EspecieId, (RacaId, EspecieId), and (VariedadeId, EspecieId); alternate keys AK_Racas_Id_EspecieId and AK_Variedades_Id_EspecieId; and Restrict composite FKs with names FK_Animais_Racas_RacaId_EspecieId and FK_Animais_Variedades_VariedadeId_EspecieId.

  In AnimalMigrationTests.cs apply migrations to EphemeralPostgreSql, inspect PostgreSQL catalog metadata for the sequence, checks, functional index, keys, and FKs, then migrate Down to the previous migration and assert Animais, AnimalCodigoInternoSequence, and both alternate keys no longer exist.

- [ ] **Step 2: Run the red tests**

  Run:

  ~~~powershell
  dotnet test tests/GenSW.Infrastructure.Tests/GenSW.Infrastructure.Tests.csproj --filter "FullyQualifiedName~AnimalPersistenceModelTests|FullyQualifiedName~AnimalMigrationTests"
  ~~~

  Expected: Animal mapping and migration do not exist.

- [ ] **Step 3: Implement the model and scaffold one migration**

  Add DbSet<Animal> Animais and configure:

  - Id uuid primary key; CodigoInterno varchar(64) required; Nome varchar(200) nullable; EspecieId required; RacaId and VariedadeId nullable; Sexo and Escopo stored as required integers; DataNascimento date nullable; Ativo required default true; timestamps required.
  - CK_Animais_CodigoInterno_Canonical and CK_Animais_Nome_Canonical, using the existing PostgreSQL canonical-space predicate shape. Nome accepts null but never persisted empty.
  - CK_Animais_Sexo with IN (1, 2, 3) and CK_Animais_Escopo with IN (1, 2).
  - UX_Animais_CodigoInterno_CaseInsensitive as PostgreSQL unique index on lower("CodigoInterno").
  - required FK Animais.EspecieId to Especies.Id with Restrict;
    alternate keys Racas(Id, EspecieId) and Variedades(Id, EspecieId);
    optional composite FKs (RacaId, EspecieId) and (VariedadeId, EspecieId) to those alternate keys, both Restrict and explicitly named as above.

  Scaffold the migration with:

  ~~~powershell
  dotnet ef migrations add AddAnimalBase --project src/Backend/GenSW.Infrastructure/GenSW.Infrastructure.csproj --startup-project src/Backend/GenSW.API/GenSW.API.csproj --output-dir Persistence/Migrations
  ~~~

  Keep the generated <MigrationId>_AddAnimalBase.cs and <MigrationId>_AddAnimalBase.Designer.cs filenames, partial classes, and MigrationAttribute exactly as EF Core produced them. Edit only the new migration's Up/Down as needed. Up creates the alternate keys before Animais, creates Animais and its named constraints/indexes, and executes:

  ~~~sql
  CREATE SEQUENCE "AnimalCodigoInternoSequence"
  AS bigint
  START WITH 1
  INCREMENT BY 1
  MINVALUE 1
  NO CYCLE
  CACHE 1;
  ~~~

  Down removes Animais first, then both alternate keys, then the sequence. Do not alter any older migration.

- [ ] **Step 4: Run model and migration tests green**

  Run the focused command from Step 2. Expected: the migration has a reversible PostgreSQL round trip and all named structural guarantees exist.

- [ ] **Step 5: Review checkpoint without commit**

  Run git diff --check, git diff --stat, git status --short, and git diff --name-only against the pre-Task-5 migration. Expected: exactly one newly generated <MigrationId>_AddAnimalBase.cs and <MigrationId>_AddAnimalBase.Designer.cs pair plus the snapshot are the only migration artifacts changed; no historical migration changed.

### Task 6: PostgreSQL allocator

**Files:**

- Create: src/Backend/GenSW.Infrastructure/Animals/PostgreSqlAnimalCodeAllocator.cs
- Modify: src/Backend/GenSW.Infrastructure/DependencyInjection.cs
- Create: tests/GenSW.Infrastructure.Tests/AnimalCodeAllocatorTests.cs

**Interfaces:**

- Consumes: IAnimalCodeAllocator and IAnimalAutomaticCodeAttempt from Task 2; GenSWDbContext and sequence from Task 5.
- Produces: scoped PostgreSqlAnimalCodeAllocator and an attempt object used by Task 7.

- [ ] **Step 1: Write red allocator tests**

  In AnimalCodeAllocatorTests.cs, using EphemeralPostgreSql, assert first allocation is AN-000001, values after 999999 retain all digits, a manual-looking code never invokes setval, and a started attempt keeps its allocated number after rollback. Add a sequence-exhaustion setup with ALTER SEQUENCE using a maximum of 1, consume its only value, and assert AnimalCodeSequenceExhaustedException on the next allocation.

  Use an instrumented transaction or equivalent observable test seam to assert the attempt lifecycle: CommitAsync followed by DisposeAsync performs no later rollback; RollbackAndDetachAsync followed by DisposeAsync performs no second rollback; and DisposeAsync on an open attempt performs exactly one rollback and one transaction disposal. Repeat DisposeAsync in each terminal state and assert it is a no-op: no second rollback, detach, commit, disposal, or nextval, and no transaction remains active.

- [ ] **Step 2: Run the red allocator tests**

  Run:

  ~~~powershell
  dotnet test tests/GenSW.Infrastructure.Tests/GenSW.Infrastructure.Tests.csproj --filter FullyQualifiedName~AnimalCodeAllocatorTests
  ~~~

  Expected: PostgreSqlAnimalCodeAllocator does not exist.

- [ ] **Step 3: Implement a transaction-bound allocator**

  BeginAttemptAsync starts a new GenSWDbContext.Database transaction and returns an attempt that owns it. AllocateNextCodigoInternoAsync executes SELECT nextval('"AnimalCodigoInternoSequence"') using the current transaction connection, converts the bigint with n.ToString("D6", CultureInfo.InvariantCulture), and prefixes AN-. Catch only PostgreSQL sequence-limit SQLSTATE 2200H and throw AnimalCodeSequenceExhaustedException.

  The attempt records terminal state. CommitAsync commits and disposes the owned transaction, then leaves the attempt terminal. RollbackAndDetachAsync rolls back first, detaches exactly the failed Animal through the same DbContext ChangeTracker, then disposes the transaction and leaves the attempt terminal. If rollback, detach, or disposal fails, propagate that error. DisposeAsync rolls back and disposes an unfinished attempt exactly once; after CommitAsync, RollbackAndDetachAsync, or an earlier DisposeAsync, it is an idempotent no-op. It must never begin another attempt or issue another nextval. Register IAnimalCodeAllocator as scoped.

- [ ] **Step 4: Run allocator tests green**

  Run the command from Step 2. Expected: PostgreSQL allocation, D6-minimum formatting, accepted gaps, and sequence exhaustion behavior are proven.

- [ ] **Step 5: Review checkpoint without commit**

  Run git diff --check, git diff --stat, and git status --short. Expected: allocator-only Infrastructure/DI/test changes, no commit and no push.

### Task 7: Persistence and bounded automatic retry

**Files:**

- Create: src/Backend/GenSW.Application/Animals/AnimalAutomaticCreator.cs
- Create: src/Backend/GenSW.Infrastructure/Animals/AnimalRepository.cs
- Modify: src/Backend/GenSW.Infrastructure/DependencyInjection.cs
- Create: tests/GenSW.Application.Tests/AnimalAutomaticCreatorTests.cs
- Create: tests/GenSW.Infrastructure.Tests/AnimalRepositoryTests.cs

**Interfaces:**

- Consumes: Task 1 Animal; Task 2 repository and allocator contracts; Task 5 model; Task 6 allocator.
- Produces: AnimalAutomaticCreator.CreateAsync and AnimalRepository persistence/read behavior consumed by Task 9 and Task 10.

~~~csharp
internal sealed class AnimalAutomaticCreator(
    IAnimalRepository repository,
    IAnimalCodeAllocator allocator,
    TimeProvider timeProvider)
{
    public Task<Animal> CreateAsync(
        CreateAnimalCommand command,
        CancellationToken cancellationToken = default);
}
~~~

- [ ] **Step 1: Write red retry and repository tests**

  In AnimalAutomaticCreatorTests.cs, use fakes that expose the sequence of attempts. Prove exactly this sequence for an eligible collision: BeginAttemptAsync, AllocateNextCodigoInternoAsync, AddAsync, SaveChangesAsync, RollbackAndDetachAsync, then a new BeginAttemptAsync. Eligibility requires an AnimalDuplicateException whose ConflictSource is PersistedNamedCodigoInternoUniqueConstraint and whose CodigoInterno equals the automatic candidate built by the current attempt. Assert a fifth eligible collision throws AnimalAutomaticCodeCollisionLimitExceededException after rollback and performs no sixth allocation. Assert an AnimalDuplicateException with PreCheck provenance, one with a different CodigoInterno, an unrelated exception, a rollback failure, and a detach failure do not trigger a new attempt. Manual-code zero-retry behavior belongs to the IAnimalService tests in Task 9.

  In AnimalRepositoryTests.cs, exercise PostgreSQL read projection, case-insensitive code pre-check, optional Breed/Variety projection, all filters, sorting with Id tie-breaker, and translation only of PostgreSQL 23505 on UX_Animais_CodigoInterno_CaseInsensitive into AnimalDuplicateException with PersistedNamedCodigoInternoUniqueConstraint provenance. Assert another constraint or SQLSTATE is not translated to AnimalDuplicateException.

- [ ] **Step 2: Run the red tests**

  Run:

  ~~~powershell
  dotnet test tests/GenSW.Application.Tests/GenSW.Application.Tests.csproj --filter FullyQualifiedName~AnimalAutomaticCreatorTests
  dotnet test tests/GenSW.Infrastructure.Tests/GenSW.Infrastructure.Tests.csproj --filter FullyQualifiedName~AnimalRepositoryTests
  ~~~

  Expected: automatic creator and AnimalRepository do not exist.

- [ ] **Step 3: Implement the exact persistence boundary**

  AnimalRepository:

  - adds/loads Animals with AsNoTracking read projections that join Especies, Racas, and Variedades in one query; nullable left joins create nullable summaries and avoid N+1;
  - filters search with escaped EF.Functions.ILike over CodigoInterno and Nome; applies each optional classification/enumeration/Ativo filter; orders each allowed sort field and then Id;
  - catches DbUpdateException only when InnerException is PostgresException with SqlState 23505 and ConstraintName UX_Animais_CodigoInterno_CaseInsensitive, then throws AnimalDuplicateException for the failed Animal.CodigoInterno with PersistedNamedCodigoInternoUniqueConstraint provenance; every other database failure escapes unchanged.

  AnimalAutomaticCreator loops attemptNumber from 1 through 5. For every attempt it uses await using around a fresh allocator attempt, allocates one candidate, creates Animal with that candidate, adds it, saves it, commits it, and returns it. It retries only an AnimalDuplicateException raised by the current SaveChangesAsync when ConflictSource is PersistedNamedCodigoInternoUniqueConstraint and CodigoInterno equals the candidate just allocated for the failed Animal. For that one qualified case, await RollbackAndDetachAsync to completion; the subsequent await using disposal is idempotent, and only then may the next BeginAttemptAsync occur. On the fifth qualified collision, throw AnimalAutomaticCodeCollisionLimitExceededException after the fifth rollback. Pre-check conflicts, manual codes, another constraint, another SQLSTATE, a different candidate, a raw DbUpdateException, cleanup failures, cancellation, and every generic error propagate without retry. Do not parse PostgreSQL Detail or exception text, and never run a sixth nextval.

- [ ] **Step 4: Run the focused tests green**

  Run the commands from Step 2. Expected: repository reads are projected and retry decisions are fully closed.

- [ ] **Step 5: Review checkpoint without commit**

  Run git diff --check, git diff --stat, and git status --short. Expected: no transaction object or failed tracked Animal can cross an automatic attempt boundary; no commit and no push.

### Task 8: PostgreSQL integration verification gate

**Files:**

- Create: tests/GenSW.API.Tests/PostgreSqlAnimalConcurrencyTests.cs
- Modify: tests/GenSW.Infrastructure.Tests/AnimalCodeAllocatorTests.cs
- Modify: tests/GenSW.Infrastructure.Tests/AnimalRepositoryTests.cs
- Modify: tests/GenSW.Infrastructure.Tests/AnimalMigrationTests.cs

**Interfaces:**

- Consumes: Tasks 4 through 7 and the existing EphemeralPostgreSql harness.
- Produces: real PostgreSQL evidence for the sequence, retry boundary, constraints, and classification update protection. This is a verification gate, not an implementation task with an artificial red phase.

- [ ] **Step 1: Add or complete PostgreSQL-only integration scenarios after Tasks 4 through 7 are green**

  Use EphemeralPostgreSql.StartAsync and a non-parallel xUnit collection. Do not substitute SQLite. Reuse AuthWebApplicationFactory(postgreSql.ConnectionString) and the existing automatic skip when PostgreSQL binaries are absent.

  Implement these named tests:

  1. Two authenticated automatic POST requests run concurrently and both receive 201 with distinct codes.
  2. A manual AN-000001 inserted before the automatic request makes the automatic path skip its first candidate and create AN-000002.
  3. Two concurrent equal manual codes yield one 201 and one 409, without allocator retry.
  4. PUT to another Animal's code returns 409 and preserves the original persisted code.
  5. Manual inserts for AN-000001 through AN-000005 cause an automatic request to roll back five attempts, return the deterministic 409 limit exception, and leave the sequence at 5 without a sixth allocation.
  6. The expected code-index collision reports the structured PersistedNamedCodigoInternoUniqueConstraint provenance for its actual automatic candidate. A test-only additional unique index creates an other-constraint 23505; verify it is not translated to that provenance, uses a single allocation, and does not retry.
  7. A test-only trigger that raises a non-23505 SQLSTATE causes one allocation and no retry.
  8. A failed automatic first candidate consumes its sequence value; the next successful code proves the gap is accepted.
  9. A direct attempt test records SELECT txid_current() immediately after the first allocator nextval, causes the named code uniqueness collision, calls RollbackAndDetachAsync, starts a second attempt, records txid_current() again, and asserts different transaction IDs, no active old transaction, successful second nextval/insert/commit, and no 25P02.
  10. A direct SQL insert with a Breed or Variety from another Species violates its corresponding composite FK.
  11. Moving a referenced Breed and a referenced Variety to another Species is blocked by the API pre-check and by a direct PostgreSQL update against the Restrict composite FK.

- [ ] **Step 2: Run the PostgreSQL integration verification gate**

  Run:

  ~~~powershell
  dotnet test tests/GenSW.API.Tests/GenSW.API.Tests.csproj --filter FullyQualifiedName~PostgreSqlAnimalConcurrencyTests
  dotnet test tests/GenSW.Infrastructure.Tests/GenSW.Infrastructure.Tests.csproj --filter "FullyQualifiedName~AnimalCodeAllocatorTests|FullyQualifiedName~AnimalRepositoryTests|FullyQualifiedName~AnimalMigrationTests"
  ~~~

  Expected: every non-skipped test passes after its owner Tasks 4 through 7 are complete; tests may be skipped only by the existing missing-PostgreSQL-binary mechanism. A newly added integration test that already passes is valid and does not require an artificial red failure.

- [ ] **Step 3: Correct only the owner of a verified gate failure**

  If the gate identifies a defect, adjust only the implementation owned by Tasks 4 through 7, then return to the focused owner tests and this gate. Keep EphemeralPostgreSql intact; do not add Docker, Testcontainers, a new local database convention, timing sleeps, a retry outside Animal automatic-code creation, or an artificial red phase.

- [ ] **Step 4: Re-run the PostgreSQL integration verification gate**

  Run the commands from Step 2. Expected: every non-skipped test proves real PostgreSQL behavior, including structured retry eligibility and transaction replacement after the aborted collision.

- [ ] **Step 5: Review checkpoint without commit**

  Run git diff --check, git diff --stat, and git status --short. Expected: PostgreSQL tests are deterministic, isolated, and use no production-only test switch; no commit and no push.

### Task 9: Animal Application service

**Files:**

- Create: src/Backend/GenSW.Application/Animals/AnimalService.cs
- Create: tests/GenSW.Application.Tests/AnimalServiceTests.cs
- Modify: src/Backend/GenSW.Application/DependencyInjection.cs
- Modify: tests/GenSW.Application.Tests/DependencyInjectionTests.cs

**Interfaces:**

- Consumes: all Animal contracts, AnimalClassificationValidator, AnimalAutomaticCreator, repository interfaces, and TimeProvider.
- Produces: IAnimalService implementation for the API.

- [ ] **Step 1: Write red Application service tests**

  Cover manual create and automatic create; omitted/null code invoking automatic flow while empty or whitespace code is a 400-worthy Domain failure; all local enum/query validation; missing/inactive/compatible/incompatible classifications; historical inactive preservation, including a retained inactive Species with a newly selected active matching Raca and independently a newly selected active matching Variedade; Policy B; get including inactive; complete list query; manual pre-check conflict and persisted manual-race conflict; update duplicate; editable sex/escopo/data/name/code; inactive Animal update/reactivation; and lifecycle idempotency.

- [ ] **Step 2: Run the red service and DI tests**

  Run:

  ~~~powershell
  dotnet test tests/GenSW.Application.Tests/GenSW.Application.Tests.csproj --filter "FullyQualifiedName~AnimalServiceTests|FullyQualifiedName~DependencyInjectionTests"
  ~~~

  Expected: AnimalService is missing and IAnimalService cannot resolve.

- [ ] **Step 3: Implement orchestration without N+1**

  CreateAsync first validates classifications. If CodigoInterno is null, call AnimalAutomaticCreator; otherwise construct Animal with the requested code, pre-check through HasCodigoInternoConflictAsync for a fast message, and when it reports a conflict throw AnimalDuplicateException with PreCheck provenance. Then add/save once and never call the allocator for a manual code. A race translated by AnimalRepository remains the same CodigoInterno-conflict exception with persisted named-constraint provenance, but manual creation still never retries. GetByIdAsync and ListAsync map repository read models. UpdateAsync loads the tracked Animal, validates the entire proposed classification snapshot (including the retained-inactive-Species/new-active-Raca-or-Variedade case), calls AlterarCadastro with DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime), pre-checks code excluding current Id with the same explicit provenance, saves, and returns the projected result. SetActiveAsync applies the existing idempotent lifecycle convention.

  Validate page 1.., pageSize 1..100, defined optional enums, defined sort field, and normalized optional search. Register IAnimalService as scoped. Do not translate raw database errors here except the typed Animal exceptions already defined.

- [ ] **Step 4: Run the Application suite green**

  Run the command from Step 2. Expected: all paths use the explicit contracts and preserve historic inactive links correctly.

- [ ] **Step 5: Review checkpoint without commit**

  Run git diff --check, git diff --stat, and git status --short. Expected: Animal Application logic remains specific and no generic repository or classification layer appears.

### Task 10: Authenticated API

**Files:**

- Create: src/Backend/GenSW.API/Contracts/Animals/CreateAnimalRequest.cs
- Create: src/Backend/GenSW.API/Contracts/Animals/UpdateAnimalRequest.cs
- Create: src/Backend/GenSW.API/Contracts/Animals/UpdateAnimalStatusRequest.cs
- Create: src/Backend/GenSW.API/Contracts/Animals/AnimalResponse.cs
- Create: src/Backend/GenSW.API/Contracts/Animals/AnimalsListResponse.cs
- Create: src/Backend/GenSW.API/Controllers/AnimaisController.cs
- Create: tests/GenSW.API.Tests/AnimaisApiTests.cs

**Interfaces:**

- Consumes: IAnimalService, Animal result/query types, Domain enums, and existing API ProblemDetails/controller conventions.
- Produces: POST/GET/PUT/PATCH Animal HTTP contract used by Task 11.

~~~csharp
public sealed record CreateAnimalRequest(
    string? CodigoInterno, string? Nome, Guid EspecieId, Guid? RacaId, Guid? VariedadeId,
    SexoAnimal Sexo, DateOnly? DataNascimento, EscopoAnimal Escopo);

public sealed record UpdateAnimalRequest(
    string CodigoInterno, string? Nome, Guid EspecieId, Guid? RacaId, Guid? VariedadeId,
    SexoAnimal Sexo, DateOnly? DataNascimento, EscopoAnimal Escopo);

public sealed record UpdateAnimalStatusRequest(bool Ativo);
public sealed record AnimalEspecieResumoResponse(Guid Id, string NomeComum, bool Ativo);
public sealed record AnimalRacaResumoResponse(Guid Id, string Nome, bool Ativo);
public sealed record AnimalVariedadeResumoResponse(Guid Id, string Nome, bool Ativo);
public sealed record AnimalResponse(
    Guid Id, string CodigoInterno, string? Nome,
    Guid EspecieId, Guid? RacaId, Guid? VariedadeId,
    SexoAnimal Sexo, DateOnly? DataNascimento, EscopoAnimal Escopo, bool Ativo,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc,
    AnimalEspecieResumoResponse Especie,
    AnimalRacaResumoResponse? Raca,
    AnimalVariedadeResumoResponse? Variedade);
public sealed record AnimalsListResponse(
    IReadOnlyList<AnimalResponse> Items,
    int Page, int PageSize, int TotalItems, int TotalPages);
~~~

- [ ] **Step 1: Write red API tests**

  In AnimaisApiTests.cs cover unauthenticated 401; POST 201 and Location; automatic and manual creation; list defaults, all filters, all sort values/directions, offset pages, case-insensitive code/name search; GET active and inactive; PUT full snapshot; PATCH idempotent lifecycle; 400 invalid input/query/policy B/inactive new destination; 404 all missing resources; 409 duplicate code, automatic limit, sequence exhaustion, and classification-in-use behavior where applicable; and DELETE returning 405.

- [ ] **Step 2: Run the red API test**

  Run:

  ~~~powershell
  dotnet test tests/GenSW.API.Tests/GenSW.API.Tests.csproj --filter FullyQualifiedName~AnimaisApiTests
  ~~~

  Expected: AnimaisController and contracts do not exist.

- [ ] **Step 3: Implement the five routes**

  Add an [ApiController], [Authorize], [Route("api/v1/animais")] controller with:

  - POST /api/v1/animais returning CreatedAtAction;
  - GET /api/v1/animais with page, pageSize, search, especieId, racaId, variedadeId, sexo, escopo, ativo, sortBy, and sortDirection;
  - GET /api/v1/animais/{id};
  - PUT /api/v1/animais/{id};
  - PATCH /api/v1/animais/{id}/ativo.

  Keep JSON enums numeric. Map typed not-found exceptions to 404; AnimalDuplicateException, AnimalAutomaticCodeCollisionLimitExceededException, and AnimalCodeSequenceExhaustedException to 409; ArgumentException and invalid query parsing to 400. Return ProblemDetails without PostgreSQL Detail, constraint, or SQLSTATE. Do not declare any DELETE endpoint.

- [ ] **Step 4: Run the API test green**

  Run the command from Step 2. Expected: all required status codes and route behavior pass.

- [ ] **Step 5: Review checkpoint without commit**

  Run git diff --check, git diff --stat, and git status --short. Expected: only authenticated Animal API artifacts are changed; no commit and no push.

### Task 11: Frontend HTTP contract

**Files:**

- Create: src/Frontend/GenSW.Web/src/features/animals/types/animals.ts
- Create: src/Frontend/GenSW.Web/src/features/animals/services/animalsContractParsers.ts
- Create: src/Frontend/GenSW.Web/src/features/animals/services/animalsService.ts
- Create: src/Frontend/GenSW.Web/src/features/animals/services/animalsContractParsers.test.ts
- Create: src/Frontend/GenSW.Web/src/features/animals/services/animalsService.test.ts

**Interfaces:**

- Consumes: shared httpRequest, InvalidApiResponseError, HttpError, and the API contract from Task 10.
- Produces: Animal types, strict parsers, and authenticated HTTP functions for Tasks 12 through 14.

~~~typescript
export type SexoAnimal = 1 | 2 | 3
export type EscopoAnimal = 1 | 2
export type AnimalSortBy = 'codigoInterno' | 'nome' | 'sexo' | 'escopo' | 'ativo' | 'createdAtUtc'
export type SortDirection = 'asc' | 'desc'

export interface Animal {
  id: string
  codigoInterno: string
  nome: string | null
  especieId: string
  racaId: string | null
  variedadeId: string | null
  sexo: SexoAnimal
  dataNascimento: string | null
  escopo: EscopoAnimal
  ativo: boolean
  createdAtUtc: string
  updatedAtUtc: string
  especie: { id: string; nomeComum: string; ativo: boolean }
  raca: { id: string; nome: string; ativo: boolean } | null
  variedade: { id: string; nome: string; ativo: boolean } | null
}

export interface CreateAnimalRequest {
  codigoInterno?: string | null
  nome?: string | null
  especieId: string
  racaId?: string | null
  variedadeId?: string | null
  sexo: SexoAnimal
  dataNascimento?: string | null
  escopo: EscopoAnimal
}

export interface UpdateAnimalRequest {
  codigoInterno: string
  nome?: string | null
  especieId: string
  racaId?: string | null
  variedadeId?: string | null
  sexo: SexoAnimal
  dataNascimento?: string | null
  escopo: EscopoAnimal
}

export interface UpdateAnimalStatusRequest { ativo: boolean }
export interface AnimalsPage {
  items: Animal[]
  page: number
  pageSize: number
  totalItems: number
  totalPages: number
}

export interface ListAnimalsParams {
  page?: number
  pageSize?: number
  search?: string
  especieId?: string
  racaId?: string
  variedadeId?: string
  sexo?: SexoAnimal
  escopo?: EscopoAnimal
  ativo?: boolean
  sortBy?: AnimalSortBy
  sortDirection?: SortDirection
}

export async function createAnimal(request: CreateAnimalRequest): Promise<Animal>
export async function getAnimalById(id: string): Promise<Animal>
export async function listAnimals(params?: ListAnimalsParams): Promise<AnimalsPage>
export async function updateAnimal(id: string, request: UpdateAnimalRequest): Promise<Animal>
export async function setAnimalAtivo(id: string, ativo: boolean): Promise<Animal>
~~~

- [ ] **Step 1: Write red parser and service tests**

  Assert parsers reject absent/wrong-type fields, invalid numeric enums, invalid dates, invalid summaries, and non-null optional summaries that lack required data. Assert they accept nullable Nome/DataNascimento/Raca/Variedade. Assert service tests verify all HTTP methods, authenticated option, and all query names. For create serialization, assert codigoInterno undefined is omitted from the request body, explicit null is preserved as null, and explicit empty or whitespace strings are serialized unchanged. animalsService must never silently convert an explicit string into property omission.

- [ ] **Step 2: Run the red frontend contract tests**

  Run:

  ~~~powershell
  npm --prefix src/Frontend/GenSW.Web test -- src/features/animals/services
  ~~~

  Expected: the animals feature does not exist.

- [ ] **Step 3: Implement types, strict parsers, and client**

  Use endpoint /animais and the existing feature-local parser style. Parse DataNascimento as null or an exact calendar date string, timestamps as ISO dates, enums only from the closed numeric sets, and nested summaries only when their shape is complete. Build query strings only from supplied values. For create, forward the request object without semantic reinterpretation: JavaScript serialization naturally omits codigoInterno when it is undefined, while null, empty strings, and whitespace strings remain explicit values. Form-state policy belongs exclusively to Task 13, not animalsService. All calls use authenticated: true and no new library.

- [ ] **Step 4: Run the frontend contract tests green**

  Run the command from Step 2. Expected: contract parsing and request serialization pass.

- [ ] **Step 5: Review checkpoint without commit**

  Run git diff --check, git diff --stat, and git status --short. Expected: the feature has no UI page yet and no external dependency; no commit and no push.

### Task 12: Animals list

**Files:**

- Create: src/Frontend/GenSW.Web/src/features/animals/pages/AnimalsListPage.tsx
- Create: src/Frontend/GenSW.Web/src/features/animals/pages/AnimalsListPage.test.tsx

**Interfaces:**

- Consumes: listAnimals, setAnimalAtivo, Animal types from Task 11; listEspecies, listRacas, and listVariedades from current frontend modules.
- Produces: list page used by routes/navigation in Task 14.

- [ ] **Step 1: Write red page tests**

  Mock all four list services. Cover table columns CodigoInterno/Nome/Species/Breed/Variety/Sexo/Escopo/Ativo; all filter query values; all six sort fields; 25/50/100 page sizes; page navigation; loading; empty; failed list with retry; lifecycle mutation error that keeps the rendered list; and inactive edit links.

  For historical filters, make Species, Breeds, and Varieties return more than one page with inactive records. Assert the page fetches every page with pageSize 100 and no Ativo restriction, labels inactive choices, and sends each selected ID to listAnimals.

- [ ] **Step 2: Run the red list-page test**

  Run:

  ~~~powershell
  npm --prefix src/Frontend/GenSW.Web test -- src/features/animals/pages/AnimalsListPage.test.tsx
  ~~~

  Expected: AnimalsListPage does not exist.

- [ ] **Step 3: Implement the dedicated list page**

  Follow the current list-page lifecycle behavior without extracting a generic table. Load all catalog pages independently and preserve catalog-load error separately from list data. Search by code/name, use the precise filter/sort state in ListAnimalsParams, reset page on a changed filter, and protect the lifecycle button from double submission. Keep inactive results visible when filters allow them.

- [ ] **Step 4: Run the list-page test green**

  Run the command from Step 2. Expected: filters, history, pagination, states, and lifecycle behavior pass.

- [ ] **Step 5: Review checkpoint without commit**

  Run git diff --check, git diff --stat, and git status --short. Expected: a feature-local Animals list only; no commit and no push.

### Task 13: Animal form

**Files:**

- Create: src/Frontend/GenSW.Web/src/features/animals/pages/AnimalFormPage.tsx
- Create: src/Frontend/GenSW.Web/src/features/animals/pages/AnimalFormPage.test.tsx

**Interfaces:**

- Consumes: Task 11 client/types; existing Species/Breed/Variety list clients; /animais routes from Task 14.
- Produces: new and edit Animal workflows.

- [ ] **Step 1: Write red form tests**

  Cover:

  - new form with empty untouched CodigoInterno omitting that property and showing the automatic-code hint;
  - a CodigoInterno input that was touched then emptied, or contains only user-entered whitespace, rejected locally rather than silently treated as automatic or omitted;
  - an explicit nonempty manual code sent normally, with manual code normalization, optional null Nome, selectable Sexo/Escopo, and optional date;
  - all active Species/Breed/Variety selector pages being loaded without truncation at 100;
  - a new classification selectable only when active and matching the selected Species;
  - either/both/neither Breed/Variety;
  - edit loading its Animal first and preserving its inactive Species/Breed/Variety as the only retained inactive option;
  - with the retained current Species inactive, a newly selected active matching Raca and independently a newly selected active matching Variedade remaining selectable and valid; a different inactive Species, a new inactive classification, or a classification of another Species remaining unavailable/invalid;
  - Species change retaining incompatible selected IDs in state, visibly warning, blocking submit, and allowing only explicit clear or replacement;
  - 400, 404, and 409 messages that do not display database details; loading, not-found, generic error, retry, and navigation after save.

- [ ] **Step 2: Run the red form test**

  Run:

  ~~~powershell
  npm --prefix src/Frontend/GenSW.Web test -- src/features/animals/pages/AnimalFormPage.test.tsx
  ~~~

  Expected: AnimalFormPage does not exist.

- [ ] **Step 3: Implement the explicit-classification form**

  On edit, load getAnimalById before options. Fetch all active catalog pages for new destinations. Append only a currently linked inactive item as a retained option; never offer any other inactive item as a new destination. When the retained current Species is inactive, continue to offer active Raca and Variedade options whose EspecieId matches that same retained Species, so either classification can be newly selected or replaced without changing Species. Store selected classification option metadata, including its Species ID, so a Species change leaves existing incompatible IDs selected but marked incompatible. Do not set those IDs to null automatically. Block submit until each is null or matches the selected Species, then send the complete PUT snapshot atomically. Preserve Policy B.

  On create, construct a request without codigoInterno only when the input is both empty and untouched. If the user has touched it, reject an empty or whitespace-only value locally; for an explicit nonempty manual code, include codigoInterno normally and let animalsService forward it unchanged. On update require a canonical nonempty code. Normalize text locally for usability but let API remain the authority for UTC date, active-state, and concurrency validation. Use date input without calculating/persisting age.

- [ ] **Step 4: Run the form test green**

  Run the command from Step 2. Expected: manual/automatic code behavior, historical links, Policy B UI, and catalog pagination pass.

- [ ] **Step 5: Review checkpoint without commit**

  Run git diff --check, git diff --stat, and git status --short. Expected: no form library, no silent classification cleanup, and no commit/push.

### Task 14: Protected routes and navigation

**Files:**

- Modify: src/Frontend/GenSW.Web/src/routes/AppRoutes.tsx
- Modify: src/Frontend/GenSW.Web/src/routes/AppRoutes.test.tsx
- Modify: src/Frontend/GenSW.Web/src/features/auth/pages/AuthenticatedHomePage.tsx

**Interfaces:**

- Consumes: AnimalsListPage and AnimalFormPage.
- Produces: protected /animais, /animais/nova, /animais/:id/editar routes and an authenticated Animals menu link.

- [ ] **Step 1: Write red route and menu tests**

  Extend AppRoutes.test.tsx mocks for every animals service function. For each Animal route, assert an anonymous user sees Login and an authenticated user sees the correct heading. Add a home navigation assertion that queries the Cadastros nav for an Animals link with href /animais and clicks it to reach the Animals heading. This is the regression guard against an implemented module being inaccessible from the menu.

- [ ] **Step 2: Run the red routing test**

  Run:

  ~~~powershell
  npm --prefix src/Frontend/GenSW.Web test -- src/routes/AppRoutes.test.tsx
  ~~~

  Expected: Animal routes and navigation do not exist.

- [ ] **Step 3: Register routes and navigation**

  Import the two Animal pages, place all three routes inside ProtectedRoute, and add one Animais link inside the existing authenticated Cadastros nav. Do not change login/session behavior or unrelated route paths.

- [ ] **Step 4: Run the routing test green**

  Run the command from Step 2. Expected: protected routes and visible navigation pass.

- [ ] **Step 5: Review checkpoint without commit**

  Run git diff --check, git diff --stat, and git status --short. Expected: all Animal screens are reachable but no unrelated navigation is changed; no commit and no push.

### Task 15: NA-01 and NA-02 regression gate

**Files:**

- Modify: tests/GenSW.Application.Tests/RacaServiceTests.cs
- Modify: tests/GenSW.Application.Tests/VariedadeServiceTests.cs
- Modify: tests/GenSW.Infrastructure.Tests/RacaPersistenceModelTests.cs
- Modify: tests/GenSW.Infrastructure.Tests/VariedadePersistenceModelTests.cs
- Modify: tests/GenSW.Infrastructure.Tests/RacaRepositoryTests.cs
- Modify: tests/GenSW.Infrastructure.Tests/VariedadeRepositoryTests.cs
- Modify: tests/GenSW.API.Tests/EspeciesApiTests.cs
- Modify: tests/GenSW.API.Tests/RacasApiTests.cs
- Modify: tests/GenSW.API.Tests/VariedadesApiTests.cs

**Interfaces:**

- Consumes: the production protection from Task 4 and structural model from Task 5.
- Produces: evidence that NA-01/NA-02 behavior is retained while the new Animal reference protection is enforced. This is a regression gate, not an implementation task with an artificial red phase.

- [ ] **Step 1: Add or complete focused regression coverage after Tasks 4 and 5 are green**

  Add tests for:

  - Species, Breed, and Variety existing lifecycle behavior and pagination remaining intact;
  - used Raca and used Variedade returning 409 on EspecieId change;
  - the same used records accepting name changes, inactivation, and reactivation;
  - database Restrict/composite keys blocking a direct species reassignment under concurrency;
  - existing no-DELETE behavior still returning 405.

- [ ] **Step 2: Run the NA-01/NA-02 regression gate**

  Run:

  ~~~powershell
  dotnet test tests/GenSW.Application.Tests/GenSW.Application.Tests.csproj --filter "FullyQualifiedName~RacaServiceTests|FullyQualifiedName~VariedadeServiceTests"
  dotnet test tests/GenSW.Infrastructure.Tests/GenSW.Infrastructure.Tests.csproj --filter "FullyQualifiedName~RacaPersistenceModelTests|FullyQualifiedName~VariedadePersistenceModelTests|FullyQualifiedName~RacaRepositoryTests|FullyQualifiedName~VariedadeRepositoryTests"
  dotnet test tests/GenSW.API.Tests/GenSW.API.Tests.csproj --filter "FullyQualifiedName~EspeciesApiTests|FullyQualifiedName~RacasApiTests|FullyQualifiedName~VariedadesApiTests"
  ~~~

  Expected: existing behavior and the new protection assertions pass after owner Tasks 4 and 5 are complete. A newly added regression test that already passes is valid and does not require an artificial red failure.

- [ ] **Step 3: Correct only the owner of a verified gate failure**

  If the gate exposes a defect, change only the Raca/Variedade service, repository, controller, or model/migration artifact owned by Task 4 or Task 5, then return to the focused owner tests and this gate. Do not redesign NA-01/NA-02, introduce a generic classification model, modify the old migrations, or manufacture a red failure.

- [ ] **Step 4: Re-run the NA-01/NA-02 regression gate**

  Run the commands from Step 2. Expected: legacy behavior and new 409 protection both pass.

- [ ] **Step 5: Review checkpoint without commit**

  Run git diff --check, git diff --stat, and git status --short. Expected: regression tests explain every NA-01/NA-02 touch; no commit and no push.

### Task 16: Integrated final gate, scope, and handoff

**Files:**

- Create: no new source file.
- Modify: no file except the owner of a verified gate failure.
- Test: GenSW.sln; frontend suite; the PostgreSQL Animal suites; migration Up/Down tests.

**Interfaces:**

- Consumes: all Tasks 1 through 15.
- Produces: one auditable implementation commit only after every gate passes, then a human-review PR workflow. This is an integrated final gate, not an implementation task with an artificial red phase; a failure returns only to its owning task.

- [ ] **Step 1: Run complete backend checks**

  Run:

  ~~~powershell
  dotnet test GenSW.sln
  dotnet build GenSW.sln --no-restore
  dotnet test tests/GenSW.Infrastructure.Tests/GenSW.Infrastructure.Tests.csproj --filter "FullyQualifiedName~AnimalMigrationTests|FullyQualifiedName~AnimalCodeAllocatorTests|FullyQualifiedName~AnimalRepositoryTests"
  dotnet test tests/GenSW.API.Tests/GenSW.API.Tests.csproj --filter FullyQualifiedName~PostgreSqlAnimalConcurrencyTests
  ~~~

  Expected: all tests pass; PostgreSQL-specific tests may be skipped only by the existing missing-binary detection.

- [ ] **Step 2: Run complete frontend checks**

  Run:

  ~~~powershell
  npm --prefix src/Frontend/GenSW.Web test
  npm --prefix src/Frontend/GenSW.Web run lint
  npm --prefix src/Frontend/GenSW.Web run build
  ~~~

  Expected: all commands exit 0.

- [ ] **Step 3: Inspect migration and scope**

  Run:

  ~~~powershell
  $base = git merge-base main HEAD
  git diff --check "$base...HEAD"
  git diff --name-only "$base...HEAD"
  git diff "$base...HEAD" -- src/Backend/GenSW.Infrastructure/Persistence/Migrations
  git status --short
  ~~~

  Verify manually from the output: no changed historical migration; exactly one new EF-generated <MigrationId>_AddAnimalBase.cs and <MigrationId>_AddAnimalBase.Designer.cs pair, with the snapshot altered; no DELETE Animal; no IRepository<T>; no ClassificacaoAnimal; no MediatR/CQRS; no dependency addition; no .gensw/ or .env.local; no secret; and no source/test implementation of #296 through #299.

- [ ] **Step 4: Create the only implementation commit after every gate**

  Only after Steps 1 through 3 are green:

  ~~~powershell
  git add src/Backend tests src/Frontend/GenSW.Web/src
  git commit -m "feat: add animal base"
  ~~~

  Do not make an intermediate implementation commit, amend a documentation commit, force-push, push main, or merge.

- [ ] **Step 5: Handoff after commit**

  Push feature/295-animal-base, open a PR, wait for CI, record fresh validation evidence in Redmine #295, and move #295 to Em validação with human homologation pending. Do not merge or mark the issue Concluído.

## SPEC to Tasks matrix

Tasks 8, 15, and 16 in this matrix are verification gates. They provide integration, regression, and final evidence after owner tasks; they never require an artificial red result.

| SPEC section | Requirement carried into the plan | Plan task(s) |
| --- | --- | --- |
| 1. Objective | Technical Id, editable operational code, and feature boundary | 1, 2, 16 |
| 2. Scope | Animal base only; no generic repository/classification or future modules | 1, 2, 16 |
| 3. Domain model | Fields, owned enums, local invariants, timestamps, idempotent lifecycle, no navigations | 1 |
| 4. Invariants | Code/manual semantics, Name/date rules, independent classifications, historic inactivation | 1, 3, 9, 10, 11, 13 |
| 5. CodigoInterno and concurrency | PostgreSQL sequence, D6 format, manual collision, five attempts, rollback/disposal, exhaustion | 2, 6, 7, 8, 9, 10 |
| 6. Species/Breed/Variety | Create/update compatibility, Policy B, inactive-history rule, used-classification protection | 3, 4, 5, 8, 9, 13, 15 |
| 7. Lifecycle | Active flag, active/inactive editing, PATCH, and no DELETE | 1, 9, 10, 12, 13, 15 |
| 8. Application | Vertical contracts, validators, service/repository behavior, read summaries, paging | 2, 3, 7, 9 |
| 9. API | Authenticated five-route contract, filters, numeric enums, ProblemDetails | 10 |
| 10. Persistence | Animais schema, checks, functional index, alternate keys, composite FKs, reversible migration | 5, 8 |
| 11. Frontend | Feature module, strict client, list, form, historical selectors, protected routes/menu | 11, 12, 13, 14 |
| 12. Errors and concurrent scenarios | 400/404/409 mappings and all named race/error outcomes | 4, 7, 8, 9, 10, 15 |
| 13. Test strategy | Domain, Application, Infrastructure/PostgreSQL, API, frontend, and regression evidence | 1, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 |
| 14. Out of scope | Exclusion of #296 through #299 and other deferred animal data | 16 |
| 15. Future extensibility | Id is durable; CodigoInterno is not a future FK; no future structures now | 1, 2, 16 |
| 16. Architectural decisions | Approved sequence, composite-FK, independent classification, Policy B, lifecycle/API conventions | 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 |

## Human homologation after PR and CI

The developer, not Codex, performs this after the implementation PR is available:

1. Create a minimum Animal with CodigoInterno omitted and confirm the generated code.
2. Create an Animal with a manual code, then edit that code.
3. Create/edit with Nome absent and with a normalized Nome.
4. Exercise Macho, Femea, Indeterminado, Operacional, Referencia, and optional DataNascimento.
5. Create with Species alone, with Breed alone, with Variety alone, and with both classifications.
6. Attempt incompatible Breed/Variety combinations and observe field-oriented validation.
7. Edit historical inactive Species/Breed/Variety links; confirm they remain visible but another inactive item cannot be selected. While retaining an inactive current Species, select an active matching Breed and independently an active matching Variety; confirm a different inactive Species and an inactive or incompatible new classification remain unavailable.
8. Change Species with a retained incompatible classification; confirm the UI requires an explicit clear/replacement and does not erase it silently.
9. Inactivate/reactivate an Animal and verify it remains editable and listable by filters.
10. Check case-insensitive search by code/name, all filters, sorting, page sizes, and pagination.
11. Reach Animals from the authenticated menu and each direct route.
12. Regress Species, Racas, and Variedades: name/lifecycle changes remain valid, but moving a referenced Breed/Variety to another Species yields 409.
13. Confirm no Animal DELETE operation is exposed.

Concurrency is accepted primarily from the automated real-PostgreSQL suite, not from manual browser timing.

## Plan self-review record

- SPEC coverage: every section maps to one or more concrete tasks in the matrix above.
- Interface consistency: the Cross-task contracts define names/signatures consumed by subsequent tasks; AnimalDuplicateException carries CodigoInterno and structured source provenance, and the transaction attempt explicitly owns begin, allocation, commit, rollback, detach, and disposal.
- Retry boundary: defined as exactly five total attempts and uses a new PostgreSQL transaction after every qualifying persisted named-constraint collision for the matching automatic candidate; no retry decision depends on exception text.
- Gate classification: Tasks 8, 15, and 16 are PostgreSQL integration, NA-01/NA-02 regression, and integrated final gates; each accepts already-passing new coverage and sends a real failure only to its owner.
- Paths: every source/test path is concrete; the migration uses the exact EF-generated <MigrationId>_AddAnimalBase.cs and <MigrationId>_AddAnimalBase.Designer.cs pair and preserves normal generated MigrationAttribute conventions without manual renaming.
- Scope: this plan contains no implementation of #296, #297, #298, or #299 and authorizes no implementation before human plan review.
