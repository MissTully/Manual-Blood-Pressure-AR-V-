using System.Collections.Generic;
using System.Threading.Tasks;
using Encountive.Domain;

namespace Encountive.Triggers
{
    /// <summary>Trigger families (SDD §13). Word budgets and firing
    /// conditions differ per family; the client lifecycle is identical
    /// (SDD §8.4).</summary>
    public enum TriggerFamily
    {
        ScenarioFraming,
        DecisionPoint,
        SafetyGate,
        Debrief
    }

    /// <summary>How the coach voice should be played back (SDD §7.2
    /// response schema).</summary>
    public enum VoicePlaybackKind
    {
        None,
        UsePrecached,
        SignedUrl
    }

    /// <summary>
    /// Structured payload posted at a trigger-fire moment (SDD §7.2).
    /// Carries only abstracted state — never raw biometrics
    /// (SDD §2.5.2).
    /// </summary>
    public sealed class TriggerEvent
    {
        public string SessionId { get; set; }
        public string StationId { get; set; }
        public TriggerFamily Family { get; set; }
        public string TriggerId { get; set; }
        public TrainingMode Mode { get; set; }
        public string Target { get; set; }

        /// <summary>Abstracted learner-state key/value pairs (e.g.
        /// stage, selectedCuffClass, muac_cm, personaId).</summary>
        public IReadOnlyDictionary<string, string> StateSnapshot { get; set; }
            = new Dictionary<string, string>();
    }

    /// <summary>Coach utterance returned for a trigger (SDD §5.1, §7.2).</summary>
    public sealed class CoachUtterance
    {
        public string UtteranceId { get; set; }
        public string Text { get; set; }
        public string Source { get; set; }                 // "authored" | "generated"
        public string PromptTemplateVersion { get; set; }
        public string AuditEntryId { get; set; }
        public VoicePlaybackKind VoiceKind { get; set; }
        public string VoiceUri { get; set; }
    }

    /// <summary>SDD §7.4 — the trigger client contract. Phase 1
    /// implementation is local and never touches the network
    /// (ADR-XR-008).</summary>
    public interface ITriggerClient
    {
        Task<CoachUtterance> Fire(TriggerEvent ev);
    }
}
