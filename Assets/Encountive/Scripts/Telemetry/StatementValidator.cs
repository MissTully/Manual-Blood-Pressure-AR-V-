using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Encountive.Telemetry
{
    public sealed class ValidationResult
    {
        public bool Ok { get; }
        public IReadOnlyList<string> Errors { get; }
        public ValidationResult(bool ok, IReadOnlyList<string> errors)
        {
            Ok = ok; Errors = errors;
        }
    }

    /// <summary>
    /// AR BP Cuff Trainer xAPI profile §10 pre-enqueue validator.
    /// Statements that fail validation MUST be logged locally and MUST
    /// NOT be transmitted (§10). Enforces structural, semantic and
    /// privacy rules; pure C# so it runs identically on-device and in
    /// CI.
    /// </summary>
    public sealed class StatementValidator
    {
        private static readonly Regex UuidV4 = new Regex(
            "^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-4[0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}$",
            RegexOptions.Compiled);

        private static readonly Regex IsoUtcMs = new Regex(
            @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z$",
            RegexOptions.Compiled);

        private static readonly Regex EmailLike = new Regex(
            @"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}",
            RegexOptions.Compiled);

        private static readonly HashSet<string> AllowedVerbs = new HashSet<string>
        {
            XApiVocabulary.Initialized, XApiVocabulary.Attempted,
            XApiVocabulary.Completed, XApiVocabulary.Passed,
            XApiVocabulary.Failed, XApiVocabulary.Abandoned,
            XApiVocabulary.Terminated,
            XApiVocabulary.Measured, XApiVocabulary.SelectedCuff,
            XApiVocabulary.PlacedCuff, XApiVocabulary.PredictedError,
            XApiVocabulary.RequestedHint, XApiVocabulary.ReceivedCoaching,
            XApiVocabulary.RepositionedCuff, XApiVocabulary.FixatedRegion
        };

        private static readonly HashSet<string> AllowedActivityTypes = new HashSet<string>
        {
            XApiVocabulary.ActivityTypeSession,
            XApiVocabulary.ActivityTypeLesson,
            XApiVocabulary.ActivityTypeTrial,
            XApiVocabulary.ActivityTypeMeasurement,
            XApiVocabulary.ActivityTypeCuffSelection,
            XApiVocabulary.ActivityTypeCuffPlacement,
            XApiVocabulary.ActivityTypeErrorPrediction,
            XApiVocabulary.ActivityTypeHint,
            XApiVocabulary.ActivityTypeCoachingPrompt,
            XApiVocabulary.ActivityTypeRegionOfInterest
        };

        // Verb → allowed activity types (spec §5.1).
        private static readonly Dictionary<string, HashSet<string>> VerbObjectMap = Build();

        // Outcome verbs MUST include result.score.scaled (spec §10.2).
        private static readonly HashSet<string> OutcomeVerbs = new HashSet<string>
        {
            XApiVocabulary.Completed, XApiVocabulary.Passed, XApiVocabulary.Failed,
            XApiVocabulary.Measured, XApiVocabulary.SelectedCuff,
            XApiVocabulary.PlacedCuff, XApiVocabulary.PredictedError
        };

        // Verbs whose statements MUST carry grouping=session (trial-level
        // statements; spec §10.2). Session-lifecycle verbs operate on
        // the session activity itself and are exempt.
        private static readonly HashSet<string> SessionLifecycleVerbs = new HashSet<string>
        {
            XApiVocabulary.Initialized, XApiVocabulary.Terminated
        };

        public ValidationResult Validate(XApiStatement s)
        {
            var errors = new List<string>();
            if (s == null) { errors.Add("statement is null"); return new ValidationResult(false, errors); }

            // §10.1 structural
            if (s.Id == null || !UuidV4.IsMatch(s.Id))
                errors.Add("id is not a UUID v4");
            if (s.Timestamp == null || !IsoUtcMs.IsMatch(s.Timestamp))
                errors.Add("timestamp is not ISO-8601 UTC with millisecond precision");

            if (s.Verb == null || s.Verb.Id == null || !AllowedVerbs.Contains(s.Verb.Id))
                errors.Add("verb id is not in the profile vocabulary");

            string activityType = s.Object?.Definition?.Type;
            if (activityType == null || !AllowedActivityTypes.Contains(activityType))
                errors.Add("activity type IRI is not in the profile vocabulary");

            if (s.Verb?.Id != null && activityType != null &&
                VerbObjectMap.TryGetValue(s.Verb.Id, out var allowed) &&
                !allowed.Contains(activityType))
                errors.Add($"verb {s.Verb.Id} is not allowed with activity type {activityType}");

            // §10.2 semantic
            if (s.Verb != null && OutcomeVerbs.Contains(s.Verb.Id))
            {
                if (s.Result?.Score?.Scaled == null)
                    errors.Add("outcome verb requires result.score.scaled");
                else if (s.Result.Score.Scaled < -1.0 || s.Result.Score.Scaled > 1.0)
                    errors.Add("result.score.scaled must be in [-1, 1]");
            }

            var cats = s.Context?.ContextActivities?.Category;
            if (cats == null || !ContainsActivity(cats, XApiVocabulary.ProfileIri))
                errors.Add("context.contextActivities.category must contain the profile IRI");

            if (s.Verb != null && !SessionLifecycleVerbs.Contains(s.Verb.Id))
            {
                var grp = s.Context?.ContextActivities?.Grouping;
                if (grp == null || grp.Count == 0)
                    errors.Add("trial-level statements must include contextActivities.grouping (session)");
            }

            if (s.Context == null || string.IsNullOrEmpty(s.Context.Registration) ||
                !UuidV4.IsMatch(s.Context.Registration))
                errors.Add("context.registration must be a UUID v4");

            foreach (string err in FiniteCheck(s)) errors.Add(err);

            // §10.3 privacy
            if (s.Actor != null)
            {
                if (HasProperty(s.Actor, "mbox") || HasProperty(s.Actor, "openid"))
                    errors.Add("actor.mbox / actor.openid are forbidden");
                if (s.Actor.Account?.Name != null && EmailLike.IsMatch(s.Actor.Account.Name))
                    errors.Add("actor.account.name resembles an email address");
            }

            foreach (string err in PrivacyExtensionCheck(s)) errors.Add(err);

            return new ValidationResult(errors.Count == 0, errors);
        }

        private static bool ContainsActivity(IList<XApiActivityRef> list, string id)
        {
            foreach (var r in list) if (r?.Id == id) return true;
            return false;
        }

        // Forbidden extension keys (§10.3) — raw biometric streams or
        // audio MUST NOT appear in telemetry.
        private static readonly string[] ForbiddenKeys =
        {
            "raw-gaze-stream", "hand-skeleton-stream",
            "audio-waveform", "voice-transcript", "audio-transcript"
        };

        private static IEnumerable<string> PrivacyExtensionCheck(XApiStatement s)
        {
            foreach (var bag in EnumerateExtensions(s))
            {
                if (bag == null) continue;
                foreach (string k in bag.Keys)
                {
                    foreach (string bad in ForbiddenKeys)
                    {
                        if (k.EndsWith("/" + bad, StringComparison.Ordinal) || k == bad)
                            yield return $"forbidden raw biometric/audio extension '{k}'";
                    }
                }
            }
        }

        private static IEnumerable<string> FiniteCheck(XApiStatement s)
        {
            foreach (var bag in EnumerateExtensions(s))
            {
                if (bag == null) continue;
                foreach (var kv in bag)
                {
                    if (kv.Value is double d && (double.IsNaN(d) || double.IsInfinity(d)))
                        yield return $"extension '{kv.Key}' has non-finite numeric value";
                    if (kv.Value is float f && (float.IsNaN(f) || float.IsInfinity(f)))
                        yield return $"extension '{kv.Key}' has non-finite numeric value";
                }
            }
        }

        private static IEnumerable<IDictionary<string, object>> EnumerateExtensions(XApiStatement s)
        {
            yield return s?.Context?.Extensions;
            yield return s?.Object?.Definition?.Extensions;
            yield return s?.Result?.Extensions;
        }

        private static bool HasProperty(XApiActor _, string _2) => false; // model has no mbox/openid

        private static Dictionary<string, HashSet<string>> Build()
        {
            var m = new Dictionary<string, HashSet<string>>();
            void Add(string verb, params string[] types) =>
                m[verb] = new HashSet<string>(types);

            Add(XApiVocabulary.Initialized, XApiVocabulary.ActivityTypeSession);
            Add(XApiVocabulary.Terminated, XApiVocabulary.ActivityTypeSession);
            Add(XApiVocabulary.Abandoned,
                XApiVocabulary.ActivityTypeSession,
                XApiVocabulary.ActivityTypeLesson,
                XApiVocabulary.ActivityTypeTrial);
            Add(XApiVocabulary.Attempted,
                XApiVocabulary.ActivityTypeLesson, XApiVocabulary.ActivityTypeTrial);
            Add(XApiVocabulary.Completed,
                XApiVocabulary.ActivityTypeLesson, XApiVocabulary.ActivityTypeTrial);
            Add(XApiVocabulary.Passed,
                XApiVocabulary.ActivityTypeLesson, XApiVocabulary.ActivityTypeTrial);
            Add(XApiVocabulary.Failed,
                XApiVocabulary.ActivityTypeLesson, XApiVocabulary.ActivityTypeTrial);
            Add(XApiVocabulary.Measured, XApiVocabulary.ActivityTypeMeasurement);
            Add(XApiVocabulary.SelectedCuff, XApiVocabulary.ActivityTypeCuffSelection);
            Add(XApiVocabulary.RepositionedCuff,
                XApiVocabulary.ActivityTypeCuffSelection, XApiVocabulary.ActivityTypeCuffPlacement);
            Add(XApiVocabulary.PlacedCuff, XApiVocabulary.ActivityTypeCuffPlacement);
            Add(XApiVocabulary.PredictedError, XApiVocabulary.ActivityTypeErrorPrediction);
            Add(XApiVocabulary.RequestedHint, XApiVocabulary.ActivityTypeHint);
            Add(XApiVocabulary.ReceivedCoaching, XApiVocabulary.ActivityTypeCoachingPrompt);
            Add(XApiVocabulary.FixatedRegion, XApiVocabulary.ActivityTypeRegionOfInterest);
            return m;
        }
    }
}
