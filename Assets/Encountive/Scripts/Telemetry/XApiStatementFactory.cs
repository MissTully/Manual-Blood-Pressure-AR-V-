using System;
using System.Collections.Generic;
using Encountive.Domain;

namespace Encountive.Telemetry
{
    /// <summary>Session-level metadata stamped on every emitted statement
    /// (AR BP Cuff Trainer xAPI profile §3, §7, §8.6).</summary>
    public sealed class XApiSessionInfo
    {
        public string SessionId { get; }                // registration UUID v4
        public string LearnerPseudonym { get; }         // actor.name
        public string InstitutionalLearnerId { get; }   // account.name
        public string LearnersHomePage { get; }
        public string Platform { get; }
        public string Language { get; }
        public string ProfileVersion { get; }
        public string DeviceModel { get; }
        public string SdkVersion { get; }
        public string AppVersion { get; }
        public string AudienceTag { get; }              // vma | clinical | student
        public string ContentVersion { get; }

        public XApiSessionInfo(
            string sessionId, string learnerPseudonym, string institutionalLearnerId,
            string profileVersion = "v1.0",
            string deviceModel = "galaxy-xr",
            string sdkVersion = null,
            string appVersion = null,
            string audienceTag = null,
            string contentVersion = null,
            string platform = XApiVocabulary.Platform,
            string language = "en-US",
            string learnersHomePage = XApiVocabulary.LearnersHomePage)
        {
            SessionId = sessionId;
            LearnerPseudonym = learnerPseudonym;
            InstitutionalLearnerId = institutionalLearnerId;
            LearnersHomePage = learnersHomePage;
            Platform = platform;
            Language = language;
            ProfileVersion = profileVersion;
            DeviceModel = deviceModel;
            SdkVersion = sdkVersion;
            AppVersion = appVersion;
            AudienceTag = audienceTag;
            ContentVersion = contentVersion;
        }

        public string SessionActivityId =>
            XApiVocabulary.ActivityBase + "session/" + SessionId;
    }

    /// <summary>
    /// Builds statements that conform to the AR BP Cuff Trainer xAPI
    /// profile v1. Time and statement id are injected so a replayed
    /// session reproduces byte-identical telemetry (SDD §16.6).
    ///
    /// The current lesson (set via <see cref="EnterLesson"/>) is tracked
    /// so trial-level statements automatically include the lesson as
    /// <c>contextActivities.parent</c> and the session as
    /// <c>contextActivities.grouping</c>, with the profile IRI as
    /// <c>contextActivities.category</c> (spec §7).
    /// </summary>
    public sealed class XApiStatementFactory
    {
        private readonly IClock _clock;
        private readonly Func<string> _idFactory;
        private readonly XApiSessionInfo _session;
        private string _currentLessonActivityId;

        public XApiStatementFactory(XApiSessionInfo session, IClock clock = null, Func<string> idFactory = null)
        {
            _session = session;
            _clock = clock ?? new SystemClock();
            _idFactory = idFactory ?? (() => Guid.NewGuid().ToString());
        }

        public string CurrentLessonActivityId => _currentLessonActivityId;

        /// <summary>Records the active lesson activity for subsequent
        /// trial/cuff/measurement statements. Returns the lesson
        /// <c>attempted</c> statement.</summary>
        public XApiStatement EnterLesson(string lessonId, string displayName)
        {
            _currentLessonActivityId = XApiVocabulary.ActivityBase + "lesson/" + lessonId;
            return Build(XApiVocabulary.Attempted, "attempted",
                XApiVocabulary.ActivityTypeLesson, _currentLessonActivityId, displayName);
        }

        public XApiStatement Initialized() =>
            Build(XApiVocabulary.Initialized, "initialized",
                XApiVocabulary.ActivityTypeSession, _session.SessionActivityId,
                "AR BP Cuff Trainer Session");

        public XApiStatement Terminated() =>
            Build(XApiVocabulary.Terminated, "terminated",
                XApiVocabulary.ActivityTypeSession, _session.SessionActivityId,
                "AR BP Cuff Trainer Session");

        public XApiStatement PassedLesson(string lessonId, string displayName, double scaled, string duration = null)
        {
            var s = Build(XApiVocabulary.Passed, "passed",
                XApiVocabulary.ActivityTypeLesson,
                XApiVocabulary.ActivityBase + "lesson/" + lessonId, displayName);
            s.Result = Outcome(scaled, true, duration);
            return s;
        }

        public XApiStatement FailedLesson(string lessonId, string displayName, double scaled, string duration = null)
        {
            var s = Build(XApiVocabulary.Failed, "failed",
                XApiVocabulary.ActivityTypeLesson,
                XApiVocabulary.ActivityBase + "lesson/" + lessonId, displayName);
            s.Result = Outcome(scaled, false, duration);
            return s;
        }

