using System;
using Encountive.Domain;

namespace Encountive.Telemetry
{
    /// <summary>
    /// Builds well-formed xAPI 1.0.3 statements for the event classes in
    /// SDD §5.2. Time and statement id are injected so a replayed
    /// session reproduces byte-identical telemetry (SDD §16.6).
    ///
    /// Mode and target ride on every statement's context extensions
    /// (SDD §2.3: "encoded in the session state ... emitted on every
    /// xAPI statement").
    /// </summary>
    public sealed class XApiStatementFactory
    {
        private readonly IClock _clock;
        private readonly Func<string> _idFactory;
        private readonly string _learnerId;
        private readonly TrainingMode _mode;
        private readonly string _target;

        public XApiStatementFactory(
            string learnerId, TrainingMode mode, string target,
            IClock clock = null, Func<string> idFactory = null)
        {
            _learnerId = learnerId;
            _mode = mode;
            _target = target;
            _clock = clock ?? new SystemClock();
            _idFactory = idFactory ?? (() => Guid.NewGuid().ToString());
        }

        private XApiStatement Base(string verbId, string verbDisplay, string activitySuffix)
        {
            var s = new XApiStatement
            {
                Id = _idFactory(),
                Timestamp = _clock.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                Actor = new XApiActor { Account = new XApiAccount { Name = _learnerId } },
                Verb = new XApiVerb { Id = verbId },
                Object = new XApiObject { Id = XApiVocabulary.ActivityBase + activitySuffix }
            };
            s.Verb.Display["en-US"] = verbDisplay;
            s.Context.Extensions[XApiVocabulary.ExtMode] = _mode.ToString().ToLowerInvariant();
            s.Context.Extensions[XApiVocabulary.ExtTarget] = _target;
            return s;
        }

        public XApiStatement SessionStarted(string sessionId) =>
            Base(XApiVocabulary.SessionStarted, "started session", "session/" + sessionId);

        public XApiStatement SessionEnded(string sessionId) =>
            Base(XApiVocabulary.SessionEnded, "ended session", "session/" + sessionId);

        public XApiStatement StationEntered(string stationId) =>
            Base(XApiVocabulary.StationEntered, "entered station", stationId);

        public XApiStatement StageEntered(string stationId, string stage) =>
            Base(XApiVocabulary.StageEntered, "entered stage", $"{stationId}/{stage}");

        public XApiStatement StageCompleted(string stationId, string stage) =>
            Base(XApiVocabulary.StageCompleted, "completed stage", $"{stationId}/{stage}");

        public XApiStatement DecisionCommitted(
            string stationId, string stage, string personaId, string cuffClass, double muacCm)
        {
            var s = Base(XApiVocabulary.DecisionCommitted, "committed decision",
                $"{stationId}/{stage}");
            s.Context.Extensions[XApiVocabulary.ExtPersona] = personaId;
            s.Context.Extensions[XApiVocabulary.ExtCuffClassSelected] = cuffClass;
            s.Context.Extensions[XApiVocabulary.ExtMuacCm] = muacCm;
            s.Result = new XApiResult { Completion = true, Success = true };
            return s;
        }

        public XApiStatement SafetyGateFired(string stationId, string gateId) =>
            Tag(Base(XApiVocabulary.SafetyGateFired, "fired safety gate", stationId),
                XApiVocabulary.ExtGateId, gateId);

        public XApiStatement SafetyGateResolved(string stationId, string gateId) =>
            Tag(Base(XApiVocabulary.SafetyGateResolved, "resolved safety gate", stationId),
                XApiVocabulary.ExtGateId, gateId);

        public XApiStatement CoachUtterancePlayed(
            string stationId, string triggerId, string promptVersion, string auditEntryId)
        {
            var s = Base(XApiVocabulary.CoachUtterancePlayed, "played coach utterance", stationId);
            s.Context.Extensions[XApiVocabulary.ExtTriggerId] = triggerId;
            s.Context.Extensions[XApiVocabulary.ExtPromptVersion] = promptVersion;
            if (auditEntryId != null)
                s.Context.Extensions[XApiVocabulary.ExtAuditEntryId] = auditEntryId;
            return s;
        }

        public XApiStatement PerStationMasteryAchieved(string stationId) =>
            Base(XApiVocabulary.PerStationMasteryAchieved, "achieved per-station mastery", stationId);

        private static XApiStatement Tag(XApiStatement s, string ext, object val)
        {
            s.Context.Extensions[ext] = val;
            return s;
        }
    }
}
