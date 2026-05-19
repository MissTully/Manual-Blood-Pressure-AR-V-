using System.Collections.Generic;
using Encountive.Telemetry;
using NUnit.Framework;

namespace Encountive.SafetyGates.Tests
{
    public sealed class StatementValidatorTests
    {
        private readonly StatementValidator _v = new StatementValidator();

        private static XApiStatement Valid()
        {
            var f = FactoryHelpers.NewFactory();
            f.EnterLesson("S2", "Cuff Size Selection");
            return f.SelectedCuff("adultA", "Adult A",
                cuffId: "manuf-A-adult-001", cuffSizeClass: "adult",
                selectionCorrect: true);
        }

        [Test]
        public void AcceptsAWellFormedStatementFromTheFactory()
        {
            var r = _v.Validate(Valid());
            Assert.IsTrue(r.Ok, string.Join("; ", r.Errors));
        }

        [Test]
        public void RejectsNonV4UuidId()
        {
            var s = Valid();
            s.Id = "not-a-uuid";
            Assert.IsFalse(_v.Validate(s).Ok);
        }

        [Test]
        public void RejectsTimestampWithoutMilliseconds()
        {
            var s = Valid();
            s.Timestamp = "2026-05-15T14:23:11Z";
            Assert.IsFalse(_v.Validate(s).Ok);
        }

        [Test]
        public void RejectsVerbOutsideProfileVocabulary()
        {
            var s = Valid();
            s.Verb.Id = "https://example.com/verbs/invented";
            Assert.IsFalse(_v.Validate(s).Ok);
        }

        [Test]
        public void RejectsVerbObjectMismatch()
        {
            // measured verb on a cuff-selection activity (spec §10.1
            // explicit example).
            var s = Valid();
            s.Verb.Id = XApiVocabulary.Measured;
            Assert.IsFalse(_v.Validate(s).Ok);
        }

        [Test]
        public void RejectsOutcomeVerbWithoutScaledScore()
        {
            var s = Valid();
            s.Result.Score = null;
            Assert.IsFalse(_v.Validate(s).Ok);
        }

        [Test]
        public void RejectsScaledScoreOutsideMinusOneToOne()
        {
            var s = Valid();
            s.Result.Score.Scaled = 1.5;
            Assert.IsFalse(_v.Validate(s).Ok);
        }

        [Test]
        public void RejectsStatementMissingProfileCategory()
        {
            var s = Valid();
            s.Context.ContextActivities.Category = null;
            Assert.IsFalse(_v.Validate(s).Ok);
        }

        [Test]
        public void RejectsTrialLevelStatementMissingGrouping()
        {
            var s = Valid();
            s.Context.ContextActivities.Grouping = null;
            Assert.IsFalse(_v.Validate(s).Ok);
        }

        [Test]
        public void SessionLifecycle_IsExemptFromGrouping()
        {
            var init = FactoryHelpers.NewFactory().Initialized();
            init.Context.ContextActivities.Grouping = null;
            Assert.IsTrue(_v.Validate(init).Ok,
                "initialized/terminated operate on the session activity itself");
        }

        [Test]
        public void RejectsEmailLikeAccountName()
        {
            var s = Valid();
            s.Actor.Account.Name = "learner@example.edu";
            Assert.IsFalse(_v.Validate(s).Ok);
        }

        [Test]
        public void RejectsNonFiniteNumericExtensions()
        {
            var s = Valid();
            s.Result.Extensions[XApiVocabulary.ExtCuffBladderWidthCm] = double.NaN;
            Assert.IsFalse(_v.Validate(s).Ok);
        }

        [Test]
        public void RejectsForbiddenRawBiometricExtensions()
        {
            var s = Valid();
            s.Result.Extensions[XApiVocabulary.ExtBase + "raw-gaze-stream"] =
                new List<double> { 0.1, 0.2 };
            Assert.IsFalse(_v.Validate(s).Ok);
        }

        [Test]
        public void RejectsForbiddenAudioTranscript()
        {
            var s = Valid();
            s.Result.Extensions[XApiVocabulary.ExtBase + "voice-transcript"] = "hello";
            Assert.IsFalse(_v.Validate(s).Ok);
        }
    }
}