        /// <summary>Cuff selection commit (profile §4.2, §8.3). The
        /// activity id encodes the trial/case so consumers can aggregate
        /// across learners (spec §5.2 deterministic IRIs).</summary>
        public XApiStatement SelectedCuff(
            string caseOrTrialId, string displayName,
            string cuffId, string cuffSizeClass, bool selectionCorrect,
            double? bladderWidthCm = null, double? bladderLengthCm = null,
            IList<string> alternativesConsidered = null,
            string duration = null, string responseSummary = null)
        {
            var s = Build(XApiVocabulary.SelectedCuff, "selected cuff",
                XApiVocabulary.ActivityTypeCuffSelection,
                XApiVocabulary.ActivityBase + "cuff-selection/" + caseOrTrialId, displayName);

            var ext = new Dictionary<string, object>
            {
                [XApiVocabulary.ExtCuffId] = cuffId,
                [XApiVocabulary.ExtCuffSizeClass] = cuffSizeClass,
                [XApiVocabulary.ExtSelectionCorrect] = selectionCorrect,
                [XApiVocabulary.ExtAlternativeCuffsConsidered] =
                    alternativesConsidered ?? new List<string>()
            };
            if (bladderWidthCm.HasValue) ext[XApiVocabulary.ExtCuffBladderWidthCm] = bladderWidthCm.Value;
            if (bladderLengthCm.HasValue) ext[XApiVocabulary.ExtCuffBladderLengthCm] = bladderLengthCm.Value;

            s.Result = new XApiResult
            {
                Success = selectionCorrect,
                Completion = true,
                Score = new XApiScore { Scaled = selectionCorrect ? 1.0 : 0.0 },
                Duration = duration,
                Response = responseSummary,
                Extensions = ext
            };
            return s;
        }

        /// <summary>Coach utterance delivery (profile §4.2). The trigger
        /// id is encoded into the coaching-prompt activity IRI so the
        /// consumer can recover it without a custom result extension.</summary>
        public XApiStatement ReceivedCoaching(string triggerId, string displayName = null)
        {
            return Build(XApiVocabulary.ReceivedCoaching, "received coaching",
                XApiVocabulary.ActivityTypeCoachingPrompt,
                XApiVocabulary.ActivityBase + "coaching-prompt/" + triggerId,
                displayName ?? triggerId);
        }

        public XApiStatement Measured(
            string measurementId, string displayName,
            double reportedCircumferenceCm, double measurementErrorCm,
            double scaled, int attempts = 1, string duration = null)
        {
            var s = Build(XApiVocabulary.Measured, "measured",
                XApiVocabulary.ActivityTypeMeasurement,
                XApiVocabulary.ActivityBase + "measurement/" + measurementId, displayName);
            s.Result = new XApiResult
            {
                Success = System.Math.Abs(measurementErrorCm) < 1.0,
                Completion = true,
                Score = new XApiScore { Scaled = scaled, Raw = measurementErrorCm },
                Duration = duration,
                Extensions = new Dictionary<string, object>
                {
                    [XApiVocabulary.ExtReportedCircumferenceCm] = reportedCircumferenceCm,
                    [XApiVocabulary.ExtMeasurementErrorCm] = measurementErrorCm,
                    [XApiVocabulary.ExtMeasurementAttempts] = attempts
                }
            };
            return s;
        }

        // ------------------------------------------------------------
        private XApiStatement Build(
            string verbId, string verbDisplay,
            string activityType, string activityId, string activityName)
        {
            var s = new XApiStatement
            {
                Id = _idFactory(),
                Timestamp = _clock.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                Actor = new XApiActor
                {
                    Name = _session.LearnerPseudonym,
                    Account = new XApiAccount
                    {
                        HomePage = _session.LearnersHomePage,
                        Name = _session.InstitutionalLearnerId
                    }
                },
                Verb = new XApiVerb { Id = verbId },
                Object = new XApiActivity
                {
                    Id = activityId,
                    Definition = new XApiActivityDefinition { Type = activityType }
                },
                Context = new XApiContext
                {
                    Registration = _session.SessionId,
                    Platform = _session.Platform,
                    Language = _session.Language,
                    ContextActivities = new XApiContextActivities
                    {
                        Category = new List<XApiActivityRef>
                        {
                            new XApiActivityRef(XApiVocabulary.ProfileIri)
                        },
                        Grouping = new List<XApiActivityRef>
                        {
                            new XApiActivityRef(_session.SessionActivityId)
                        }
                    }
                }
            };
            s.Verb.Display["en-US"] = verbDisplay;
            s.Object.Definition.Name["en-US"] = activityName;

            // Parent = current lesson, for trial-/cuff-/measurement-level
            // statements (spec §7).
            bool nonRoot = activityType != XApiVocabulary.ActivityTypeSession
                        && activityType != XApiVocabulary.ActivityTypeLesson;
            if (nonRoot && _currentLessonActivityId != null)
            {
                s.Context.ContextActivities.Parent = new List<XApiActivityRef>
                {
                    new XApiActivityRef(_currentLessonActivityId)
                };
            }

            var ext = s.Context.Extensions;
            ext[XApiVocabulary.ExtProfileVersion] = _session.ProfileVersion;
            ext[XApiVocabulary.ExtDeviceModel] = _session.DeviceModel;
            if (_session.SdkVersion != null) ext[XApiVocabulary.ExtSdkVersion] = _session.SdkVersion;
            if (_session.AppVersion != null) ext[XApiVocabulary.ExtAppVersion] = _session.AppVersion;
            if (_session.AudienceTag != null) ext[XApiVocabulary.ExtAudienceTag] = _session.AudienceTag;
            if (_session.ContentVersion != null) ext[XApiVocabulary.ExtContentVersion] = _session.ContentVersion;
            return s;
        }

        private static XApiResult Outcome(double scaled, bool success, string duration) =>
            new XApiResult
            {
                Success = success,
                Completion = true,
                Score = new XApiScore { Scaled = scaled },
                Duration = duration
            };
    }
}
