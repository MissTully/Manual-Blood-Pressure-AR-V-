using System.Collections.Generic;
using System.Threading.Tasks;
using Encountive.Domain;
using Encountive.SafetyGates;
using Encountive.Telemetry;
using Encountive.Triggers;

namespace Encountive.Stations
{
    /// <summary>Station-level learner intents for Cuff Size Selection.</summary>
    public enum CuffAction
    {
        VerifyIdentity,
        GiveConsent,
        ObtainAssent,
        CalmPatient,
        IdentifyLandmarks,
        MeasureMuac,
        EngageDp3,
        CommitCuff,
        CaptureRationale,
        ConfirmS5,
        AdvanceToStation3
    }

    public sealed class CuffActionData
    {
        public bool Accurate = true;          // landmarks / measurement quality
        public bool FirstAttempt = true;      // landmark first-attempt flag
        public double MuacCm;
        public CuffClass Commit = CuffClass.None;
        public string RationaleText;
    }

    /// <summary>Effects of one state-machine step (SDD §8.1 vocabulary).</summary>
    public sealed class StepResult
    {
        public CssStage Stage;
        public bool GateFired;
        public GateResolution Gate;
        public string CoachTriggerId;
        public string CoachText;
        public bool StationComplete;
        public readonly List<XApiStatement> Emitted = new List<XApiStatement>();
        public IReadOnlyList<CriterionScore> RubricScores;
    }

    /// <summary>
    /// Deterministic Cuff Size Selection (Station 2) state machine
    /// (SDD §11). Pure C#, no Unity: it composes the deterministic
    /// <see cref="ISafetyGateEngine"/>, the <see cref="ITriggerClient"/>
    /// (Phase 1 = local fallback, no AI), and the
    /// <see cref="XApiStatementFactory"/>. Identical inputs reproduce
    /// identical transitions, gate fires, rubric scores and xAPI
    /// sequence — the canonical regression guarantee (SDD §16.6).
    ///
    /// A fired safety gate transitions to a blocked state and is only
    /// cleared when the same action is re-submitted and passes; the
    /// session never advances through an unsafe step (SDD §8.5).
    /// </summary>
    public sealed class CuffSizeSelectionStateMachine
    {
        private readonly ISafetyGateEngine _gates;
        private readonly ITriggerClient _triggers;
        private readonly XApiStatementFactory _xapi;
        private readonly Persona _persona;
        private readonly TrainingMode _mode;
        private const string StationId = "S2";

        private readonly CuffEvidence _ev = new CuffEvidence();
        private string _pendingGateId;
        private bool _commitAttempted;

        public CssStage Stage { get; private set; } = CssStage.S1_IdentityConsent;
        public bool Complete { get; private set; }
        public CuffEvidence Evidence => _ev;

        // Tracked abstracted state.
        private bool _identity, _consent, _assent, _calm, _armExposed,
            _dp3, _s5Confirmed;
        private double _muac;
        private CuffClass _committed = CuffClass.None;
        private string _rationale;

        public CuffSizeSelectionStateMachine(
            ISafetyGateEngine gates, ITriggerClient triggers,
            XApiStatementFactory xapi, Persona persona, TrainingMode mode)
        {
            _gates = gates;
            _triggers = triggers;
            _xapi = xapi;
            _persona = persona;
            _mode = mode;
            _ev.Pediatric = persona.Population == PopulationClass.Pediatric;
        }

        /// <summary>Enter the sub-scene: frame the scenario (CSS-SF-1)
        /// and emit station/stage entry telemetry.</summary>
        public async Task<StepResult> Start()
        {
            var r = new StepResult { Stage = Stage };
            // Profile §5: each station is modeled as a lesson; the trial-
            // /selection-/measurement-level statements that follow are
            // automatically scoped to this lesson as their parent.
            r.Emitted.Add(_xapi.EnterLesson(StationId, "Cuff Size Selection"));
            await Fire(r, "CSS-SF-1");
            return r;
        }

