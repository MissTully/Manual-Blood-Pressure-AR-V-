namespace Encountive.Telemetry
{
    /// <summary>
    /// Encountive xAPI verb and extension IRIs (SDD §5.2). New event
    /// classes live under the Encountive extension namespace; existing
    /// vocabulary is reused where it covers the event.
    /// </summary>
    public static class XApiVocabulary
    {
        public const string VerbBase = "https://xapi.encountive.com/verbs/";
        public const string ExtBase = "https://xapi.encountive.com/extensions/";
        public const string ActivityBase = "https://xapi.encountive.com/activities/mbpxr/";

        // Session lifecycle
        public const string SessionStarted = VerbBase + "session-started";
        public const string SessionEnded = VerbBase + "session-ended";
        public const string StationEntered = VerbBase + "station-entered";
        public const string StationExited = VerbBase + "station-exited";

        // Stage progression
        public const string StageEntered = VerbBase + "stage-entered";
        public const string StageCompleted = VerbBase + "stage-completed";

        // Decision events
        public const string DecisionRationaleCaptured = VerbBase + "decision-rationale-captured";
        public const string DecisionCommitted = VerbBase + "decision-committed";
        public const string DecisionRevised = VerbBase + "decision-revised";

        // Safety gate events
        public const string SafetyGateFired = VerbBase + "safety-gate-fired";
        public const string SafetyGateRedirected = VerbBase + "safety-gate-redirected";
        public const string SafetyGateResolved = VerbBase + "safety-gate-resolved";

        // Coaching
        public const string CoachUtterancePlayed = VerbBase + "coach-utterance-played";

        // Mastery
        public const string PerStationMasteryAchieved = VerbBase + "per-station-mastery-achieved";
        public const string FullEncounterMasteryAchieved = VerbBase + "full-encounter-mastery-achieved";
        public const string PersistentMasteryAchieved = VerbBase + "persistent-mastery-achieved";

        // Context extensions
        public const string ExtMode = ExtBase + "mode";
        public const string ExtTarget = ExtBase + "target";
        public const string ExtPersona = ExtBase + "persona";
        public const string ExtCuffClassSelected = ExtBase + "cuff_class_selected";
        public const string ExtMuacCm = ExtBase + "muac_cm";
        public const string ExtGateId = ExtBase + "gate_id";
        public const string ExtTriggerId = ExtBase + "trigger_id";
        public const string ExtPromptVersion = ExtBase + "prompt_version";
        public const string ExtAuditEntryId = ExtBase + "audit_entry_id";
    }
}
