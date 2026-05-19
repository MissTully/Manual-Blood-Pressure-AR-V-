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

    internal static class FactoryHelpers
    {
        public const string SessionUuid = "a4d1e2c0-3f7b-4d8a-9b1f-2e6e1c7c0000";
        public const string StatementUuid = "a4d1e2c0-3f7b-4d8a-9b1f-2e6e1c7c0001";

        public static XApiSessionInfo Session(string sessionId = SessionUuid) =>
            new XApiSessionInfo(
                sessionId: sessionId,
                learnerPseudonym: "learner-7af3e2",
                institutionalLearnerId: "reva-cohort-12-learner-074",
                sdkVersion: "androidxr-dp3",
                appVersion: "0.1.0",
                audienceTag: "vma",
                contentVersion: "1.0.0");

        public static XApiStatementFactory NewFactory()
        {
            int n = 0;
            return new XApiStatementFactory(
                Session(), new FixedClock(),
                () => "a4d1e2c0-3f7b-4d8a-9b1f-" + (++n).ToString("x12"));
        }
    }

    public sealed class XApiStatementFactoryTests
    {
        [Test]
        public void EveryStatement_CarriesProfileCategoryAndRegistration()
        {
            var s = FactoryHelpers.NewFactory().Initialized();

            Assert.AreEqual("1.0.3", s.Version);
            Assert.AreEqual(FactoryHelpers.SessionUuid, s.Context.Registration);
            Assert.AreEqual(XApiVocabulary.Platform, s.Context.Platform);
            Assert.AreEqual("en-US", s.Context.Language);

            Assert.IsNotNull(s.Context.ContextActivities.Category);
            Assert.AreEqual(XApiVocabulary.ProfileIri,
                s.Context.ContextActivities.Category[0].Id);
            Assert.AreEqual("v1.0",
                s.Context.Extensions[XApiVocabulary.ExtProfileVersion]);
            Assert.AreEqual("galaxy-xr",
                s.Context.Extensions[XApiVocabulary.ExtDeviceModel]);
        }

        [Test]
        public void Actor_IsPseudonymous_AndUsesLearnersHomePage()
        {
            var s = FactoryHelpers.NewFactory().Initialized();
            Assert.AreEqual("Agent", s.Actor.ObjectType);
            Assert.AreEqual("learner-7af3e2", s.Actor.Name);
            Assert.AreEqual(XApiVocabulary.LearnersHomePage, s.Actor.Account.HomePage);
            Assert.AreEqual("reva-cohort-12-learner-074", s.Actor.Account.Name);
        }

        [Test]
        public void Initialized_TargetsSessionActivityType()
        {
            var s = FactoryHelpers.NewFactory().Initialized();
            Assert.AreEqual(XApiVocabulary.Initialized, s.Verb.Id);
            Assert.AreEqual(XApiVocabulary.ActivityTypeSession, s.Object.Definition.Type);
            Assert.IsTrue(s.Object.Id.StartsWith(XApiVocabulary.ActivityBase + "session/"));
        }

        [Test]
        public void EnterLesson_SetsParentForSubsequentChildStatements()
        {
            var f = FactoryHelpers.NewFactory();
            f.EnterLesson("S2", "Cuff Size Selection");

            var sel = f.SelectedCuff("adultB", "Cuff selection for Margarita",
                cuffId: "manuf-A-large-adult", cuffSizeClass: "large-adult",
                selectionCorrect: true);

            Assert.IsNotNull(sel.Context.ContextActivities.Parent);
            Assert.AreEqual(f.CurrentLessonActivityId,
                sel.Context.ContextActivities.Parent[0].Id);
            Assert.AreEqual(XApiVocabulary.ActivityTypeCuffSelection,
                sel.Object.Definition.Type);
        }

        [Test]
        public void SelectedCuff_CarriesProfileExtensions_AndScaledScore()
        {
            var f = FactoryHelpers.NewFactory();
            f.EnterLesson("S2", "Cuff Size Selection");

            var s = f.SelectedCuff("adultA", "Adult A",
                cuffId: "manuf-A-adult-001", cuffSizeClass: "adult",
                selectionCorrect: true, bladderWidthCm: 13.0, bladderLengthCm: 24.0);

            Assert.AreEqual(XApiVocabulary.SelectedCuff, s.Verb.Id);
            Assert.IsTrue(s.Result.Success);
            Assert.AreEqual(1.0, s.Result.Score.Scaled);
            Assert.AreEqual("manuf-A-adult-001",
                s.Result.Extensions[XApiVocabulary.ExtCuffId]);
            Assert.AreEqual(true,
                s.Result.Extensions[XApiVocabulary.ExtSelectionCorrect]);
        }

        [Test]
        public void ReceivedCoaching_EncodesTriggerIdInActivityIri()
        {
            var f = FactoryHelpers.NewFactory();
            f.EnterLesson("S2", "Cuff Size Selection");

            var c = f.ReceivedCoaching("CSS-SG-1");
            Assert.AreEqual(XApiVocabulary.ReceivedCoaching, c.Verb.Id);
            Assert.AreEqual(
                XApiVocabulary.ActivityBase + "coaching-prompt/CSS-SG-1",
                c.Object.Id);
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

            Assert.IsFalse(await em.FlushAsync());
            Assert.AreEqual(2, em.PendingCount);

            sink.Online = true;
            Assert.IsTrue(await em.FlushAsync());
            Assert.AreEqual(0, em.PendingCount);
            CollectionAssert.AreEqual(
                new[] { "a", "b" },
                sink.Stored.ConvertAll(s => s.Id));
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
