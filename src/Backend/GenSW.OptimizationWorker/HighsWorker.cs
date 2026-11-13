using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using GenSW.Domain.Optimization;

namespace GenSW.OptimizationWorker;

internal static class HighsWorker
{
    internal const string ExpectedVersion = "1.15.1";
    private const int Threads = 1;
    private const int Seed = 0;
    private const double FeasibilityTolerance = 1e-8;
    private const double SmallMatrixValue = 1e-12;
    private const int Rowwise = 2;
    private const int Minimize = 1;
    private const int FeasiblePrimal = 2;
    private const string Algorithm = "simplex";

    internal static OptimizationSolverResult Solve(OptimizationSolverRequest request)
    {
        var started = Stopwatch.GetTimestamp();
        var model = request.Model;
        if (!double.IsFinite(request.BudgetSeconds) || request.BudgetSeconds <= 0 || request.BudgetSeconds > 5 ||
            !ValidModel(model))
            return new(OptimizationNativeStatus.Error, "RequisicaoNativaInvalida");

        if (FindVerifiedLibrary() is not { } binary)
            return new(OptimizationNativeStatus.Error, "BinarioNativoNaoVerificado");

        var version = Marshal.PtrToStringUTF8(HighsNative.Version());
        if (version != ExpectedVersion)
            return new(OptimizationNativeStatus.Error, "VersaoNativaIncompativel");

        var solver = HighsNative.Create();
        if (solver == 0)
            return new(OptimizationNativeStatus.Error, "FalhaCriacaoSolver");

        try
        {
            var integerBytes = HighsNative.GetSizeofHighsInt(solver);
            if (integerBytes != sizeof(int))
                return new(OptimizationNativeStatus.Error, "AbiNativaIncompativel");

            if (!Configure(solver, model.SmallMatrixValue))
                return new(OptimizationNativeStatus.Error, "ConfiguracaoNativaInvalida");

            if (!PassModel(solver, model))
                return new(OptimizationNativeStatus.Error, "ModeloNativoRecusado");

            double Remaining() => request.BudgetSeconds - Stopwatch.GetElapsedTime(started).TotalSeconds;

            double? timeLimit = null;
            OptimizationSolverMetadata Metadata() => new(version, binary.Hash, binary.Runtime, Stopwatch.GetElapsedTime(started).TotalSeconds,
                request.BudgetSeconds, Algorithm, Threads, Seed, FeasibilityTolerance, FeasibilityTolerance, SmallMatrixValue, integerBytes, timeLimit);

            var remaining = Remaining();
            if (remaining <= 0)
                return new(OptimizationNativeStatus.TimeLimit, "OrcamentoEsgotado", Metadata: Metadata());

            if (HighsNative.SetDoubleOption(solver, "time_limit", remaining) != 0 ||
                HighsNative.GetDoubleOption(solver, "time_limit", out var effectiveLimit) != 0 || effectiveLimit != remaining)
                return new(OptimizationNativeStatus.Error, "ConfiguracaoNativaInvalida", Metadata: Metadata());
            timeLimit = effectiveLimit;

            var run = HighsNative.Run(solver);
            var rawStatus = HighsNative.GetModelStatus(solver);
            if (run < 0)
                return new(OptimizationNativeStatus.Error, "FalhaExecucaoNativa", RawStatus: RawStatusName(rawStatus), Metadata: Metadata());

            var status = rawStatus switch
            {
                7 => OptimizationNativeStatus.Optimal,
                8 => OptimizationNativeStatus.Infeasible,
                10 => OptimizationNativeStatus.Unbounded,
                11 or 12 or 16 => OptimizationNativeStatus.Feasible,
                13 => OptimizationNativeStatus.TimeLimit,
                14 => OptimizationNativeStatus.IterationLimit,
                9 or 15 or 17 => OptimizationNativeStatus.Ambiguous,
                _ => OptimizationNativeStatus.Error,
            };

            double[]? columns = null;
            double? objective = null;
            if (status is OptimizationNativeStatus.Optimal or OptimizationNativeStatus.Feasible or OptimizationNativeStatus.TimeLimit &&
                HighsNative.GetIntInfo(solver, "primal_solution_status", out var primal) == 0 && primal == FeasiblePrimal)
            {
                columns = new double[model.Costs.Length];
                var columnDuals = new double[columns.Length];
                var rowValues = new double[model.UpperBounds.Length];
                var rowDuals = new double[rowValues.Length];
                if (HighsNative.GetSolution(solver, columns, columnDuals, rowValues, rowDuals) != 0 || columns.Any(x => !double.IsFinite(x)))
                {
                    if (status != OptimizationNativeStatus.TimeLimit)
                        return new(OptimizationNativeStatus.Error, "VetorNativoInvalido", RawStatus: RawStatusName(rawStatus), Metadata: Metadata());
                    columns = null;
                }
                else
                {
                    var value = HighsNative.GetObjectiveValue(solver);
                    if (double.IsFinite(value))
                        objective = value;
                    else if (status != OptimizationNativeStatus.TimeLimit)
                        return new(OptimizationNativeStatus.Error, "ObjetivoNativoInvalido", RawStatus: RawStatusName(rawStatus), Metadata: Metadata());
                }
            }

            double[]? ray = null;
            remaining = Remaining();
            if (status == OptimizationNativeStatus.Infeasible && remaining > 0 &&
                HighsNative.SetDoubleOption(solver, "time_limit", remaining) == 0)
            {
                var values = new double[model.UpperBounds.Length];
                if (HighsNative.GetDualRay(solver, out var hasRay, values) == 0 && hasRay == 1 && values.All(double.IsFinite))
                    ray = values;
            }

            var code = status switch
            {
                OptimizationNativeStatus.Optimal => "OtimoNativo",
                OptimizationNativeStatus.Feasible => "ViavelNativo",
                OptimizationNativeStatus.Infeasible => "InviavelNativo",
                OptimizationNativeStatus.Unbounded => "ModeloIlimitado",
                OptimizationNativeStatus.TimeLimit => "LimiteTempoNativo",
                OptimizationNativeStatus.IterationLimit => "LimiteIteracoes",
                OptimizationNativeStatus.Ambiguous => "StatusNativoAmbiguo",
                _ => "FalhaExecucaoNativa",
            };
            return new(status, code, RawStatusName(rawStatus), columns, ray, Metadata(), objective);
        }
        finally
        {
            HighsNative.Destroy(solver);
        }
    }