        public async Task<StepResult> Step(CuffAction action, CuffActionData data = null)
        {
            data ??= new CuffActionData();
            var r = new StepResult { Stage = Stage };

            if (TryGate(action, data, out GateInput gi))
            {
                GateResolution res = _gates.Evaluate(gi);
                if (res.Kind != GateResolutionKind.Pass)
                {
                    RecordGateEvidence(res.GateId);
                    _pendingGateId = res.GateId;
                    r.GateFired = true;
                    r.Gate = res;
                    r.Stage = Stage; // blocked; no advance
                    // Profile has no native "gate fired" verb; the redirect
                    // coaching utterance below records the event through
                    // the standard received-coaching path. The gate id is
                    // encoded in the trigger id (e.g. CSS-SG-1).
                    await Fire(r, res.CoachTriggerId);
                    return r;
                }

                if (_pendingGateId != null)
                {
                    // Re-attempt passed; clear the pending gate. No
                    // "resolved" verb exists in the profile.
                    _pendingGateId = null;
                }
            }

            await Apply(action, data, r);
            r.Stage = Stage;
            return r;
        }

        // Which actions are policed by a deterministic gate, and the
        // GateInput they map to (SDD §13.3).
        private bool TryGate(CuffAction a, CuffActionData d, out GateInput gi)
        {
            LearnerAction? la = a switch
            {
                CuffAction.IdentifyLandmarks => LearnerAction.AttemptPatientContact,
                CuffAction.MeasureMuac => LearnerAction.AttemptMeasurement,
                CuffAction.CommitCuff => LearnerAction.CommitCuffClass,
                CuffAction.CaptureRationale => LearnerAction.CaptureRationale,
                CuffAction.AdvanceToStation3 => LearnerAction.AttemptAdvanceToStation3,
                _ => (LearnerAction?)null
            };
            if (la == null) { gi = null; return false; }

            gi = new GateInput
            {
                Action = la.Value,
                Stage = Stage,
                Mode = _mode,
                Persona = _persona,
                MuacCm = _muac,
                IdentityVerified = _identity,
                ConsentObtained = _consent,
                AssentObtained = _assent,
                CalmStateAchieved = _calm,
                ArmExposed = _armExposed,
                // The gate must see the learner's intended choice for
                // THIS action, not the last-stored value (the payload is
                // only committed to state after the gate passes).
                CommittedCuffClass = a == CuffAction.CommitCuff ? d.Commit : _committed,
                Dp3Engaged = _dp3,
                S5Confirmed = _s5Confirmed,
                RationaleText = a == CuffAction.CaptureRationale ? d.RationaleText : _rationale
            };
            return true;
        }

