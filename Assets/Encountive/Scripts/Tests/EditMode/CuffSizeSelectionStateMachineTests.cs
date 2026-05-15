using System.Linq;
using System.Threading.Tasks;
using Encountive.Domain;
using Encountive.SafetyGates;
using Encountive.Stations;
using Encountive.Telemetry;
using Encountive.Triggers;
using NUnit.Framework;

namespace Encountive.SafetyGates.Tests
{
    public sealed class CuffSizeSelectionStateMachineTests
    {
        private static readonly Persona AdultA =
            new Persona("adultA", "Adult A", PopulationClass.Adult);
        private static readonly Persona AdultC =
            new Persona("adultC", "Adult C", PopulationClass.Adult, isBoundaryPersona: true);

        private static CuffSizeSelectionStateMachine New(Persona p, TrainingMode mode = TrainingMode.Guided)
        {
            int n = 0;
            var factory = new XApiStatementFactory(
                "learner", mode, "galaxy_xr",
                new FixedClock(), () => "x-" + (++n));
            return new CuffSizeSelectionStateMachine(
                new SafetyGateEngine(), new LocalFallbackTriggerClient(() => "u"),
                factory, p, mode);
        }

        [Test]
        public async Task GoldenPath_AdultA_CompletesWithFullMastery()
        {
            var sm = New(AdultA);
            await sm.Start();
            await sm.Step(CuffAction.VerifyIdentity);
            await sm.Step(CuffAction.GiveConsent);
            await sm.Step(CuffAction.IdentifyLandmarks,
                new CuffActionData { Accurate = true, FirstAttempt = true });
            await sm.Step(CuffAction.MeasureMuac,
                new CuffActionData { MuacCm = 30, Accurate = true });
            await sm.Step(CuffAction.CommitCuff,
                new CuffActionData { Commit = CuffClass.Adult });
            await sm.Step(CuffAction.CaptureRationale, new CuffActionData
            {
                RationaleText = "This person needs an Adult cuff; the bladder fits the AHA rule."
            });
            await sm.Step(CuffAction.ConfirmS5);
            StepResult final = await sm.Step(CuffAction.AdvanceToStation3);

            Assert.IsTrue(sm.Complete);
            Assert.IsTrue(final.StationComplete);
            Assert.IsNotNull(final.RubricScores);
            Assert.IsTrue(final.RubricScores.All(c => c.IsMastery),
                "every criterion should be >= 3 on the golden path");
        }

        [Test]
        public async Task SG1_BlocksThenResolves_OnIdentitySkip()
        {
            var sm = New(AdultA);
            await sm.Start();

            StepResult blocked = await sm.Step(CuffAction.IdentifyLandmarks);
            Assert.IsTrue(blocked.GateFired);
            Assert.AreEqual("CSS-SG-1", blocked.Gate.GateId);
            Assert.AreEqual(CssStage.S1_IdentityConsent, sm.Stage); // no advance

            await sm.Step(CuffAction.VerifyIdentity);
            StepResult ok = await sm.Step(CuffAction.IdentifyLandmarks,
                new CuffActionData { Accurate = true });

            Assert.IsFalse(ok.GateFired);
            Assert.IsTrue(ok.Emitted.Any(s =>
                s.Verb.Id == XApiVocabulary.SafetyGateResolved));
            Assert.AreEqual(CssStage.S3_Measurement, sm.Stage);
        }

        [Test]
        public async Task BoundaryPersona_RequiresDp3_BeforeCommit()
        {
            var sm = New(AdultC);
            await sm.Start();
            await sm.Step(CuffAction.VerifyIdentity);
            await sm.Step(CuffAction.GiveConsent);
            await sm.Step(CuffAction.IdentifyLandmarks, new CuffActionData { Accurate = true });
            await sm.Step(CuffAction.MeasureMuac, new CuffActionData { MuacCm = 30, Accurate = true });

            StepResult sg7 = await sm.Step(CuffAction.CommitCuff,
                new CuffActionData { Commit = CuffClass.Adult });
            Assert.IsTrue(sg7.GateFired);
            Assert.AreEqual("CSS-SG-7", sg7.Gate.GateId);

            await sm.Step(CuffAction.EngageDp3);
            StepResult ok = await sm.Step(CuffAction.CommitCuff,
                new CuffActionData { Commit = CuffClass.Adult });
            Assert.IsFalse(ok.GateFired);
            Assert.AreEqual(CssStage.S5_Confirmation, sm.Stage);
        }

        [Test]
        public async Task StigmatizingRationale_FiresSG6_AndZerosRubricCriterion()
        {
            var sm = New(AdultA);
            await sm.Start();
            await sm.Step(CuffAction.VerifyIdentity);
            await sm.Step(CuffAction.GiveConsent);
            await sm.Step(CuffAction.IdentifyLandmarks, new CuffActionData { Accurate = true });
            await sm.Step(CuffAction.MeasureMuac, new CuffActionData { MuacCm = 30, Accurate = true });
            await sm.Step(CuffAction.CommitCuff, new CuffActionData { Commit = CuffClass.Adult });

            StepResult sg6 = await sm.Step(CuffAction.CaptureRationale,
                new CuffActionData { RationaleText = "The diabetic obviously needs a bigger cuff." });
            Assert.IsTrue(sg6.GateFired);
            Assert.AreEqual("CSS-SG-6", sg6.Gate.GateId);

            await sm.Step(CuffAction.ConfirmS5);
            StepResult final = await sm.Step(CuffAction.AdvanceToStation3);

            var rationale = final.RubricScores.First(c => c.Criterion == "Rationale capture (S5)");
            Assert.AreEqual(0, rationale.Score);
        }

        [Test]
        public async Task ReplayDeterminism_SameInputsProduceSameXApiSequence()
        {
            async Task<string[]> Run()
            {
                var sm = New(AdultA);
                var verbs = new System.Collections.Generic.List<string>();
                void Collect(StepResult r) => verbs.AddRange(r.Emitted.Select(e => e.Verb.Id));

                Collect(await sm.Start());
                Collect(await sm.Step(CuffAction.VerifyIdentity));
                Collect(await sm.Step(CuffAction.GiveConsent));
                Collect(await sm.Step(CuffAction.IdentifyLandmarks, new CuffActionData { Accurate = true }));
                Collect(await sm.Step(CuffAction.MeasureMuac, new CuffActionData { MuacCm = 30, Accurate = true }));
                Collect(await sm.Step(CuffAction.CommitCuff, new CuffActionData { Commit = CuffClass.Adult }));
                Collect(await sm.Step(CuffAction.CaptureRationale,
                    new CuffActionData { RationaleText = "Adult cuff fits this person per the rule." }));
                Collect(await sm.Step(CuffAction.ConfirmS5));
                Collect(await sm.Step(CuffAction.AdvanceToStation3));
                return verbs.ToArray();
            }

            CollectionAssert.AreEqual(await Run(), await Run());
        }
    }
}
