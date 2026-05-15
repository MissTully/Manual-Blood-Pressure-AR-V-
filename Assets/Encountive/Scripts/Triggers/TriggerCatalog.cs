using System.Collections.Generic;

namespace Encountive.Triggers
{
    /// <summary>One hand-authored coach line plus its SDD §13 word
    /// budget (expressed in seconds) and optional pre-cached clip id.</summary>
    public sealed class TriggerCatalogEntry
    {
        public string TriggerId { get; }
        public TriggerFamily Family { get; }
        public double MinSeconds { get; }
        public double MaxSeconds { get; }
        public string AuthoredText { get; }
        public string PrecachedClipId { get; }

        public TriggerCatalogEntry(
            string triggerId, TriggerFamily family,
            double minSeconds, double maxSeconds,
            string authoredText, string precachedClipId = null)
        {
            TriggerId = triggerId;
            Family = family;
            MinSeconds = minSeconds;
            MaxSeconds = maxSeconds;
            AuthoredText = authoredText;
            PrecachedClipId = precachedClipId;
        }
    }

    /// <summary>
    /// The Phase 1 hand-authored fallback library for the Cuff Size
    /// Selection sub-scene (SDD §13, §6.2.1 "Hand-Authored Fallback
    /// Library"). No AI, no network — this IS the Phase 1 coach
    /// (ADR-XR-008).
    ///
    /// Copy below is seeded from the SDD Appendix F samples and is
    /// marked PENDING the verbatim Cuff Size Selection Trigger Taxonomy
    /// Mapping v0.1, which is the authoritative source for the final
    /// prompt-template library. Word budgets are taken from SDD §13 and
    /// §2.5.2 (safety-gate redirection 12–20 s; debrief up to 90 s).
    /// </summary>
    public static class CuffSizeSelectionCatalog
    {
        /// <summary>Conversational speaking rate used to convert the
        /// SDD second-budgets into a deterministic word-count guard.
        /// ~165 words/minute = 2.75 words/second.</summary>
        public const double WordsPerSecond = 2.75;

        public static readonly IReadOnlyList<TriggerCatalogEntry> Entries = new[]
        {
            // Scenario framing (CSS-SF-1..5) — SDD §13.1
            new TriggerCatalogEntry("CSS-SF-1", TriggerFamily.ScenarioFraming, 8, 15,
                "Meet your patient. Their name is on the chart in front of you. Take a moment to read the room before you begin.",
                "fallback_scenario_framing_1"),
            new TriggerCatalogEntry("CSS-SF-2", TriggerFamily.ScenarioFraming, 8, 12,
                "Identity is confirmed. Next, find the landmarks on the upper arm so the measurement is accurate."),
            new TriggerCatalogEntry("CSS-SF-3", TriggerFamily.ScenarioFraming, 8, 12,
                "Landmarks are set. Now measure the mid-upper arm circumference at the midpoint you marked."),
            new TriggerCatalogEntry("CSS-SF-4", TriggerFamily.ScenarioFraming, 8, 12,
                "You have a measurement. Choose the cuff class that satisfies the bladder-fit rule for this arm."),
            new TriggerCatalogEntry("CSS-SF-5", TriggerFamily.ScenarioFraming, 8, 12,
                "Cuff selected. We'll carry this choice into cuff application at the next station."),

            // Decision points (CSS-DP-1..3) — SDD §13.2
            new TriggerCatalogEntry("CSS-DP-1", TriggerFamily.DecisionPoint, 8, 12,
                "Before you commit, what makes this cuff class the right fit here?"),
            new TriggerCatalogEntry("CSS-DP-2", TriggerFamily.DecisionPoint, 8, 12,
                "Say why this cuff class — in plain words, describing the person first."),
            new TriggerCatalogEntry("CSS-DP-3", TriggerFamily.DecisionPoint, 10, 15,
                "This circumference sits between two cuff classes. Walk me through how the boundary rule applies."),

            // Safety-gate redirects (CSS-SG-1..8) — SDD §13.3, budget §2.5.2
            new TriggerCatalogEntry("CSS-SG-1", TriggerFamily.SafetyGate, 12, 20,
                "Pause. We need to confirm who we're working with before any patient contact. Let's go back to identity verification.",
                "fallback_safety_gate_1"),
            new TriggerCatalogEntry("CSS-SG-2", TriggerFamily.SafetyGate, 12, 20,
                "Hold on — for a younger patient we ask for age-appropriate assent before we measure. Let's do that first."),
            new TriggerCatalogEntry("CSS-SG-3", TriggerFamily.SafetyGate, 12, 20,
                "They're not settled yet. Let's help them feel calm before the cuff goes on; an upset patient gives a false reading."),
            new TriggerCatalogEntry("CSS-SG-4", TriggerFamily.SafetyGate, 12, 20,
                "The cuff needs bare skin. Clothing under the cuff changes the pressure — let's expose the upper arm first."),
            new TriggerCatalogEntry("CSS-SG-5", TriggerFamily.SafetyGate, 12, 20,
                "That class doesn't satisfy the bladder-fit rule for this circumference. Back to the tray — let's match it correctly."),
            new TriggerCatalogEntry("CSS-SG-6", TriggerFamily.SafetyGate, 12, 20,
                "Let's reframe that — we describe people first, conditions second. Try the rationale again with person-first language."),
            new TriggerCatalogEntry("CSS-SG-7", TriggerFamily.SafetyGate, 12, 20,
                "This is a boundary case — it deserves the boundary rule before you commit. Let's work through that step together."),
            new TriggerCatalogEntry("CSS-SG-8", TriggerFamily.SafetyGate, 12, 20,
                "One more step before we move on — confirm the selection at the review stage so it's documented."),

            // Debrief (CSS-DB-1) — SDD §13.4
            new TriggerCatalogEntry("CSS-DB-1", TriggerFamily.Debrief, 0, 90,
                "You worked through this case and named the rule out loud. The next case will be different; see if your reasoning holds at the other end of the band. Notice when you reach for habit instead of the rule.")
        };

        private static readonly Dictionary<string, TriggerCatalogEntry> ById = Build();

        private static Dictionary<string, TriggerCatalogEntry> Build()
        {
            var map = new Dictionary<string, TriggerCatalogEntry>();
            foreach (var e in Entries) map[e.TriggerId] = e;
            return map;
        }

        public static bool TryGet(string triggerId, out TriggerCatalogEntry entry) =>
            ById.TryGetValue(triggerId, out entry);

        /// <summary>Deterministic word-count guard: authored copy must
        /// not exceed its SDD §13 second-budget at the assumed speaking
        /// rate. Used by tests so over-long coach copy fails the build,
        /// keeping the coach unobtrusive (SDD Appendix B checklist).</summary>
        public static bool WithinBudget(TriggerCatalogEntry entry)
        {
            int words = CountWords(entry.AuthoredText);
            double maxWords = entry.MaxSeconds * WordsPerSecond;
            return words <= maxWords;
        }

        public static int CountWords(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;
            return text.Split(
                new[] { ' ', '\t', '\n', '\r' },
                System.StringSplitOptions.RemoveEmptyEntries).Length;
        }
    }
}
