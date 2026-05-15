using Encountive.Domain;
using Encountive.SafetyGates;
using NUnit.Framework;

namespace Encountive.SafetyGates.Tests
{
    /// <summary>
    /// Exercises every CSS-SG gate (1..8), the Full Encounter
    /// escalation path, gate priority ordering, and clean-pass paths
    /// across adult and pediatric personas and all modes (SDD §16.1).
    /// </summary>
    public sealed class SafetyGateEngineTests
    {
        private ISafetyGateEngine _engine;

        private static readonly Persona AdultA =
            new Persona("adultA", "Adult A", PopulationClass.Adult);
        private static readonly Persona AdultC =
            new Persona("adultC", "Adult C", PopulationClass.Adult, isBoundaryPersona: true);
        private static readonly Persona PedCalm =
            new Persona("pedE", "Ped E", PopulationClass.Pediatric, PediatricBand.Child);
        private static readonly Persona PedDistressed =
            new Persona("pedA", "Ped A", PopulationClass.Pediatric, PediatricBand.Infant,
                showsDistress: true);

        [SetUp]
        public void SetUp() => _engine = new SafetyGateEngine();

        private static GateInput Valid(LearnerAction action, Persona persona = null) =>
            new GateInput
            {
                Action = action,
                Stage = CssStage.S4_CuffSelection,
                Mode = TrainingMode.Guided,
                Persona = persona ?? AdultA,
                MuacCm = 30.0,
                IdentityVerified = true,
                ConsentObtained = true,
                AssentObtained = true,
                CalmStateAchieved = true,
                ArmExposed = true,
                CommittedCuffClass = CuffClass.Adult,
                Dp3Engaged = false,
                S5Confirmed = true,
                RationaleText = "Adult cuff fits this person's mid-arm circumference."
            };

        [Test]
        public void SG1_IdentityNotVerified_Fires()
        {
            var input = Valid(LearnerAction.AttemptPatientContact);
            input.IdentityVerified = false;

            GateResolution r = _engine.Evaluate(input);

            Assert.AreEqual(GateResolutionKind.Fire, r.Kind);
            Assert.AreEqual("CSS-SG-1", r.GateId);
            Assert.AreEqual(CssStage.S1_IdentityConsent, r.RedirectStage);
        }

        [Test]
        public void SG1_IdentitySkippedInFullEncounter_Escalates()
        {
            var input = Valid(LearnerAction.AttemptPatientContact);
            input.IdentityVerified = false;
            input.Mode = TrainingMode.FullEncounter;

            GateResolution r = _engine.Evaluate(input);

            Assert.AreEqual(GateResolutionKind.Escalate, r.Kind);
            Assert.AreEqual("CSS-SG-1", r.GateId);
        }

        [Test]
        public void SG2_PediatricMeasurementWithoutAssent_Fires()
        {
            var input = Valid(LearnerAction.AttemptMeasurement, PedCalm);
            input.AssentObtained = false;

            GateResolution r = _engine.Evaluate(input);

            Assert.AreEqual("CSS-SG-2", r.GateId);
            Assert.AreEqual(GateResolutionKind.Fire, r.Kind);
        }

        [Test]
        public void SG3_DistressedPediatricNotCalmed_Fires()
        {
            var input = Valid(LearnerAction.AttemptMeasurement, PedDistressed);
            input.CalmStateAchieved = false;

            GateResolution r = _engine.Evaluate(input);

            Assert.AreEqual("CSS-SG-3", r.GateId);
        }

        [Test]
        public void SG3_DoesNotFireForCalmPediatric()
        {
            var input = Valid(LearnerAction.AttemptMeasurement, PedCalm);
            input.CalmStateAchieved = false; // irrelevant: persona not distressed

            GateResolution r = _engine.Evaluate(input);

            Assert.AreEqual(GateResolutionKind.Pass, r.Kind);
        }

        [Test]
        public void SG4_CuffOverClothing_Fires()
        {
            var input = Valid(LearnerAction.AttemptCuffApplication);
            input.ArmExposed = false;

            GateResolution r = _engine.Evaluate(input);

            Assert.AreEqual("CSS-SG-4", r.GateId);
            Assert.AreEqual(CssStage.S2_Landmarks, r.RedirectStage);
        }

