using System.Linq;
using Encountive.Content;
using Encountive.Domain;
using Encountive.SafetyGates;
using Encountive.Stations;
using NUnit.Framework;

namespace Encountive.SafetyGates.Tests
{
    public sealed class PersonaLibraryTests
    {
        [Test]
        public void LibraryHasThreeAdultsAndEightPediatrics()
        {
            Assert.AreEqual(11, PersonaLibrary.All.Count);
            Assert.AreEqual(3, PersonaLibrary.Adults.Count);
            Assert.AreEqual(8, PersonaLibrary.All
                .Count(s => s.Persona.Population == PopulationClass.Pediatric));
        }

        [Test]
        public void AdultB_IsMargaritaDelgado_TheFullEncounterPersona()
        {
            Assert.AreEqual("Margarita Delgado", PersonaLibrary.AdultB.Persona.DisplayName);
        }

        [Test]
        public void BoundaryPersonas_AreFlagged()
        {
            Assert.IsTrue(PersonaLibrary.AdultC.Persona.IsBoundaryPersona);
            Assert.IsTrue(PersonaLibrary.PedF.Persona.IsBoundaryPersona);
        }

        [Test]
        public void ById_ResolvesAndIsNullForUnknown()
        {
            Assert.AreSame(PersonaLibrary.AdultA, PersonaLibrary.ById("adultA"));
            Assert.IsNull(PersonaLibrary.ById("nope"));
        }
    }

    public sealed class CaseParameterSamplerTests
    {
        [Test]
        public void SameCaseId_ReproducesIdenticalParameters()
        {
            var s = new CaseParameterSampler("v1");
            var a = s.Sample("case-001", PersonaLibrary.AdultA);
            var b = s.Sample("case-001", PersonaLibrary.AdultA);

            Assert.AreEqual(a.MuacCm, b.MuacCm);
            Assert.AreEqual(a.CaseId, b.CaseId);
            Assert.AreEqual("v1", a.ParameterSetVersion);
        }

        [Test]
        public void ParameterSetVersion_IsPartOfTheKey_AndStaysInBand()
        {
            var v1 = new CaseParameterSampler("v1").Sample("case-001", PersonaLibrary.AdultA);
            var v2 = new CaseParameterSampler("v2").Sample("case-001", PersonaLibrary.AdultA);
            Assert.AreEqual("v1", v1.ParameterSetVersion);
            Assert.AreEqual("v2", v2.ParameterSetVersion);
            foreach (var cp in new[] { v1, v2 })
            {
                Assert.GreaterOrEqual(cp.MuacCm, PersonaLibrary.AdultA.MuacMinCm);
                Assert.LessOrEqual(cp.MuacCm, PersonaLibrary.AdultA.MuacMaxCm);
            }
        }

        [Test]
        public void SampledMuac_StaysInsidePersonaBand_AndKeepsCuffClassStable()
        {
            var sampler = new CaseParameterSampler("v1");
            for (int i = 0; i < 200; i++)
            {
                var cp = sampler.Sample("case-" + i, PersonaLibrary.AdultA);
                Assert.GreaterOrEqual(cp.MuacCm, PersonaLibrary.AdultA.MuacMinCm);
                Assert.LessOrEqual(cp.MuacCm, PersonaLibrary.AdultA.MuacMaxCm);

                // AdultA is the canonical Adult band — every sample must
                // resolve to the Adult cuff with no boundary ambiguity.
                var rec = CuffSizeRules.Recommend(cp.Persona, cp.MuacCm);
                Assert.IsFalse(rec.IsBoundary, $"case-{i} muac {cp.MuacCm}");
                Assert.AreEqual(CuffClass.Adult, rec.PrimaryClass);
            }
        }

        [Test]
        public void PediatricPersona_MuacIsBandDriven_NotSampled()
        {
            var cp = new CaseParameterSampler().Sample("c1", PersonaLibrary.PedC);
            Assert.AreEqual(0.0, cp.MuacCm);
        }
    }

    public sealed class ModuleStateMachineTests
    {
        private static readonly Persona P =
            new Persona("adultB", "Margarita Delgado", PopulationClass.Adult);

        [Test]
        public void SingleStation_ReturnsToSelectorOnExit()
        {
            var m = new ModuleStateMachine();
            Assert.IsTrue(m.StartSingleStation("S2", P, TrainingMode.Guided));
            Assert.AreEqual(ModulePhase.InStation, m.Phase);

            m.CompleteStation();
            Assert.AreEqual(ModulePhase.Selector, m.Phase);
            Assert.IsNull(m.CurrentStationId);
        }

        [Test]
        public void SingleStation_RejectsFullEncounterMode()
        {
            var m = new ModuleStateMachine();
            Assert.IsFalse(m.StartSingleStation("S2", P, TrainingMode.FullEncounter));
        }

        [Test]
        public void FullEncounter_ChainsAllSixStations_WithOneLockedPersona()
        {
            var m = new ModuleStateMachine();
            Assert.IsTrue(m.StartFullEncounter(P));

            foreach (string expected in ModuleStateMachine.EncounterOrder)
            {
                Assert.AreEqual(expected, m.CurrentStationId);
                Assert.AreSame(P, m.PersonaForActiveStation());
                m.CompleteStation();
            }

            Assert.AreEqual(ModulePhase.Completed, m.Phase);
        }

        [Test]
        public void GateEscalation_AbortsEncounterToRemediation()
        {
            var m = new ModuleStateMachine();
            m.StartFullEncounter(P);
            m.CompleteStation();             // S1 -> S2
            Assert.AreEqual("S2", m.CurrentStationId);

            m.EscalateToRemediation("CSS-SG-1 identity skipped");
            Assert.AreEqual(ModulePhase.Remediation, m.Phase);
            Assert.AreEqual("CSS-SG-1 identity skipped", m.RemediationReason);

            // No further progression once in remediation.
            m.CompleteStation();
            Assert.AreEqual(ModulePhase.Remediation, m.Phase);
        }

        [Test]
        public void CannotStartTwice()
        {
            var m = new ModuleStateMachine();
            Assert.IsTrue(m.StartSingleStation("S2", P, TrainingMode.Practice));
            Assert.IsFalse(m.StartFullEncounter(P));
        }
    }
}
