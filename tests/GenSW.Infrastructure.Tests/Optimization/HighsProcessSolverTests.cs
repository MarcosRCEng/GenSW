using System.Runtime.InteropServices;
using GenSW.Domain.Optimization;
using GenSW.Infrastructure.Optimization;
using Xunit;

namespace GenSW.Infrastructure.Tests.Optimization;

public sealed class HighsProcessSolverTests
{
    [Fact]
    public async Task LoadsThePinnedBinaryAndSolvesAConstrainedAbProblem()
    {
        var a = Guid.Parse("00000000-0000-4000-8000-000000000001");
        var b = Guid.Parse("00000000-0000-4000-8000-000000000002");
        var problem = new OptimizationProblem(100, [new(a, 60, 0, 100), new(b, 40, 0, 100)],
        [
            new(Guid.Parse("00000000-0000-4000-8000-000000000101"), 250, null, "g/kg BN", new Dictionary<Guid, Rational> { [a] = 100, [b] = 400 }),
            new(Guid.Parse("00000000-0000-4000-8000-000000000102"), 10, null, "MJ/kg BN", new Dictionary<Guid, Rational> { [a] = 12, [b] = 8 }),
            new(Guid.Parse("00000000-0000-4000-8000-000000000103"), null, 50, "g/kg BN", new Dictionary<Guid, Rational> { [a] = 20, [b] = 80 }),
        ]);

        var result = await new OptimizationEngine(new HighsProcessSolver()).EvaluateAsync(problem);

        Assert.True(result.Outcome == OptimizationOutcome.SolucaoViavel, $"{result.Outcome}/{result.Code}/{result.Native?.RawStatus}");
        Assert.Equal("50", result.Candidate!.QuantizedMasses![a]);
        Assert.Equal("50", result.Candidate.QuantizedMasses[b]);
        Assert.Equal("20", result.Candidate.QuantizedDeviationKg);
        Assert.Equal("Optimal", result.Native!.RawStatus);

        var metadata = Assert.IsType<OptimizationSolverMetadata>(result.Native.Metadata);
        Assert.Equal("1.15.1", metadata.Version);
        Assert.Equal("simplex", metadata.Algorithm);
        Assert.Equal(1, metadata.Threads);
        Assert.Equal(0, metadata.Seed);
        Assert.Equal(1e-8, metadata.PrimalTolerance);
        Assert.Equal(1e-8, metadata.DualTolerance);
        Assert.Equal(1e-12, metadata.SmallMatrixValue);
        Assert.Equal(4, metadata.HighsIntBytes);
        Assert.InRange(metadata.NativeTimeLimitSeconds!.Value, 0.01, 5);
        Assert.Equal(Architecture.X64, RuntimeInformation.ProcessArchitecture);
        if (OperatingSystem.IsWindows())
        {
            Assert.Equal("win-x64", metadata.Runtime);
            Assert.Equal("43DDF43BBBCC18E024731F17DB3369C379E2FA6F1CF61AA7E2E2F3FD8BB03058", metadata.BinarySha256);
        }
        else
        {
            Assert.True(OperatingSystem.IsLinux());
            Assert.Equal("linux-x64", metadata.Runtime);
            Assert.Equal("640EB397C0B106E3C0CBE37175EEE6C76048A3F873827E187E9610A0C2A17912", metadata.BinarySha256);
        }

        Assert.True(File.Exists(Path.Combine(AppContext.BaseDirectory, "ThirdParty", "HiGHS.LICENSE.txt")));
        Assert.True(File.Exists(Path.Combine(AppContext.BaseDirectory, "ThirdParty", "HiGHS.THIRD_PARTY_NOTICES.md")));
    }

    [Fact]
    public async Task ReadsARealDualRayAndVerifiesItAgainstTheOriginalModel()
    {
        var a = Guid.Parse("00000000-0000-4000-8000-000000000001");
        var b = Guid.Parse("00000000-0000-4000-8000-000000000002");
        var c = Guid.Parse("00000000-0000-4000-8000-000000000003");
        var minimum = Rational.Parse("3/4");
        var problem = new OptimizationProblem(1, [new(a, 1, 0, 100), new(b, 0, 0, 100), new(c, 0, 0, 100)],
        [
            new(Guid.Parse("00000000-0000-4000-8000-000000000101"), minimum, null, "g/kg BN", new Dictionary<Guid, Rational> { [a] = 1, [b] = 1, [c] = 0 }),
            new(Guid.Parse("00000000-0000-4000-8000-000000000102"), minimum, null, "MJ/kg BN", new Dictionary<Guid, Rational> { [a] = 0, [b] = 0, [c] = 1 }),
        ]);

        var result = await new OptimizationEngine(new HighsProcessSolver()).EvaluateAsync(problem);

        Assert.Equal("Infeasible", result.Native?.RawStatus);
        Assert.NotNull(result.Native?.DualRay);
        Assert.Equal(OptimizationOutcome.InviabilidadeDemonstrada, result.Outcome);
        Assert.Equal("FarkasVerificado", result.Proof?.Code);
    }

    [Fact]
    public async Task SupervisorTimesOutAnActualWorkerProcess()
    {
        var model = new OptimizationNativeModel([0, 1], [[1, 0], [-1, 0], [0, -1]], [1, -1, 0], 1e-12);

        var result = await new HighsProcessSolver(TimeSpan.FromMilliseconds(1)).SolveAsync(model, TimeSpan.FromSeconds(5), CancellationToken.None);

        Assert.Equal(OptimizationNativeStatus.TimeLimit, result.Status);
        Assert.Equal("WatchdogExcedido", result.Code);
    }
}
