using Encountive.Domain;

namespace Encountive.SafetyGates
{
    /// <summary>
    /// Abstracted learner state handed to the gate engine. Carries no
    /// raw biometrics (SDD §2.5.2) — only the booleans and values the
    /// deterministic rules need.
    /// </summary>
    public sealed class GateInput
    {
        public LearnerAction Action { get; set; }
        public CssStage Stage { get; set; }
        public TrainingMode Mode { get; set; }
        public Persona Persona { get; set; }
        public double MuacCm { get; set; }

        public bool IdentityVerified { get; set; }
        public bool ConsentObtained { get; set; }

        // Pediatric prerequisites (Gate-1a / Gate-1b).
        public bool AssentObtained { get; set; }
        public bool CalmStateAchieved { get; set; }

        public bool ArmExposed { get; set; }

        // S4 commit context.
        public CuffClass CommittedCuffClass { get; set; }
        public bool Dp3Engaged { get; set; }

        // S5 context.
        public bool S5Confirmed { get; set; }
        public string RationaleText { get; set; }
    }

    /// <summary>
    /// Gate engine verdict. On <see cref="GateResolutionKind.Fire"/> or
    /// <see cref="GateResolutionKind.Escalate"/>, <see cref="GateId"/> is
    /// the CSS-SG-* identifier, <see cref="RedirectStage"/> is where the
    /// per-station FSM sends the learner, and <see cref="CoachTriggerId"/>
    /// is the trigger requested through the standard (fallback-capable)
    /// path — never an AI decision (SDD §8.5).
    /// </summary>
    public sealed class GateResolution
    {
        public GateResolutionKind Kind { get; }
        public string GateId { get; }
        public CssStage RedirectStage { get; }
        public string CoachTriggerId { get; }
        public string Reason { get; }

        private GateResolution(
            GateResolutionKind kind, string gateId,
            CssStage redirectStage, string coachTriggerId, string reason)
        {
            Kind = kind;
            GateId = gateId;
            RedirectStage = redirectStage;
            CoachTriggerId = coachTriggerId;
            Reason = reason;
        }

        public static readonly GateResolution Pass =
            new GateResolution(GateResolutionKind.Pass, null, default, null, "pass");

        public static GateResolution Fire(
            string gateId, CssStage redirectStage, string coachTriggerId, string reason) =>
            new GateResolution(GateResolutionKind.Fire, gateId, redirectStage, coachTriggerId, reason);

        public static GateResolution Escalate(
            string gateId, CssStage redirectStage, string coachTriggerId, string reason) =>
            new GateResolution(GateResolutionKind.Escalate, gateId, redirectStage, coachTriggerId, reason);
    }

    /// <summary>SDD §7.4 — the deterministic safety-gate contract.
    /// Pure C#, no Unity dependency, 100% unit-test coverage.</summary>
    public interface ISafetyGateEngine
    {
        GateResolution Evaluate(GateInput input);
    }
}
