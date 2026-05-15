using System;
using System.Threading.Tasks;

namespace Encountive.Triggers
{
    /// <summary>
    /// Phase 1 <see cref="ITriggerClient"/> implementation. Resolves
    /// every trigger from the local hand-authored catalog and never
    /// touches the network or any AI service (ADR-XR-008). The Phase 2
    /// Façade-backed client implements the same interface, so station
    /// code never changes.
    ///
    /// An unknown trigger id returns a safe, generic acknowledgement
    /// rather than throwing — a missing line must never block or crash
    /// a learner session (SDD §3.2.2: session failure is a safety
    /// issue, scoring failure is only a quality issue).
    /// </summary>
    public sealed class LocalFallbackTriggerClient : ITriggerClient
    {
        public const string PromptVersion = "phase1-local-v0";

        private readonly Func<string> _idFactory;

        public LocalFallbackTriggerClient(Func<string> idFactory = null)
        {
            _idFactory = idFactory ?? (() => Guid.NewGuid().ToString());
        }

        public Task<CoachUtterance> Fire(TriggerEvent ev)
        {
            if (ev == null || string.IsNullOrWhiteSpace(ev.TriggerId))
                return Task.FromResult(SafeFallback());

            if (!CuffSizeSelectionCatalog.TryGet(ev.TriggerId, out var entry))
                return Task.FromResult(SafeFallback());

            var utterance = new CoachUtterance
            {
                UtteranceId = _idFactory(),
                Text = entry.AuthoredText,
                Source = "authored",
                PromptTemplateVersion = PromptVersion,
                AuditEntryId = null,
                VoiceKind = entry.PrecachedClipId != null
                    ? VoicePlaybackKind.UsePrecached
                    : VoicePlaybackKind.None,
                VoiceUri = entry.PrecachedClipId
            };
            return Task.FromResult(utterance);
        }

        private CoachUtterance SafeFallback() => new CoachUtterance
        {
            UtteranceId = _idFactory(),
            Text = "Let's keep going.",
            Source = "authored",
            PromptTemplateVersion = PromptVersion,
            VoiceKind = VoicePlaybackKind.None
        };
    }
}
