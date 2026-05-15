using System.Collections.Generic;
using System.Threading.Tasks;

namespace Encountive.Telemetry
{
    /// <summary>
    /// Phase 1 destination for queued statements. The Phase 2 Façade
    /// HTTPS endpoint (POST /v1/xr/xapi/batch, SDD §7.3) implements the
    /// same interface, so the emitter never changes. A local durable
    /// JSON sink is the Phase 1 implementation (Unity glue).
    /// </summary>
    public interface IXApiSink
    {
        /// <summary>Persist a batch. Returns false on any failure
        /// (offline, 5xx) so the emitter retains the batch and retries
        /// on the next flush — statements are append-only and idempotent
        /// (SDD §3.2.2, §6.5).</summary>
        Task<bool> TryStoreAsync(IReadOnlyList<XApiStatement> batch);
    }

    /// <summary>
    /// Offline-tolerant xAPI emitter (SDD §3.2.2, §7.3). Emit never
    /// blocks the learner session; statements queue locally and flush on
    /// reconnect with no data hole. Idempotent on statement id so a
    /// retried batch never double-counts (SDD §6.5).
    ///
    /// "Session failure is a safety issue; scoring failure is only a
    /// quality issue" (SDD §9.3) — therefore Emit cannot throw and a
    /// sink outage only delays delivery, never drops it.
    /// </summary>
    public sealed class XApiEmitter
    {
        private readonly IXApiSink _sink;
        private readonly List<XApiStatement> _queue = new List<XApiStatement>();
        private readonly HashSet<string> _seenIds = new HashSet<string>();
        private readonly int _maxBatch;

        public XApiEmitter(IXApiSink sink, int maxBatch = 50)
        {
            _sink = sink;
            _maxBatch = maxBatch; // SDD §7.3: batch endpoint up to 50
        }

        public int PendingCount => _queue.Count;

        /// <summary>Enqueue a statement. Append-only and deduplicated by
        /// id: a statement already seen (e.g. a replay re-emit) is
        /// ignored. Never throws.</summary>
        public void Emit(XApiStatement statement)
        {
            if (statement == null || string.IsNullOrEmpty(statement.Id)) return;
            if (!_seenIds.Add(statement.Id)) return;
            _queue.Add(statement);
        }

        /// <summary>Attempt to drain the queue to the sink in batches.
        /// On sink failure the unsent statements stay queued in original
        /// order for the next flush. Returns true only when the queue is
        /// fully drained.</summary>
        public async Task<bool> FlushAsync()
        {
            while (_queue.Count > 0)
            {
                int take = _queue.Count < _maxBatch ? _queue.Count : _maxBatch;
                var batch = _queue.GetRange(0, take);

                bool stored = await _sink.TryStoreAsync(batch);
                if (!stored) return false; // keep queue intact; retry later

                _queue.RemoveRange(0, take);
            }
            return true;
        }
    }
}
