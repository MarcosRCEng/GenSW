using GenSW.Domain.Optimization;
using Xunit;

namespace GenSW.Domain.Tests.Optimization;

public sealed class OptimizationEngineTests
{
    private static readonly Guid A = Guid.Parse("00000000-0000-4000-8000-000000000001");
    private static readonly Guid B = Guid.Parse("00000000-0000-4000-8000-000000000002");
    private static readonly Guid C = Guid.Parse("00000000-0000-4000-8000-000000000003");
    private static readonly Guid Protein = Guid.Parse("00000000-0000-4000-8000-000000000101");
    private static readonly Guid Energy = Guid.Parse("00000000-0000-4000-8000-000000000102");
    private static readonly Guid Fiber = Guid.Parse("00000000-0000-4000-8000-000000000103");

    [Theory]
    [InlineData(60, "20")]
    [InlineData(30, "40")]
    [InlineData(50, "0")]
    public async Task AdjustsTheAbBaselineAgainstAllHardGoals(int baselineA, string deviation)
    {
        var t = Math.Abs(50 - baselineA) / 100.0;
        var engine = Engine(_ => new(OptimizationNativeStatus.Optimal, "Otimo", Columns: [0.5, 0.5, t, t]));

        var result = await engine.EvaluateAsync(Ab(baselineA));

        Assert.Equal(OptimizationOutcome.SolucaoViavel, result.Outcome);
        var candidate = Assert.IsType<OptimizationCandidate>(result.Candidate);
        Assert.Equal("50", candidate.QuantizedMasses![A]);
        Assert.Equal("50", candidate.QuantizedMasses[B]);
        Assert.Equal(deviation, candidate.QuantizedDeviationKg);
        Assert.All(candidate.Constraints.Where(x => x.Kind == "Meta"), x => Assert.True(x.Active));
        Assert.Contains(candidate.Contributions, x => x.IngredientId == A && x.GoalId == Protein && x.Value == "5000");
        Assert.Contains(candidate.Contributions, x => x.IngredientId == B && x.GoalId == Protein && x.Value == "20000");
        Assert.Contains(candidate.Contributions, x => x.IngredientId == A && x.GoalId == Energy && x.Value == "600");
        Assert.Contains(candidate.Contributions, x => x.IngredientId == B && x.GoalId == Fiber && x.Value == "4000");
    }

    [Fact]
    public async Task ProvesContradictoryEnergyAndProteinInTheTwoIngredientModel()
    {
        var problem = Ab();
        problem = problem with { Goals = [problem.Goals[0], problem.Goals[1] with { Minimum = Rational.FromDecimal(10.1m) }] };

        var result = await WithoutSolver().EvaluateAsync(problem);

        Assert.Equal(OptimizationOutcome.InviabilidadeDemonstrada, result.Outcome);
        Assert.Equal("IntervaloContraditorio", result.Proof!.Code);
        Assert.Contains("minimoKg=50", result.Proof.Details);
        Assert.Contains("maximoKg=95/2", result.Proof.Details);
    }

    [Theory]
    [InlineData(450, 100, "maximoAtingivel=400")]
    [InlineData(250, 20, "maximoAtingivel=160")]
    public async Task ProvesAnAttainableNutrientUpperBoundWithInclusionLimits(int minimum, int maximumB, string expected)
    {
        var problem = Ab();
        problem = problem with
        {
            Ingredients = [problem.Ingredients[0], problem.Ingredients[1] with { MaximumPercent = maximumB }],
            Goals = [problem.Goals[0] with { Minimum = minimum }],
        };

        var result = await WithoutSolver().EvaluateAsync(problem);

        Assert.Equal(OptimizationOutcome.InviabilidadeDemonstrada, result.Outcome);
        Assert.Contains(expected, result.Proof!.Details);
    }

    [Fact]
    public async Task DistinguishesInvalidIndividualIntervalsFromInfeasibleAggregateLimits()
    {
        var problem = Ab();
        var invalid = await WithoutSolver().EvaluateAsync(problem with
        {
            Ingredients = [problem.Ingredients[0], new(B, 40, 30, 20)],
        });
        Assert.Equal(OptimizationOutcome.DadosIncompativeis, invalid.Outcome);
        Assert.Equal("IntervaloInclusaoInvalido", invalid.Code);

        var impossible = await WithoutSolver().EvaluateAsync(problem with { Ingredients = [new(A, 60, 60, 100), new(B, 40, 60, 100)] });
        Assert.Equal(OptimizationOutcome.InviabilidadeDemonstrada, impossible.Outcome);
        Assert.Contains("somaMinimaKg=120", impossible.Proof!.Details);
    }

