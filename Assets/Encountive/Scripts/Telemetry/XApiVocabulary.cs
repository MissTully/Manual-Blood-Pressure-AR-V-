namespace Encountive.Telemetry
{
    /// <summary>
    /// Controlled vocabulary for the AR BP Cuff Trainer xAPI profile v1
    /// (companion spec §4, §5, §8). Implementations MUST NOT emit verbs
    /// outside this list under this profile.
    /// </summary>
    public static class XApiVocabulary
    {
        public const string ProfileIri =
            "https://encountive.com/xapi/profiles/ar-bp-cuff/v1";

        public const string VerbBase = "https://encountive.com/xapi/verbs/";
        public const string ExtBase = "https://encountive.com/xapi/extensions/";
        public const string ActivityBase = "https://encountive.com/xapi/activities/";
        public const string ActivityTypeBase = "https://encountive.com/xapi/activity-types/";
        public const string LearnersHomePage = "https://encountive.com/learners";
        public const string Platform = "AR-BP-Cuff-Trainer";

        // --- Standard ADL verbs (spec §4.1) ---
        public const string Initialized = "http://adlnet.gov/expapi/verbs/initialized";
        public const string Attempted = "http://adlnet.gov/expapi/verbs/attempted";
        public const string Completed = "http://adlnet.gov/expapi/verbs/completed";
        public const string Passed = "http://adlnet.gov/expapi/verbs/passed";
        public const string Failed = "http://adlnet.gov/expapi/verbs/failed";
        public const string Abandoned = "http://adlnet.gov/expapi/verbs/abandoned";
        public const string Terminated = "http://adlnet.gov/expapi/verbs/terminated";

        // --- Profile verbs (spec §4.2) ---
        public const string Measured = VerbBase + "measured";
        public const string SelectedCuff = VerbBase + "selected-cuff";
        public const string PlacedCuff = VerbBase + "placed-cuff";
        public const string PredictedError = VerbBase + "predicted-error";
        public const string RequestedHint = VerbBase + "requested-hint";
        public const string ReceivedCoaching = VerbBase + "received-coaching";
        public const string RepositionedCuff = VerbBase + "repositioned-cuff";
        public const string FixatedRegion = VerbBase + "fixated-region";

        // --- Activity types (spec §5.1) ---
        public const string ActivityTypeSession = ActivityTypeBase + "session";
        public const string ActivityTypeLesson = ActivityTypeBase + "lesson";
        public const string ActivityTypeTrial = ActivityTypeBase + "trial";
        public const string ActivityTypeMeasurement = ActivityTypeBase + "measurement";
        public const string ActivityTypeCuffSelection = ActivityTypeBase + "cuff-selection";
        public const string ActivityTypeCuffPlacement = ActivityTypeBase + "cuff-placement";
        public const string ActivityTypeErrorPrediction = ActivityTypeBase + "error-prediction";
        public const string ActivityTypeHint = ActivityTypeBase + "hint";
        public const string ActivityTypeCoachingPrompt = ActivityTypeBase + "coaching-prompt";
        public const string ActivityTypeRegionOfInterest = ActivityTypeBase + "region-of-interest";

        // --- Context extensions (spec §8.6) ---
        public const string ExtProfileVersion = ExtBase + "profile-version";
        public const string ExtDeviceModel = ExtBase + "device-model";
        public const string ExtSdkVersion = ExtBase + "sdk-version";
        public const string ExtAppVersion = ExtBase + "app-version";
        public const string ExtAudienceTag = ExtBase + "audience-tag";
        public const string ExtFixationSummary = ExtBase + "fixation-summary";

        // --- Trial object extensions (spec §8.1) ---
        public const string ExtTrialType = ExtBase + "trial-type";
        public const string ExtArchetypeId = ExtBase + "archetype-id";
        public const string ExtArchetypeArmCircumferenceCm = ExtBase + "archetype-arm-circumference-cm";
        public const string ExtArchetypeTaperCoefficient = ExtBase + "archetype-taper-coefficient";
        public const string ExtContentVersion = ExtBase + "content-version";
        public const string ExtHintsAllowed = ExtBase + "hints-allowed";
        public const string ExtSnapEnabled = ExtBase + "snap-enabled";

        // --- measured result extensions (spec §8.2) ---
        public const string ExtReportedCircumferenceCm = ExtBase + "reported-circumference-cm";
        public const string ExtMeasurementErrorCm = ExtBase + "measurement-error-cm";
        public const string ExtTapeAnchorPosition = ExtBase + "tape-anchor-position";
        public const string ExtMeasurementAttempts = ExtBase + "measurement-attempts";

        // --- selected-cuff result extensions (spec §8.3) ---
        public const string ExtCuffId = ExtBase + "cuff-id";
        public const string ExtCuffSizeClass = ExtBase + "cuff-size-class";
        public const string ExtCuffBladderWidthCm = ExtBase + "cuff-bladder-width-cm";
        public const string ExtCuffBladderLengthCm = ExtBase + "cuff-bladder-length-cm";
        public const string ExtSelectionCorrect = ExtBase + "selection-correct";
        public const string ExtAlternativeCuffsConsidered = ExtBase + "alternative-cuffs-considered";

        // --- placed-cuff result extensions (spec §8.4) ---
        public const string ExtBladderOverArteryAngularErrorDeg = ExtBase + "bladder-over-artery-angular-error-deg";
        public const string ExtLongitudinalErrorCm = ExtBase + "longitudinal-error-cm";
        public const string ExtCoverageFraction = ExtBase + "coverage-fraction";
        public const string ExtOrientationCorrect = ExtBase + "orientation-correct";
        public const string ExtRepositionings = ExtBase + "repositionings";

        // --- predicted-error result extensions (spec §8.5) ---
        public const string ExtPredictedSystolicErrorMmHg = ExtBase + "predicted-systolic-error-mmhg";
        public const string ExtPredictedDiastolicErrorMmHg = ExtBase + "predicted-diastolic-error-mmhg";
        public const string ExtSimulatedSystolicErrorMmHg = ExtBase + "simulated-systolic-error-mmhg";
        public const string ExtSimulatedDiastolicErrorMmHg = ExtBase + "simulated-diastolic-error-mmhg";
        public const string ExtPredictionAccuracyMmHg = ExtBase + "prediction-accuracy-mmhg";
    }
}
