using System.Diagnostics;
using System.Numerics;
using static GenSW.Domain.Optimization.OptimizationModel;

namespace GenSW.Domain.Optimization;

public sealed class OptimizationEngine(IOptimizationSolver solver)
{
    public const string ModelVersion = "f05-n1/1";
    public const int MaxIngredients = 50;
    public const int MaxGoals = 50;
    public static readonly TimeSpan Budget = TimeSpan.FromSeconds(5);

    private const int Microkg = 1_000_000;
    private const decimal MaxMassKg = 99999999999999.999999m;
    private static readonly Rational ActivityTolerance = Rational.Parse("0.00000001");

    public async Task<OptimizationResult> EvaluateAsync(OptimizationProblem problem, CancellationToken cancellationToken = default)
    {
        var started = Stopwatch.GetTimestamp();
        var invalid = Validate(problem);
        if (invalid is not null)
            return invalid;

        var ingredients = problem.Ingredients.Where(x => x.MaximumPercent > 0)
            .OrderBy(x => x.Id.ToString("D"), StringComparer.Ordinal).ToArray();
        var goals = problem.Goals.OrderBy(x => x.Id.ToString("D"), StringComparer.Ordinal).ToArray();
        var mass = Rational.FromDecimal(problem.MassKg);
        var excludedDeviation = Sum(problem.Ingredients.Where(x => x.MaximumPercent == 0).Select(x => Rational.FromDecimal(x.ReferenceKg)));
        var model = OptimizationModel.Create(mass, ingredients, goals, excludedDeviation);
        var nativeUsable = NativeModelPreservesCoefficients(model);
        if (!nativeUsable && (model.Native.UpperBounds.Any(x => !double.IsFinite(x)) ||
            model.Native.Coefficients.Any(row => row.Any(x => !double.IsFinite(x)))))
            return new(OptimizationOutcome.DadosIncompativeis, "EscalaNumericaNaoSuportada");

        var proof = OptimizationInfeasibility.FindAnalyticProof(model, ingredients, goals);
        if (Stopwatch.GetElapsedTime(started) >= Budget)
            return new(OptimizationOutcome.LimiteTempo, "OrcamentoEsgotado", Model: model);
        if (proof is not null)
            return new(OptimizationOutcome.InviabilidadeDemonstrada, proof.Code, Model: model, Proof: proof);
        if (!nativeUsable)
            return new(OptimizationOutcome.DadosIncompativeis, "EscalaNumericaNaoSuportada", Model: model);

        cancellationToken.ThrowIfCancellationRequested();
        var remaining = Budget - Stopwatch.GetElapsedTime(started);
        if (remaining <= TimeSpan.Zero)
            return new(OptimizationOutcome.LimiteTempo, "OrcamentoEsgotado", Model: model);

        var raw = await solver.SolveAsync(model.Native, remaining, cancellationToken);
        var native = CleanForStorage(raw);
        if (raw.Status == OptimizationNativeStatus.Error)
            return new(OptimizationOutcome.ErroAlgoritmo, raw.Code, Model: model, Native: native);
        if (raw.Status == OptimizationNativeStatus.Unbounded)
            return new(OptimizationOutcome.ErroAlgoritmo, "ModeloIlimitado", Model: model, Native: native);

        if (raw.Status == OptimizationNativeStatus.TimeLimit || Stopwatch.GetElapsedTime(started) >= Budget)
        {
            var evaluation = EvaluateCandidate(problem, model, ingredients, goals, raw);
            return Timeout(model, native, raw.Status == OptimizationNativeStatus.TimeLimit ? raw.Code : "OrcamentoEsgotado", evaluation);
        }

        if (raw.Status == OptimizationNativeStatus.Infeasible)
        {
            proof = OptimizationInfeasibility.VerifyDualRay(model, raw.DualRay);
            if (Stopwatch.GetElapsedTime(started) >= Budget)
                return Timeout(model, native, "OrcamentoEsgotado");

            return proof is null
                ? new(OptimizationOutcome.Inconclusivo, "InviabilidadeNaoCertificada", Model: model, Native: native)
                : new(OptimizationOutcome.InviabilidadeDemonstrada, proof.Code, Model: model, Native: native, Proof: proof);
        }

        if (raw.Status is OptimizationNativeStatus.IterationLimit or OptimizationNativeStatus.Ambiguous)
            return new(OptimizationOutcome.Inconclusivo, raw.Code, Model: model, Native: native);

        var result = EvaluateCandidate(problem, model, ingredients, goals, raw);
        return Stopwatch.GetElapsedTime(started) >= Budget
            ? Timeout(model, native, "OrcamentoEsgotado", result)
            : result with { Model = model, Native = native };
    }

