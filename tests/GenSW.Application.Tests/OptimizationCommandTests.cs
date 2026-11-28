using GenSW.Application.Optimization;
using GenSW.Domain.Optimization;
using Xunit;

namespace GenSW.Application.Tests;

public sealed class OptimizationCommandTests
{
    [Fact]
    public void Canonical_command_preserves_exact_semantic_keys_and_normalizes_numbers_and_unordered_ids()
    {
        var first = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var last = Guid.Parse("20000000-0000-0000-0000-000000000001");
        var request = new OptimizationCommand(OptimizationEngine.ModelVersion, "N1", false, "100.000000",
            [Line(last, "60.0"), Line(first, "40.00000")],
            [new(last, "EM", " Método documentado ", " Contexto exato ", "BN", "MJ/kg", "10.000", null,
                "InformadaUsuario", Justificativa: "Fixture sintética")],
            ReferenciaDescricao: "Mistura sintética", Fase: " Fase documentada ");

        var canonical = OptimizationInputResolver.Canonicalize(request);
        Assert.Equal("100", canonical.MassaKg);
        Assert.Equal(new[] { first, last }, canonical.Candidatos.Select(x => x.Id));
        Assert.Equal("40", canonical.Candidatos[0].ReferenciaKg);
        Assert.Equal("10", canonical.Metas[0].Minimo);
        Assert.Equal(" Método documentado ", canonical.Metas[0].Metodo);
        Assert.Equal(" Contexto exato ", canonical.Metas[0].Contexto);
        Assert.Equal(" Fase documentada ", canonical.Fase);
    }

    [Fact]
    public void A_matching_idempotent_request_waits_for_capture_without_taking_a_solver_slot()
    {
        var gate = new OptimizationWorkGate();
        var actor = Guid.NewGuid();
        var account = Guid.NewGuid();
        var binding = Guid.NewGuid();
        using var owner = Assert.IsType<OptimizationWorkGate.Lease>(
            gate.TryEnter(account, binding, 1, 1, actor, "same", out var noWait));
        Assert.Null(noWait);
        Assert.Null(gate.TryEnter(account, binding, 1, 1, actor, "same", out var matchingCapture));
        Assert.NotNull(matchingCapture);
        Assert.False(matchingCapture.IsCompleted);
        Assert.Null(gate.TryEnter(account, binding, 1, 1, Guid.NewGuid(), "same", out var differentActor));
        Assert.Null(differentActor);
        Assert.Null(gate.TryEnter(account, binding, 1, 2, actor, "same", out var differentGeneration));
        Assert.Null(differentGeneration);
        using var independent = Assert.IsType<OptimizationWorkGate.Lease>(
            gate.TryEnter(Guid.NewGuid(), Guid.NewGuid(), 1, 1, actor, "same", out var otherAccount));
        Assert.Null(otherAccount);
        owner.SignalCapture();
        Assert.True(matchingCapture.IsCompletedSuccessfully);
        Assert.Null(gate.TryEnter(account, binding, 1, 1, actor, "different", out _));
        owner.Dispose();
        using var next = Assert.IsType<OptimizationWorkGate.Lease>(gate.TryEnter(account, binding, 1, 1, actor, "different", out _));
    }

    [Fact]
    public void Saturated_process_admits_waiting_accounts_in_order_and_skips_abandoned_requests()
    {
        var gate = new OptimizationWorkGate();
        var actor = Guid.NewGuid();
        OptimizationWorkGate.Lease Enter() =>
            Assert.IsType<OptimizationWorkGate.Lease>(gate.TryEnter(Guid.NewGuid(), Guid.NewGuid(), 1, 1, actor, "same", out _));

        var active = Enumerable.Range(0, 8).Select(_ => Enter()).ToArray();
        Assert.All(active, lease => Assert.True(lease.Ready.IsCompletedSuccessfully));
        using var abandoned = Enter();
        using var first = Enter();
        using var second = Enter();
        Assert.False(first.Ready.IsCompleted);
        Assert.False(second.Ready.IsCompleted);

        abandoned.Dispose();
        active[0].Dispose();
        Assert.True(first.Ready.IsCompletedSuccessfully);
        Assert.False(abandoned.Ready.IsCompleted);
        Assert.False(second.Ready.IsCompleted);
        first.Dispose();
        Assert.True(second.Ready.IsCompletedSuccessfully);

        foreach (var lease in active) lease.Dispose();
    }

    private static OptimizationCandidateCommand Line(Guid id, string reference) =>
        new(id, Guid.NewGuid(), 1, null, null, reference);
}