    private static bool Configure(nint solver, double smallMatrixValue)
    {
        if (HighsNative.SetBoolOption(solver, "output_flag", 0) != 0 ||
            HighsNative.SetStringOption(solver, "solver", Algorithm) != 0 ||
            HighsNative.SetIntOption(solver, "threads", Threads) != 0 ||
            HighsNative.SetIntOption(solver, "random_seed", Seed) != 0 ||
            HighsNative.SetDoubleOption(solver, "primal_feasibility_tolerance", FeasibilityTolerance) != 0 ||
            HighsNative.SetDoubleOption(solver, "dual_feasibility_tolerance", FeasibilityTolerance) != 0 ||
            HighsNative.SetDoubleOption(solver, "small_matrix_value", smallMatrixValue) != 0)
            return false;

        var solverOption = new byte[512];
        return HighsNative.GetStringOption(solver, "solver", solverOption) == 0 &&
            Encoding.UTF8.GetString(solverOption).TrimEnd('\0') == Algorithm &&
            HighsNative.GetIntOption(solver, "threads", out var threads) == 0 && threads == Threads &&
            HighsNative.GetIntOption(solver, "random_seed", out var seed) == 0 && seed == Seed &&
            HighsNative.GetDoubleOption(solver, "primal_feasibility_tolerance", out var primal) == 0 && primal == FeasibilityTolerance &&
            HighsNative.GetDoubleOption(solver, "dual_feasibility_tolerance", out var dual) == 0 && dual == FeasibilityTolerance &&
            HighsNative.GetDoubleOption(solver, "small_matrix_value", out var small) == 0 && small == smallMatrixValue;
    }

