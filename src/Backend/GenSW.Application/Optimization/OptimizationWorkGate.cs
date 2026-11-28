namespace GenSW.Application.Optimization;

public sealed class OptimizationWorkGate
{
    private const int ProcessCapacity = 8;
    private const int WaitingCapacity = 32;
    private readonly object sync = new();
    private readonly Dictionary<Guid, Lease> active = [];
    private readonly LinkedList<Lease> waiting = [];
    private int running;

    internal Lease? TryEnter(Guid account, Guid binding, long revision, long generation, Guid actor, string key,
        out Task? matchingCapture)
    {
        lock (sync)
        {
            matchingCapture = null;
            if (active.TryGetValue(account, out var current))
            {
                if (current.Matches(binding, revision, generation, actor, key))
                    matchingCapture = current.Captured;
                return null;
            }
            if (running == ProcessCapacity && waiting.Count == WaitingCapacity) return null;

            var lease = new Lease(this, account, binding, revision, generation, actor, key);
            active.Add(account, lease);
            if (running < ProcessCapacity)
            {
                running++;
                lease.Start();
            }
            else
            {
                lease.WaitPosition = waiting.AddLast(lease);
            }
            return lease;
        }
    }

    internal sealed class Lease(OptimizationWorkGate gate, Guid account, Guid binding, long revision, long generation,
        Guid actor, string key) : IDisposable
    {
        private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource capture = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal LinkedListNode<Lease>? WaitPosition { get; set; }
        internal Task Ready => ready.Task;
        internal Task Captured => capture.Task;
        internal void Start() => ready.TrySetResult();
        internal void SignalCapture() => capture.TrySetResult();

        internal bool Matches(Guid otherBinding, long otherRevision, long otherGeneration, Guid otherActor, string otherKey) =>
            binding == otherBinding && revision == otherRevision && generation == otherGeneration &&
            actor == otherActor && key == otherKey;

        public void Dispose()
        {
            lock (gate.sync)
            {
                if (gate.active.TryGetValue(account, out var current) && ReferenceEquals(current, this))
                {
                    gate.active.Remove(account);
                    if (WaitPosition is { } position)
                    {
                        gate.waiting.Remove(position);
                    }
                    else if (gate.waiting.First is { } next)
                    {
                        gate.waiting.RemoveFirst();
                        next.Value.WaitPosition = null;
                        next.Value.Start();
                    }
                    else
                    {
                        gate.running--;
                    }
                }
                SignalCapture();
            }
        }
    }
}
