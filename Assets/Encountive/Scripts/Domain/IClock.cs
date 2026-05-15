using System;

namespace Encountive.Domain
{
    /// <summary>Injected time source so telemetry timestamps are
    /// deterministic under test (SDD §16.6 replay determinism).</summary>
    public interface IClock
    {
        DateTimeOffset UtcNow { get; }
    }

    public sealed class SystemClock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }
}