    private static OptimizationResult? Validate(OptimizationProblem problem)
    {
        if (problem.MassKg <= 0 || problem.MassKg > MaxMassKg || !HasSixPlaces(problem.MassKg))
            return new(OptimizationOutcome.DadosIncompativeis, "MassaInvalida");
        if (problem.Goals.Count == 0 || problem.Goals.Count > MaxGoals ||
            problem.Ingredients.Count(x => x.MaximumPercent > 0) > MaxIngredients)
            return new(OptimizationOutcome.DadosIncompativeis, "DimensaoModeloInvalida");
        if (problem.Ingredients.Any(x => x.Id == Guid.Empty) || problem.Goals.Any(x => x.Id == Guid.Empty) ||
            problem.Ingredients.Select(x => x.Id).Distinct().Count() != problem.Ingredients.Count ||
            problem.Goals.Select(x => x.Id).Distinct().Count() != problem.Goals.Count)
            return new(OptimizationOutcome.DadosIncompativeis, "IdentificadoresInvalidos");

        foreach (var ingredient in problem.Ingredients)
        {
            if (ingredient.ReferenceKg < 0 || ingredient.ReferenceKg > problem.MassKg || !HasSixPlaces(ingredient.ReferenceKg))
                return new(OptimizationOutcome.DadosIncompativeis, "ReferenciaInvalida");
            if (ingredient.MinimumPercent < 0 || ingredient.MaximumPercent > 100 || ingredient.MinimumPercent > ingredient.MaximumPercent ||
                !HasSixPlaces(ingredient.MinimumPercent) || !HasSixPlaces(ingredient.MaximumPercent))
                return new(OptimizationOutcome.DadosIncompativeis, "IntervaloInclusaoInvalido");
        }

        if (Sum(problem.Ingredients.Select(x => Rational.FromDecimal(x.ReferenceKg))) != Rational.FromDecimal(problem.MassKg))
            return new(OptimizationOutcome.DadosIncompativeis, "ReferenciaNaoSomaMassa");

        foreach (var goal in problem.Goals)
        {
            if ((goal.Minimum is null && goal.Maximum is null) || goal.Minimum is { } min && min < 0 ||
                goal.Maximum is { } max && max < 0 || goal.Minimum is { } lower && goal.Maximum is { } upper && lower > upper ||
                string.IsNullOrWhiteSpace(goal.Unit))
                return new(OptimizationOutcome.DadosIncompativeis, "IntervaloMetaInvalido");

            foreach (var ingredient in problem.Ingredients.Where(x => x.MaximumPercent > 0))
            {
                if (!goal.Coefficients.TryGetValue(ingredient.Id, out var coefficient))
                    return new(OptimizationOutcome.DadosIncompletos, "CoeficienteAusente");
                if (coefficient < 0)
                    return new(OptimizationOutcome.DadosIncompativeis, "CoeficienteNegativo");
            }
        }
        return null;
    }

