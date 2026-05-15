using System.Collections.Generic;
using System.Threading.Tasks;
using Encountive.Domain;
using Encountive.Triggers;
using NUnit.Framework;

namespace Encountive.SafetyGates.Tests
{
    public sealed class TriggerCatalogTests
    {
        [Test]
        public void AllCuffSizeSelectionTriggersPresent()
        {
            // SDD §13: 5 SF + 3 DP + 8 SG + 1 DB = 17 triggers.
            Assert.AreEqual(17, CuffSizeSelectionCatalog.Entries.Count);

            string[] expected =
            {
                "CSS-SF-1","CSS-SF-2","CSS-SF-3","CSS-SF-4","CSS-SF-5",
                "CSS-DP-1","CSS-DP-2","CSS-DP-3",
                "CSS-SG-1","CSS-SG-2","CSS-SG-3","CSS-SG-4",
                "CSS-SG-5","CSS-SG-6","CSS-SG-7","CSS-SG-8",
                "CSS-DB-1"
            };
            foreach (string id in expected)
                Assert.IsTrue(CuffSizeSelectionCatalog.TryGet(id, out _), $"missing {id}");
        }

        [Test]
        public void EveryAuthoredLineIsWithinItsWordBudget()
        {
            // SDD Appendix B checklist: keep the coach unobtrusive.
            foreach (var e in CuffSizeSelectionCatalog.Entries)
                Assert.IsTrue(CuffSizeSelectionCatalog.WithinBudget(e),
                    $"{e.TriggerId} exceeds its {e.MaxSeconds}s budget " +
                    $"({CuffSizeSelectionCatalog.CountWords(e.AuthoredText)} words).");
        }

        [Test]
        public void SafetyGateRedirectsUse12To20SecondBudget()
        {
            foreach (var e in CuffSizeSelectionCatalog.Entries)
            {
                if (e.Family != TriggerFamily.SafetyGate) continue;
                Assert.AreEqual(12, e.MinSeconds, e.TriggerId);
                Assert.AreEqual(20, e.MaxSeconds, e.TriggerId);
            }
        }
    }

    public sealed class LocalFallbackTriggerClientTests
    {
        private static TriggerEvent Ev(string id) => new TriggerEvent
        {
            SessionId = "s1",
            StationId = "S2",
            Family = TriggerFamily.SafetyGate,
            TriggerId = id,
            Mode = TrainingMode.Guided,
            Target = "galaxy_xr",
            StateSnapshot = new Dictionary<string, string> { ["stage"] = "S4" }
        };

        [Test]
        public async Task KnownTrigger_ReturnsAuthoredCatalogText()
        {
            var client = new LocalFallbackTriggerClient(() => "fixed-id");
            CoachUtterance u = await client.Fire(Ev("CSS-SG-1"));

            CuffSizeSelectionCatalog.TryGet("CSS-SG-1", out var entry);
            Assert.AreEqual(entry.AuthoredText, u.Text);
            Assert.AreEqual("authored", u.Source);
            Assert.AreEqual("fixed-id", u.UtteranceId);
            Assert.AreEqual(LocalFallbackTriggerClient.PromptVersion, u.PromptTemplateVersion);
            Assert.AreEqual(VoicePlaybackKind.UsePrecached, u.VoiceKind);
            Assert.IsNull(u.AuditEntryId); // no AI surface in Phase 1
        }

        [Test]
        public async Task UnknownTrigger_ReturnsSafeFallback_NeverThrows()
        {
            var client = new LocalFallbackTriggerClient();
            CoachUtterance u = await client.Fire(Ev("CSS-DOES-NOT-EXIST"));

            Assert.IsNotNull(u);
            Assert.IsNotEmpty(u.Text);
            Assert.AreEqual("authored", u.Source);
        }

        [Test]
        public async Task NullEvent_ReturnsSafeFallback()
        {
            var client = new LocalFallbackTriggerClient();
            CoachUtterance u = await client.Fire(null);
            Assert.IsNotNull(u);
            Assert.IsNotEmpty(u.Text);
        }
    }
}
