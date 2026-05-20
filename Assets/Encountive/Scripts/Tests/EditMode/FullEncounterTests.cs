using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Encountive.Content;
using Encountive.Stations;
using Encountive.Telemetry;
using Encountive.Triggers;
using NUnit.Framework;

namespace Encountive.SafetyGates.Tests
{
    public sealed class FullEncounterOrchestratorTests
    {
        private static (FullEncounterOrchestrator, ModuleStateMachine,
            Dictionary<string, IStationRunner>, XApiStatementFactory, ITriggerClient) Setup()
        {
            var module = new ModuleStateMachine();
            var runners = new Dictionary<string, IStationRunner>();
            foreach (var s in Phase1StationScaffolds.NonStation2())
                runners[s.StationId] = s;
            // Station 2 — also use a passing scaffold for the encounter
            // test; the real Station 2 FSM is exercised by its own tests.
            runners["S2"] = new ScaffoldStation("S2", "Cuff Size Selection",
                scenarioFramingTrigger: "CSS-SF-1");

            return (new FullEncounterOrchestrator(), module, runners,
                FactoryHelpers.NewFactory(),
                new LocalFallbackTriggerClient(() => "u"));
        }

        [Test]
        public async Task FullEncounter_ChainsSixStations_WithPersonaLockedAndProfileValid()
        {
            var (orch, module, runners, xapi, triggers) = Setup();
            var validator = new StatementValidator();
            var persona = PersonaLibrary.AdultB.Persona; // Margarita Delgado

            FullEncounterResult result = await orch.RunAsync(
                module, runners, persona, xapi, triggers);

            Assert.IsTrue(result.Completed);
            Assert.IsFalse(result.Aborted);
            CollectionAssert.AreEqual(
                ModuleStateMachine.EncounterOrder,
                result.StationResults.Select(r => r.StationId).ToList());

            foreach (var s in result.AllEmitted)
            {
                var v = validator.Validate(s);
                Assert.IsTrue(v.Ok,
                    $"verb={s.Verb?.Id} object={s.Object?.Id} errors={string.Join("; ", v.Errors)}");
            }
        }

        [Test]
        public async Task FullEncounter_StationFailure_AbortsToRemediation()
        {
            var (orch, module, runners, xapi, triggers) = Setup();
            // Make S3 fail to simulate a downstream gate escalation.
            runners["S3"] = new ScaffoldStation("S3", "Cuff application",
                passes: false, scaledScore: 0.0);

            FullEncounterResult r = await orch.RunAsync(
                module, runners, PersonaLibrary.AdultB.Persona, xapi, triggers);

            Assert.IsFalse(r.Completed);
            Assert.IsTrue(r.Aborted);
            Assert.AreEqual("S3 failed", r.AbortReason);
            // S1, S2, S3 ran; S4-S6 did not.
            Assert.AreEqual(3, r.StationResults.Count);
        }
    }
}