    private static OptimizationResult EvaluateCandidate(OptimizationProblem problem, OptimizationLinearModel model,
        OptimizationIngredient[] ingredients, OptimizationGoal[] goals, OptimizationSolverResult raw)
    {
        if (raw.Columns is not { } columns || columns.Length != model.Native.Costs.Length || columns.Any(x => !double.IsFinite(x)))
            return new(OptimizationOutcome.ErroAlgoritmo, "VetorNativoInvalido");
        if (columns.Take(ingredients.Length).Any(x => x < 0))
            return new(OptimizationOutcome.ErroAlgoritmo, "MassaNegativa");
        if (raw.Objective is { } objective)
        {
            var expected = model.Native.Costs.Zip(columns, (cost, x) => cost * x).Sum() + model.Native.Offset;
            if (!double.IsFinite(objective) || !double.IsFinite(expected) || Math.Abs(objective - expected) > SolverTolerance)
                return new(OptimizationOutcome.ErroAlgoritmo, "ObjetivoNativoInvalido");
        }

        for (var rowIndex = 0; rowIndex < model.Native.Coefficients.Length; rowIndex++)
        {
            var row = model.Native.Coefficients[rowIndex];
            var value = row.Zip(columns, (a, x) => a * x).Sum();
            if (!double.IsFinite(value) || value - model.Native.UpperBounds[rowIndex] > SolverTolerance)
                return new(OptimizationOutcome.ErroAlgoritmo, "ResiduoNativoInvalido");
        }

        var original = problem.Ingredients.OrderBy(x => x.Id.ToString("D"), StringComparer.Ordinal).ToArray();
        var masses = original.ToDictionary(x => x.Id, _ => (Rational)0);
        for (var i = 0; i < ingredients.Length; i++)
            masses[ingredients[i].Id] = Rational.FromDouble(columns[i]) * model.MassKg;

        var quantized = Quantize(model.MassKg, masses, ingredients);
        var candidate = Describe(model.MassKg, original, goals, masses, quantized, raw.Status);
        return quantized is null || candidate.Constraints.Any(x => x.Violated)
            ? new(OptimizationOutcome.Inconclusivo, "PrecisaoIncompativel", Candidate: candidate)
            : new(OptimizationOutcome.SolucaoViavel, "CandidatoRevalidado", Candidate: candidate);
    }

    private static Dictionary<Guid, Rational>? Quantize(Rational mass, Dictionary<Guid, Rational> raw, OptimizationIngredient[] ingredients)
    {
        var target = mass * Microkg;
        var floors = raw.ToDictionary(x => x.Key, x => (x.Value * Microkg).Floor());
        var residual = target - new Rational(floors.Values.Aggregate(BigInteger.Zero, (sum, value) => sum + value), BigInteger.One);
        var eligible = ingredients.Select(x => (x.Id, Remainder: raw[x.Id] * Microkg - new Rational(floors[x.Id], BigInteger.One)))
            .Where(x => x.Remainder > 0).OrderByDescending(x => x.Remainder)
            .ThenBy(x => x.Id.ToString("D"), StringComparer.Ordinal).ToArray();
        if (!residual.Denominator.IsOne || residual < 0 || residual > eligible.Length)
            return null;

        foreach (var row in eligible.Take((int)residual.Numerator))
            floors[row.Id]++;
        return floors.ToDictionary(x => x.Key, x => new Rational(x.Value, Microkg));
    }

