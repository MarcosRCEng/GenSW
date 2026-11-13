namespace GenSW.Application.Optimization;

public sealed class OptimizationWorkGate
{
    private readonly object sync = new();
    private Lease? active;

    internal Lease? TryEnter(Guid actor, string key, out Task? matchingCapture)
    {
        lock (sync)
        {
            if (active is null)
            {
                matchingCapture = null;
                return active = new Lease(this, actor, key);
            }
            matchingCapture = active.Actor == actor && active.Key == key ? active.Captured : null;
            return null;
        }
    }

    internal sealed class Lease(OptimizationWorkGate gate, Guid actor, string key) : IDisposable
    {
        private readonly TaskCompletionSource capture = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Guid Actor => actor;
        internal string Key => key;
        internal Task Captured => capture.Task;
        internal void SignalCapture() => capture.TrySetResult();

        public void Dispose()
        {
            lock (gate.sync)
            {
                if (ReferenceEquals(gate.active, this)) gate.active = null;
                SignalCapture();
            }
        }
    }
}
