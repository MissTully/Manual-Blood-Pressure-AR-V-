using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Encountive.Domain;
using Encountive.Telemetry;
using NUnit.Framework;

namespace Encountive.SafetyGates.Tests
{
    internal sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } =
            new DateTimeOffset(2026, 5, 15, 14, 23, 11, 527, TimeSpan.Zero);
    }

    /// <summary>Sink whose availability is toggled to simulate a network
    /// outage and reconnect (SDD §3.2.2).</summary>
    internal sealed class FlakySink : IXApiSink
    {
        public bool Online = true;
        public readonly List<XApiStatement> Stored = new List<XApiStatement>();
        public int StoreCalls;

        public Task<bool> TryStoreAsync(IReadOnlyList<XApiStatement> batch)
        {
            StoreCalls++;
            if (!Online) return Task.FromResult(false);
            Stored.AddRange(batch);
            return Task.FromResult(true);
        }
    }

    public sealed class XApiStatementFactoryTests
    {
        private XApiStatementFactory NewFactory(int idStart = 0)
        {
            int n = idStart;
            return new XApiStatementFactory(
                "learner-uuid", TrainingMode.Guided, "galaxy_xr",
                new FixedClock(), () => "id-" + (++n));
        }

        [Test]
        public void StatementsAreVersion103_WithModeAndTargetOnContext()
        {
            var s = NewFactory().StationEntered("S2");

            Assert.AreEqual("1.0.3", s.Version);
            Assert.AreEqual("guided", s.Context.Extensions[XApiVocabulary.ExtMode]);
            Assert.AreEqual("galaxy_xr", s.Context.Extensions[XApiVocabulary.ExtTarget]);
            Assert.AreEqual("2026-05-15T14:23:11.527Z", s.Timestamp);
            Assert.AreEqual("learner-uuid", s.Actor.Account.Name);
        }

        [Test]
        public void DecisionCommitted_CarriesPersonaCuffAndMuac()
        {
            var s = NewFactory().DecisionCommitted("S2", "S4", "adultB", "Adult Large", 38);

            Assert.AreEqual(XApiVocabulary.DecisionCommitted, s.Verb.Id);
            Assert.AreEqual("adultB", s.Context.Extensions[XApiVocabulary.ExtPersona]);
            Assert.AreEqual("Adult Large", s.Context.Extensions[XApiVocabulary.ExtCuffClassSelected]);
            Assert.AreEqual(38.0, s.Context.Extensions[XApiVocabulary.ExtMuacCm]);
            Assert.IsTrue(s.Result.Success);
        }

        [Test]
        public void SafetyGateFired_TagsGateId()
        {
            var s = NewFactory().SafetyGateFired("S2", "CSS-SG-5");
            Assert.AreEqual("CSS-SG-5", s.Context.Extensions[XApiVocabulary.ExtGateId]);
            Assert.AreEqual(XApiVocabulary.SafetyGateFired, s.Verb.Id);
        }
    }

    public sealed class XApiEmitterTests
    {
        private static XApiStatement Stmt(string id) =>
            new XApiStatement { Id = id, Verb = new XApiVerb { Id = "v" } };

        [Test]
        public async Task FlushDrainsQueue_WhenSinkOnline()
        {
            var sink = new FlakySink();
            var em = new XApiEmitter(sink);
            em.Emit(Stmt("a"));
            em.Emit(Stmt("b"));

            Assert.IsTrue(await em.FlushAsync());
            Assert.AreEqual(0, em.PendingCount);
            Assert.AreEqual(2, sink.Stored.Count);
        }

        [Test]
        public async Task OfflineThenReconnect_FlushesWithNoDataHole()
        {
            var sink = new FlakySink { Online = false };
            var em = new XApiEmitter(sink);
            em.Emit(Stmt("a"));
            em.Emit(Stmt("b"));

            Assert.IsFalse(await em.FlushAsync());   // outage
            Assert.AreEqual(2, em.PendingCount);     // retained

            sink.Online = true;
            Assert.IsTrue(await em.FlushAsync());     // reconnect
            Assert.AreEqual(0, em.PendingCount);
            CollectionAssert.AreEqual(
                new[] { "a", "b" },
                sink.Stored.ConvertAll(s => s.Id));   // original order
        }

        [Test]
        public void Emit_IsIdempotentOnStatementId()
        {
            var em = new XApiEmitter(new FlakySink());
            em.Emit(Stmt("dup"));
            em.Emit(Stmt("dup"));
            Assert.AreEqual(1, em.PendingCount);
        }

        [Test]
        public void Emit_NeverThrowsOnNullOrEmptyId()
        {
            var em = new XApiEmitter(new FlakySink());
            Assert.DoesNotThrow(() => em.Emit(null));
            Assert.DoesNotThrow(() => em.Emit(new XApiStatement { Id = "" }));
            Assert.AreEqual(0, em.PendingCount);
        }

        [Test]
        public async Task BatchesAreCappedAtMax()
        {
            var sink = new FlakySink();
            var em = new XApiEmitter(sink, maxBatch: 2);
            for (int i = 0; i < 5; i++) em.Emit(Stmt("s" + i));

            Assert.IsTrue(await em.FlushAsync());
            Assert.AreEqual(3, sink.StoreCalls); // 2 + 2 + 1
            Assert.AreEqual(5, sink.Stored.Count);
        }
    }
}
