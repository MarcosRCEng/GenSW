using System.Text.Json.Serialization;

namespace GenSW.Domain.Optimization;

public sealed record OptimizationIngredient(Guid Id, decimal ReferenceKg, decimal MinimumPercent, decimal MaximumPercent);

public sealed record OptimizationGoal(
    Guid Id, Rational? Minimum, Rational? Maximum, string Unit, IReadOnlyDictionary<Guid, Rational> Coefficients);

public sealed record OptimizationProblem(decimal MassKg, IReadOnlyList<OptimizationIngredient> Ingredients, IReadOnlyList<OptimizationGoal> Goals);

[JsonConverter(typeof(JsonStringEnumConverter<OptimizationOutcome>))]
public enum OptimizationOutcome
{
    SolucaoViavel,
    InviabilidadeDemonstrada,
    DadosIncompletos,
    DadosIncompativeis,
    LimiteTempo,
    ErroAlgoritmo,
    Inconclusivo,
}

[JsonConverter(typeof(JsonStringEnumConverter<OptimizationNativeStatus>))]
public enum OptimizationNativeStatus
{
    Optimal,
    Feasible,
    Infeasible,
    TimeLimit,
    IterationLimit,
    Ambiguous,
    Unbounded,
    Error,
}

[JsonConverter(typeof(JsonStringEnumConverter<OptimizationObjectiveQuality>))]
public enum OptimizationObjectiveQuality
{
    OtimoNumericoContinuo,
    ViavelSemProvaDeOtimo,
    ViavelAposQuantizacao,
}

public sealed record OptimizationInequality(string Id, Rational[] Coefficients, Rational UpperBound, Rational Scale);

public sealed record OptimizationNativeModel(double[] Costs, double[][] Coefficients, double[] UpperBounds, double SmallMatrixValue, double Offset = 0);

public sealed record OptimizationLinearModel(
    string Version,
    Rational MassKg,
    Guid[] IngredientIds,
    Guid[] GoalIds,
    OptimizationInequality[] Inequalities,
    OptimizationNativeModel Native,
    Rational ExcludedDeviationKg);

public sealed record OptimizationSolverMetadata(
    string Version,
    string BinarySha256,
    string Runtime,
    double Seconds,
    double BudgetSeconds,
    string Algorithm,
    int Threads,
    int Seed,
    double PrimalTolerance,
    double DualTolerance,
    double SmallMatrixValue,
    int HighsIntBytes = 4,
    double? NativeTimeLimitSeconds = null);

public sealed record OptimizationSolverResult(
    OptimizationNativeStatus Status,
    string Code,
    string? RawStatus = null,
    double[]? Columns = null,
    double[]? DualRay = null,
    OptimizationSolverMetadata? Metadata = null,
    double? Objective = null);

public interface IOptimizationSolver
{
    Task<OptimizationSolverResult> SolveAsync(OptimizationNativeModel model, TimeSpan budget, CancellationToken cancellationToken);
}

public sealed record OptimizationSolverRequest(OptimizationNativeModel Model, double BudgetSeconds);

public sealed record OptimizationConstraint(
    string Id,
    string Kind,
    Guid? ReferenceId,
    string Unit,
    string Value,
    string? Minimum,
    string? Maximum,
    string? LowerSlack,
    string? UpperSlack,
    bool Active,
    bool NearBoundary,
    bool Violated,
    string ActivityTolerance);

public sealed record OptimizationContribution(Guid GoalId, Guid IngredientId, string Value, string Unit);

public sealed record OptimizationCandidate(
    IReadOnlyDictionary<Guid, string> RawMasses,
    IReadOnlyDictionary<Guid, string>? QuantizedMasses,
    IReadOnlyDictionary<Guid, string>? ResidualsKg,
    string RawDeviationKg,
    string? QuantizedDeviationKg,
    string? RedistributedKg,
    IReadOnlyList<OptimizationConstraint> Constraints,
    IReadOnlyList<OptimizationContribution> Contributions,
    OptimizationObjectiveQuality ObjectiveQuality);

public sealed record OptimizationProof(string Code, string[] ConstraintIds, string[] Details, IReadOnlyDictionary<string, Rational>? Multipliers = null);

public sealed record OptimizationResult(
    OptimizationOutcome Outcome,
    string Code,
    OptimizationCandidate? Candidate = null,
    OptimizationLinearModel? Model = null,
    OptimizationSolverResult? Native = null,
    OptimizationProof? Proof = null);