    private static OptimizationCandidate Describe(Rational mass, OptimizationIngredient[] ingredients, OptimizationGoal[] goals,
        Dictionary<Guid, Rational> raw, Dictionary<Guid, Rational>? quantized, OptimizationNativeStatus status)
    {
        var described = quantized ?? raw;
        var constraints = new List<OptimizationConstraint> { Constraint("Massa", "Massa", null, "kg BN", Sum(described.Values), mass, mass) };
        var contributions = new List<OptimizationContribution>();
        foreach (var ingredient in ingredients)
        {
            constraints.Add(Constraint($"Inclusao:{ingredient.Id:D}", "Inclusao", ingredient.Id, "%", described[ingredient.Id] * 100 / mass,
                Rational.FromDecimal(ingredient.MinimumPercent), Rational.FromDecimal(ingredient.MaximumPercent)));
        }

        foreach (var goal in goals)
        {
            Rational total = 0;
            var unit = goal.Unit.Split('/')[0];
            foreach (var ingredient in ingredients)
            {
                // Excluded ingredients can have no coefficient and contribute exactly zero mass.
                var value = ingredient.MaximumPercent == 0 ? default : goal.Coefficients[ingredient.Id] * described[ingredient.Id];
                total += value;
                contributions.Add(new(goal.Id, ingredient.Id, value.ToDecimalString(18), unit));
            }
            constraints.Add(Constraint($"Meta:{goal.Id:D}", "Meta", goal.Id, goal.Unit, total / mass, goal.Minimum, goal.Maximum));
        }

        Rational Deviation(Dictionary<Guid, Rational> candidate) => Sum(ingredients.Select(x => (candidate[x.Id] - Rational.FromDecimal(x.ReferenceKg)).Abs()));
        var rawDeviation = Deviation(raw);
        var finalDeviation = quantized is null ? (Rational?)null : Deviation(quantized);
        var changedByQuantization = finalDeviation is not null && finalDeviation != rawDeviation && !constraints.Any(x => x.Violated);
        var quality = status != OptimizationNativeStatus.Optimal ? OptimizationObjectiveQuality.ViavelSemProvaDeOtimo
            : changedByQuantization ? OptimizationObjectiveQuality.ViavelAposQuantizacao
            : OptimizationObjectiveQuality.OtimoNumericoContinuo;

        return new OptimizationCandidate(raw.ToDictionary(x => x.Key, x => x.Value.ToDecimalString(28)),
            quantized?.ToDictionary(x => x.Key, x => x.Value.ToDecimalString(6)),
            quantized?.ToDictionary(x => x.Key, x => (raw[x.Key] - x.Value).ToDecimalString(28)),
            rawDeviation.ToDecimalString(28), finalDeviation?.ToDecimalString(6),
            (finalDeviation / 2)?.ToDecimalString(7) ?? (rawDeviation / 2).ToDecimalString(28),
            constraints, contributions, quality);
    }

    private static OptimizationConstraint Constraint(string id, string kind, Guid? referenceId, string unit, Rational value,
        Rational? minimum, Rational? maximum)
    {
        var lowerSlack = value - minimum;
        var upperSlack = maximum - value;
        var active = lowerSlack is { IsZero: true } || upperSlack is { IsZero: true };
        var scale = new[] { (Rational)1, value.Abs(), minimum?.Abs() ?? 0, maximum?.Abs() ?? 0 }.Max();
        var tolerance = ActivityTolerance * scale;
        var near = !active && (lowerSlack?.Abs() <= tolerance || upperSlack?.Abs() <= tolerance);
        return new(id, kind, referenceId, unit, value.ToDecimalString(18), minimum?.ToDecimalString(18), maximum?.ToDecimalString(18),
            lowerSlack?.ToDecimalString(18), upperSlack?.ToDecimalString(18), active, near, lowerSlack < 0 || upperSlack < 0,
            tolerance.ToDecimalString(18));
    }

    private static OptimizationSolverResult CleanForStorage(OptimizationSolverResult raw) => raw with
    {
        Columns = raw.Columns?.All(double.IsFinite) == true ? raw.Columns : null,
        DualRay = raw.DualRay?.All(double.IsFinite) == true ? raw.DualRay : null,
        Objective = raw.Objective is { } objective && double.IsFinite(objective) ? objective : null,
    };

    private static OptimizationResult Timeout(OptimizationLinearModel model, OptimizationSolverResult native, string code,
        OptimizationResult? evaluation = null) => new(OptimizationOutcome.LimiteTempo, code,
        evaluation is { Outcome: OptimizationOutcome.SolucaoViavel, Candidate: { } candidate }
            ? candidate with { ObjectiveQuality = OptimizationObjectiveQuality.ViavelSemProvaDeOtimo }
            : null, model, native);

    private static bool HasSixPlaces(decimal value) => decimal.Round(value, 6) == value;
}