        private async Task Apply(CuffAction a, CuffActionData d, StepResult r)
        {
            switch (a)
            {
                case CuffAction.VerifyIdentity:
                    _identity = true;
                    _ev.IdentityVerifiedBeforeContact = true;
                    break;
                case CuffAction.GiveConsent:
                    _consent = true; _ev.ConsentObtained = true;
                    break;
                case CuffAction.ObtainAssent:
                    _assent = true; _ev.AssentObtained = true;
                    break;
                case CuffAction.CalmPatient:
                    _calm = true; _ev.CalmAchieved = true;
                    break;

                case CuffAction.IdentifyLandmarks:
                    await Advance(r, CssStage.S1_IdentityConsent, CssStage.S2_Landmarks, "CSS-SF-2");
                    _ev.LandmarksAccurateFirstAttempt = d.Accurate && d.FirstAttempt;
                    _ev.LandmarksAccurateEventually = d.Accurate;
                    if (d.Accurate)
                        await Advance(r, CssStage.S2_Landmarks, CssStage.S3_Measurement, "CSS-SF-3");
                    break;

                case CuffAction.MeasureMuac:
                    _muac = d.MuacCm;
                    _ev.MeasurementWithinTolerance = d.Accurate;
                    _ev.BoundaryCase =
                        CuffSizeRules.Recommend(_persona, _muac).IsBoundary;
                    await Advance(r, CssStage.S3_Measurement, CssStage.S4_CuffSelection, "CSS-SF-4");
                    break;

                case CuffAction.EngageDp3:
                    _dp3 = true; _ev.Dp3Engaged = true;
                    await Fire(r, "CSS-DP-3");
                    break;

                case CuffAction.CommitCuff:
                    await Fire(r, "CSS-DP-1");
                    _committed = d.Commit;
                    _ev.CuffCorrectOnFirstCommit = !_commitAttempted;
                    _commitAttempted = true;
                    // Profile §4.2 / §8.3 — cuff selection commit. Gate
                    // already passed so the AHA rule is satisfied;
                    // selection-correct=true on this commit.
                    r.Emitted.Add(_xapi.SelectedCuff(
                        caseOrTrialId: _persona.PersonaId,
                        displayName: "Cuff selection for " + _persona.DisplayName,
                        cuffId: d.Commit.ToString(),
                        cuffSizeClass: d.Commit.ToString(),
                        selectionCorrect: true));
                    await Advance(r, CssStage.S4_CuffSelection, CssStage.S5_Confirmation, "CSS-SF-5");
                    break;

                case CuffAction.CaptureRationale:
                    _rationale = d.RationaleText;
                    _ev.RationaleProvided = !string.IsNullOrWhiteSpace(d.RationaleText);
                    _ev.RationalePersonFirst = _ev.RationaleProvided && !_ev.Sg6Fired;
                    await Fire(r, "CSS-DP-2");
                    break;

                case CuffAction.ConfirmS5:
                    _s5Confirmed = true;
                    break;

                case CuffAction.AdvanceToStation3:
                    _ev.RationalePersonFirst = _ev.RationaleProvided && !_ev.Sg6Fired;
                    r.RubricScores = CuffSizeSelectionRubric.Score(_ev);
                    if (_mode != TrainingMode.FullEncounter)
                        await Fire(r, "CSS-DB-1");

                    double scaled = NormalizedRubricScore(r.RubricScores);
                    bool allMastery = AllMastery(r.RubricScores);
                    r.Emitted.Add(allMastery
                        ? _xapi.PassedLesson(StationId, "Cuff Size Selection", scaled)
                        : _xapi.FailedLesson(StationId, "Cuff Size Selection", scaled));
                    Complete = true;
                    r.StationComplete = true;
                    break;
            }
        }

        private async Task Advance(StepResult r, CssStage from, CssStage to, string framingTrigger)
        {
            if (Stage != from) return;
            // Stage transitions are internal to the FSM; the AR BP Cuff
            // Trainer profile has no per-stage verbs, so xAPI emission
            // happens via the framing trigger's received-coaching event.
            Stage = to;
            await Fire(r, framingTrigger);
        }

        private static double NormalizedRubricScore(System.Collections.Generic.IReadOnlyList<CriterionScore> scores)
        {
            if (scores == null || scores.Count == 0) return 0.0;
            double sum = 0;
            foreach (var c in scores) sum += c.Score;
            return System.Math.Round(sum / (scores.Count * 4.0), 3); // 0..1
        }

        private static bool AllMastery(System.Collections.Generic.IReadOnlyList<CriterionScore> scores)
        {
            foreach (var c in scores) if (!c.IsMastery) return false;
            return true;
        }

        private async Task Fire(StepResult r, string triggerId)
        {
            CoachUtterance u = await _triggers.Fire(new TriggerEvent
            {
                SessionId = "session",
                StationId = StationId,
                TriggerId = triggerId,
                Family = FamilyOf(triggerId),
                Mode = _mode,
                Target = "galaxy_xr"
            });
            r.CoachTriggerId = triggerId;
            r.CoachText = u?.Text;
            r.Emitted.Add(_xapi.ReceivedCoaching(triggerId));
        }

        private static TriggerFamily FamilyOf(string id)
        {
            if (id.StartsWith("CSS-SF")) return TriggerFamily.ScenarioFraming;
            if (id.StartsWith("CSS-DP")) return TriggerFamily.DecisionPoint;
            if (id.StartsWith("CSS-SG")) return TriggerFamily.SafetyGate;
            return TriggerFamily.Debrief;
        }

        private void RecordGateEvidence(string gateId)
        {
            switch (gateId)
            {
                case "CSS-SG-5": _ev.Sg5Fired = true; break;
                case "CSS-SG-6": _ev.Sg6Fired = true; break;
                case "CSS-SG-2":
                case "CSS-SG-3":
                    _ev.PediatricAdvancedWithoutPrereq = true; break;
            }
        }
    }
}
