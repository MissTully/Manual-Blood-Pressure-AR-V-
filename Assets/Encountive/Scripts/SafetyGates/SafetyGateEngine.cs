using Encountive.Domain;

namespace Encountive.SafetyGates
{
    /// <summary>
    /// Deterministic Cuff Size Selection safety-gate engine
    /// (CSS-SG-1..8, SDD §13.3). Pure C#, no Unity dependency, no AI
    /// (ADR-XR-004). Gates are evaluated in fixed priority order so the
    /// most safety-critical violation is always the one surfaced:
    /// identity → pediatric assent → pediatric calm state → bare arm →
    /// boundary handling → cuff correctness → stigmatizing language →
    /// documentation. The first failing gate wins; otherwise Pass.
    ///
    /// Gate-1 (identity) escalates instead of firing when skipped during
    /// Full Encounter — the encounter aborts to a remediation pathway
    /// (SDD §8.5).
    /// </summary>
    public sealed class SafetyGateEngine : ISafetyGateEngine
    {
        private readonly BiasLexicon _biasLexicon;

        public SafetyGateEngine(BiasLexicon biasLexicon = null)
        {
            _biasLexicon = biasLexicon ?? new BiasLexicon();
        }

        public GateResolution Evaluate(GateInput input)
        {
            // CSS-SG-1 — Gate-1: identity verification. Every action in
            // this sub-scene is post-identity; none may proceed until S1
            // identity is satisfied.
            if (!input.IdentityVerified)
            {
                return input.Mode == TrainingMode.FullEncounter
                    ? GateResolution.Escalate(
                        "CSS-SG-1", CssStage.S1_IdentityConsent, "CSS-SG-1",
                        "Identity not verified during Full Encounter; encounter aborts to remediation.")
                    : GateResolution.Fire(
                        "CSS-SG-1", CssStage.S1_IdentityConsent, "CSS-SG-1",
                        "Patient contact attempted without verifying identity in S1.");
            }

            bool pediatric = input.Persona != null &&
                             input.Persona.Population == PopulationClass.Pediatric;
            bool measurementContact =
                input.Action == LearnerAction.AttemptMeasurement ||
                input.Action == LearnerAction.AttemptCuffApplication;

            // CSS-SG-2 — Gate-1a: pediatric assent.
            if (pediatric && measurementContact && !input.AssentObtained)
            {
                return GateResolution.Fire(
                    "CSS-SG-2", CssStage.S1_IdentityConsent, "CSS-SG-2",
                    "Pediatric measurement attempted without age-appropriate assent.");
            }

            // CSS-SG-3 — Gate-1b: pediatric calm-state protocol.
            if (pediatric && measurementContact &&
                input.Persona.ShowsDistress && !input.CalmStateAchieved)
            {
                return GateResolution.Fire(
                    "CSS-SG-3", CssStage.S1_IdentityConsent, "CSS-SG-3",
                    "Pediatric patient in distress; measurement attempted before calming.");
            }

            // CSS-SG-4 — cuff applied over clothing.
            if (input.Action == LearnerAction.AttemptCuffApplication && !input.ArmExposed)
            {
                return GateResolution.Fire(
                    "CSS-SG-4", CssStage.S2_Landmarks, "CSS-SG-4",
                    "Cuff application attempted without exposing the upper arm.");
            }

            if (input.Action == LearnerAction.CommitCuffClass)
            {
                CuffRecommendation rec = CuffSizeRules.Recommend(input.Persona, input.MuacCm);

                // CSS-SG-7 — boundary case committed without DP-3. Policed
                // before SG-5 so the boundary is the lesson, not "wrong".
                if (rec.IsBoundary && !input.Dp3Engaged)
                {
                    return GateResolution.Fire(
                        "CSS-SG-7", CssStage.S4_CuffSelection, "CSS-SG-7",
                        "Boundary case committed without engaging DP-3.");
                }

                // CSS-SG-5 — committed class fails the AHA 80%/40% rule.
                if (!CuffSizeRules.IsCommitCorrect(input.CommittedCuffClass, rec))
                {
                    return GateResolution.Fire(
                        "CSS-SG-5", CssStage.S4_CuffSelection, "CSS-SG-5",
                        "Committed cuff class fails the AHA 80%/40% rule for the captured MUAC.");
                }
            }

            // CSS-SG-6 — stigmatizing language at rationale capture.
            if (input.Action == LearnerAction.CaptureRationale &&
                _biasLexicon.IsStigmatizing(input.RationaleText))
            {
                return GateResolution.Fire(
                    "CSS-SG-6", CssStage.S5_Confirmation, "CSS-SG-6",
                    "Stigmatizing language detected in captured rationale.");
            }

            // CSS-SG-8 — advance attempted without S5 confirmation.
            if (input.Action == LearnerAction.AttemptAdvanceToStation3 && !input.S5Confirmed)
            {
                return GateResolution.Fire(
                    "CSS-SG-8", CssStage.S5_Confirmation, "CSS-SG-8",
                    "Advance to Station 3 attempted without confirming at S5.");
            }

            return GateResolution.Pass;
        }
    }
}