        [Test]
        public void SG5_WrongCuffClassCommitted_Fires()
        {
            var input = Valid(LearnerAction.CommitCuffClass);
            input.CommittedCuffClass = CuffClass.AdultThigh; // wrong for MUAC 30

            GateResolution r = _engine.Evaluate(input);

            Assert.AreEqual("CSS-SG-5", r.GateId);
        }

        [Test]
        public void SG5_CorrectCuffClassCommitted_Passes()
        {
            GateResolution r = _engine.Evaluate(Valid(LearnerAction.CommitCuffClass));

            Assert.AreEqual(GateResolutionKind.Pass, r.Kind);
        }

        [Test]
        public void SG6_StigmatizingRationale_Fires()
        {
            var input = Valid(LearnerAction.CaptureRationale);
            input.RationaleText = "The diabetic needs a larger cuff.";

            GateResolution r = _engine.Evaluate(input);

            Assert.AreEqual("CSS-SG-6", r.GateId);
            Assert.AreEqual(CssStage.S5_Confirmation, r.RedirectStage);
        }

        [Test]
        public void SG6_PersonFirstRationale_Passes()
        {
            var input = Valid(LearnerAction.CaptureRationale);
            input.RationaleText = "This person with diabetes has a mid-arm circumference of 30 cm.";

            GateResolution r = _engine.Evaluate(input);

            Assert.AreEqual(GateResolutionKind.Pass, r.Kind);
        }

        [Test]
        public void SG7_BoundaryCommitWithoutDp3_Fires()
        {
            var input = Valid(LearnerAction.CommitCuffClass, AdultC);
            input.Dp3Engaged = false;

            GateResolution r = _engine.Evaluate(input);

            Assert.AreEqual("CSS-SG-7", r.GateId);
            Assert.AreEqual(CssStage.S4_CuffSelection, r.RedirectStage);
        }

        [Test]
        public void SG7_BoundaryCommitWithDp3Engaged_Passes()
        {
            var input = Valid(LearnerAction.CommitCuffClass, AdultC);
            input.Dp3Engaged = true;
            input.CommittedCuffClass = CuffClass.Adult; // acceptable on boundary

            GateResolution r = _engine.Evaluate(input);

            Assert.AreEqual(GateResolutionKind.Pass, r.Kind);
        }

        [Test]
        public void SG7_TakesPriorityOverSG5_OnBoundary()
        {
            // Boundary persona AND a class that would fail SG-5: the
            // boundary lesson (SG-7) must win.
            var input = Valid(LearnerAction.CommitCuffClass, AdultC);
            input.Dp3Engaged = false;
            input.CommittedCuffClass = CuffClass.AdultThigh;

            GateResolution r = _engine.Evaluate(input);

            Assert.AreEqual("CSS-SG-7", r.GateId);
        }

        [Test]
        public void SG8_AdvanceWithoutS5Confirmation_Fires()
        {
            var input = Valid(LearnerAction.AttemptAdvanceToStation3);
            input.S5Confirmed = false;

            GateResolution r = _engine.Evaluate(input);

            Assert.AreEqual("CSS-SG-8", r.GateId);
        }

        [Test]
        public void Priority_IdentityBeatsEveryOtherViolation()
        {
            // Multiple violations present; identity must surface first.
            var input = Valid(LearnerAction.CommitCuffClass, PedDistressed);
            input.IdentityVerified = false;
            input.AssentObtained = false;
            input.CalmStateAchieved = false;
            input.CommittedCuffClass = CuffClass.AdultThigh;

            GateResolution r = _engine.Evaluate(input);

            Assert.AreEqual("CSS-SG-1", r.GateId);
        }

        [TestCase(TrainingMode.Guided)]
        [TestCase(TrainingMode.Practice)]
        [TestCase(TrainingMode.Evaluation)]
        [TestCase(TrainingMode.FullEncounter)]
        public void CleanHappyPath_PassesInEveryMode(TrainingMode mode)
        {
            var input = Valid(LearnerAction.CommitCuffClass);
            input.Mode = mode;

            Assert.AreEqual(GateResolutionKind.Pass, _engine.Evaluate(input).Kind);
        }
    }
}