    private static bool ValidModel(OptimizationNativeModel model)
    {
        var columns = model.Costs.Length;
        var rows = model.UpperBounds.Length;
        return columns is >= 2 and <= OptimizationEngine.MaxIngredients * 2 && columns % 2 == 0 &&
            rows is >= 2 and <= OptimizationEngine.MaxIngredients * 5 + OptimizationEngine.MaxGoals * 2 + 2 &&
            model.Coefficients.Length == rows && model.SmallMatrixValue == SmallMatrixValue && model.Offset is >= 0 and <= 1 &&
            model.Costs.All(double.IsFinite) && model.UpperBounds.All(double.IsFinite) &&
            model.Coefficients.All(row => row.Length == columns &&
                row.All(x => double.IsFinite(x) && (x == 0 || Math.Abs(x) > SmallMatrixValue)));
    }

    private static bool PassModel(nint solver, OptimizationNativeModel model)
    {
        var starts = new List<int>();
        var indices = new List<int>();
        var values = new List<double>();
        foreach (var row in model.Coefficients)
        {
            starts.Add(indices.Count);
            for (var column = 0; column < row.Length; column++)
            {
                if (row[column] == 0)
                    continue;
                indices.Add(column);
                values.Add(row[column]);
            }
        }

        var infinity = HighsNative.GetInfinity(solver);
        return HighsNative.PassLp(solver, model.Costs.Length, model.UpperBounds.Length, values.Count, Rowwise, Minimize, model.Offset,
            model.Costs, Repeat(-infinity, model.Costs.Length), Repeat(infinity, model.Costs.Length),
            Repeat(-infinity, model.UpperBounds.Length), model.UpperBounds, starts.ToArray(), indices.ToArray(), values.ToArray()) == 0;
    }

    private static double[] Repeat(double value, int count)
    {
        var result = new double[count];
        Array.Fill(result, value);
        return result;
    }

    private static (string Hash, string Runtime)? FindVerifiedLibrary()
    {
        if (RuntimeInformation.ProcessArchitecture != Architecture.X64)
            return null;

        var platform = OperatingSystem.IsWindows()
            ? (Runtime: "win-x64", File: "highs.dll", Hash: "43DDF43BBBCC18E024731F17DB3369C379E2FA6F1CF61AA7E2E2F3FD8BB03058")
            : (Runtime: "linux-x64", File: "highs.so", Hash: "640EB397C0B106E3C0CBE37175EEE6C76048A3F873827E187E9610A0C2A17912");
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux())
            return null;

        var location = Path.Combine(AppContext.BaseDirectory, "runtimes", platform.Runtime, "native", platform.File);
        if (!File.Exists(location))
            location = Path.Combine(AppContext.BaseDirectory, platform.File);
        if (!File.Exists(location))
            return null;

        using var stream = File.OpenRead(location);
        var hash = Convert.ToHexString(SHA256.HashData(stream));
        if (hash != platform.Hash)
            return null;

        var library = NativeLibrary.Load(location);
        NativeLibrary.SetDllImportResolver(typeof(HighsNative).Assembly, (_, _, _) => library);
        return (hash, platform.Runtime);
    }

    private static string RawStatusName(int status) => status switch
    {
        0 => "Notset", 1 => "LoadError", 2 => "ModelError", 3 => "PresolveError", 4 => "SolveError", 5 => "PostsolveError",
        6 => "ModelEmpty", 7 => "Optimal", 8 => "Infeasible", 9 => "UnboundedOrInfeasible", 10 => "Unbounded",
        11 => "ObjectiveBound", 12 => "ObjectiveTarget", 13 => "TimeLimit", 14 => "IterationLimit", 15 => "Unknown",
        16 => "SolutionLimit", 17 => "Interrupt", _ => $"Status{status}",
    };
}