    [Fact]
    public async Task RequiresCoefficientsUnlessAnIngredientWasExplicitlyExcluded()
    {
        var goal = new OptimizationGoal(Protein, 100, null, "g/kg BN", new Dictionary<Guid, Rational> { [A] = 100 });
        var missing = await WithoutSolver().EvaluateAsync(new(100, [new(A, 60, 0, 100), new(B, 40, 0, 100)], [goal]));
        Assert.Equal(OptimizationOutcome.DadosIncompletos, missing.Outcome);

        var engine = Engine(_ => new(OptimizationNativeStatus.Optimal, "Otimo", Columns: [1, 0.4]));
        var excluded = await engine.EvaluateAsync(new(100, [new(A, 60, 0, 100), new(B, 40, 0, 0)], [goal]));
        Assert.Equal(OptimizationOutcome.SolucaoViavel, excluded.Outcome);
        Assert.Equal("80", excluded.Candidate!.QuantizedDeviationKg);
        Assert.Equal("0", excluded.Candidate.QuantizedMasses![B]);
        Assert.Equal([A], excluded.Model!.IngredientIds);
    }

    [Fact]
    public async Task RefusesScaleWhenNativeMatrixWouldEraseAKnownCoefficient()
    {
        var goal = new OptimizationGoal(Protein, 0, null, "g/kg BN", new Dictionary<Guid, Rational>
        {
            [A] = Rational.Parse("0.00000000000001"), [B] = 1,
        });

        var result = await WithoutSolver().EvaluateAsync(Ab() with { Goals = [goal] });

        Assert.Equal(OptimizationOutcome.DadosIncompativeis, result.Outcome);
        Assert.Equal("EscalaNumericaNaoSuportada", result.Code);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BalancedThirdsStillFailAnExactNutrientMinimum(bool constrainFiber)
    {
        var goals = new List<OptimizationGoal> { Goal(Protein, 200, null, 100, 400, "g/kg BN") };
        if (constrainFiber)
            goals.Add(Goal(Fiber, null, 40, 20, 80, "g/kg BN"));
        var engine = Engine(_ => new(OptimizationNativeStatus.Optimal, "Otimo", Columns: [2.0 / 3, 1.0 / 3, 1.0 / 3, 1.0 / 3]));

        var result = await engine.EvaluateAsync(new(1, [new(A, 1, 0, 100), new(B, 0, 0, 100)], goals));

        Assert.Equal(OptimizationOutcome.Inconclusivo, result.Outcome);
        Assert.Equal("PrecisaoIncompativel", result.Code);
        Assert.Equal("0.666667", result.Candidate!.QuantizedMasses![A]);
        Assert.Equal("0.333333", result.Candidate.QuantizedMasses[B]);
        var protein = Assert.Single(result.Candidate.Constraints, x => x.ReferenceId == Protein);
        Assert.True(protein.Violated);
        Assert.Equal("199.9999", protein.Value);
    }

    [Fact]
    public async Task DoesNotRedistributeMoreThanOneMicrokgPerEligibleLine()
    {
        const decimal mass = 1_000_000_000;
        var problem = new OptimizationProblem(mass, [new(A, mass / 2, 0, 100), new(B, mass / 2, 0, 100)],
            [Goal(Protein, 0, null, 100, 400, "g/kg BN")]);
        var engine = Engine(_ => new(OptimizationNativeStatus.Optimal, "Otimo", Columns: [0.4999999995, 0.5, 0.001, 0.001]));

        var result = await engine.EvaluateAsync(problem);

        Assert.Equal(OptimizationOutcome.Inconclusivo, result.Outcome);
        Assert.Equal("PrecisaoIncompativel", result.Code);
        Assert.Null(result.Candidate!.QuantizedMasses);
    }

    [Fact]
    public async Task BreaksQuantizationRemainderTiesByOrdinalIdWithNoPreferenceInTheLp()
    {
        var goals = new[] { new OptimizationGoal(Protein, 0, null, "g/kg BN", new Dictionary<Guid, Rational> { [A] = 0, [B] = 0, [C] = 0 }) };
        var engine = Engine(_ => new(OptimizationNativeStatus.Feasible, "Viavel", Columns: [1.0 / 3, 1.0 / 3, 1.0 / 3, 2.0 / 3, 1.0 / 3, 1.0 / 3]));

        var result = await engine.EvaluateAsync(new(1, [new(C, 0, 0, 100), new(B, 0, 0, 100), new(A, 1, 0, 100)], goals));

        Assert.Equal(OptimizationOutcome.SolucaoViavel, result.Outcome);
        Assert.Equal("0.333334", result.Candidate!.QuantizedMasses![A]);
        Assert.Equal("0.333333", result.Candidate.QuantizedMasses[B]);
        Assert.Equal("0.333333", result.Candidate.QuantizedMasses[C]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task KeepsTimeoutEvenWithAValidIncumbent(bool withIncumbent)
    {
        var engine = Engine(_ => new(OptimizationNativeStatus.TimeLimit, "LimiteTempoNativo",
            Columns: withIncumbent ? [0.5, 0.5, 0.1, 0.1] : null));

        var result = await engine.EvaluateAsync(Ab());

        Assert.Equal(OptimizationOutcome.LimiteTempo, result.Outcome);
        Assert.Equal(withIncumbent, result.Candidate is not null);
        if (withIncumbent)
            Assert.Equal(OptimizationObjectiveQuality.ViavelSemProvaDeOtimo, result.Candidate!.ObjectiveQuality);
    }

    [Theory]
    [InlineData(double.NaN, "VetorNativoInvalido")]
    [InlineData(-1e-12, "MassaNegativa")]
    [InlineData(0.2, "ResiduoNativoInvalido")]
    public async Task RejectsInvalidNativeVectors(double column, string expected)
    {
        var result = await Engine(_ => new(OptimizationNativeStatus.Optimal, "Otimo", Columns: [column, 0.5, 0.1, 0.1])).EvaluateAsync(Ab());

        Assert.Equal(OptimizationOutcome.ErroAlgoritmo, result.Outcome);
        Assert.Equal(expected, result.Code);
        Assert.Null(result.Candidate);
    }

    [Theory]
    [InlineData(-1, OptimizationOutcome.InviabilidadeDemonstrada)]
    [InlineData(-0.9, OptimizationOutcome.Inconclusivo)]
    [InlineData(0, OptimizationOutcome.Inconclusivo)]
    public async Task RequiresTheDualRayToProveInfeasibilityInTheRationalModel(double multiplier, OptimizationOutcome expected)
    {
        var goals = new[]
        {
            new OptimizationGoal(Protein, Rational.FromDecimal(0.75m), null, "g/kg BN", new Dictionary<Guid, Rational> { [A] = 1, [B] = 1, [C] = 0 }),
            new OptimizationGoal(Energy, Rational.FromDecimal(0.75m), null, "MJ/kg BN", new Dictionary<Guid, Rational> { [A] = 0, [B] = 0, [C] = 1 }),
        };
        var engine = Engine(model =>
        {
            var ray = new double[model.UpperBounds.Length];
            ray[0] = ray[^2] = -1;
            ray[^1] = multiplier;
            return new(OptimizationNativeStatus.Infeasible, "Inviavel", DualRay: multiplier == 0 ? null : ray);
        });

        var result = await engine.EvaluateAsync(new(1, [new(A, 1, 0, 100), new(B, 0, 0, 100), new(C, 0, 0, 100)], goals));

        Assert.Equal(expected, result.Outcome);
        if (expected == OptimizationOutcome.InviabilidadeDemonstrada)
            Assert.Equal(3, result.Proof!.Multipliers!.Count);
        else
            Assert.Equal("InviabilidadeNaoCertificada", result.Code);
    }

    private static OptimizationProblem Ab(decimal baselineA = 60) => new(100,
        [new(A, baselineA, 0, 100), new(B, 100 - baselineA, 0, 100)],
        [Goal(Protein, 250, null, 100, 400, "g/kg BN"), Goal(Energy, 10, null, 12, 8, "MJ/kg BN"), Goal(Fiber, null, 50, 20, 80, "g/kg BN")]);

    private static OptimizationGoal Goal(Guid id, Rational? min, Rational? max, Rational coefficientA, Rational coefficientB, string unit) =>
        new(id, min, max, unit, new Dictionary<Guid, Rational> { [A] = coefficientA, [B] = coefficientB });

    private static OptimizationEngine WithoutSolver() => Engine(_ => throw new InvalidOperationException("Solver must not run."));
    private static OptimizationEngine Engine(Func<OptimizationNativeModel, OptimizationSolverResult> solve) => new(new StubSolver(solve));

    private sealed class StubSolver(Func<OptimizationNativeModel, OptimizationSolverResult> solve) : IOptimizationSolver
    {
        public Task<OptimizationSolverResult> SolveAsync(OptimizationNativeModel model, TimeSpan budget, CancellationToken cancellationToken) =>
            Task.FromResult(solve(model));
    }
}
