using UnityEngine;
using UnityEngine.Events;

namespace BloodPressure
{
    /// <summary>
    /// The ten training stages of an auscultatory BP measurement, in
    /// the same order authored in the plan. The visual-scripting
    /// stateflow drops one Instructions/Feedback node per stage and
    /// binds it to <see cref="BPTrainingController.OnStageChanged"/>.
    /// </summary>
    public enum BPStage
    {
        Onboarding = 0,
        GreetPatient = 1,
        ExposeUpperArm = 2,
        WrapCuff = 3,
        PalpatePulse = 4,
        PlaceStethoscope = 5,
        InflateCuff = 6,
        DeflateCuff = 7,
        MarkReadings = 8,
        EnterAndAssess = 9,
        Debrief = 10,
    }

    /// <summary>
    /// Single orchestrator that wires the six BP simulation components
    /// together into one stateful controller the visual-scripting
    /// stateflow can talk to.
    ///
    /// Why this exists: without an orchestrator, the scene's stateflow
    /// graph has to wire ~20 UnityEvents across six different components
    /// for every new training scene, which is both error-prone and
    /// duplicated work if the same clinic hosts multiple BP training
    /// variants. With this controller the graph only has to:
    ///
    ///   1. Call <see cref="StartCase"/> with a PatientProfile.
    ///   2. Bind a single <see cref="OnStageChanged"/> handler that
    ///      swaps the on-screen instruction text.
    ///   3. Drive stage transitions with <see cref="Advance"/> or the
    ///      auto-advance conditions this controller watches for
    ///      internally (cuff hits target pressure, stethoscope is
    ///      placed, assessment submits a correct reading).
    ///
    /// This controller DOES NOT own any simulation state — every field
    /// still lives on the six single-responsibility components from
    /// step 4. It only owns stage transitions, auto-advance policy,
    /// and the wiring glue.
    /// </summary>
    public class BPTrainingController : MonoBehaviour
    {
        [Header("Simulation Components")]
        [SerializeField]
        [Tooltip("Authoritative cuff pressure. Auto-advances from InflateCuff when its pressure crosses the target threshold.")]
        private CuffPressureSimulator cuff;

        [SerializeField]
        [Tooltip("Deflation valve. Its InCorrectWindow state is re-broadcast as OnDeflationWindowChanged for the HUD.")]
        private ValveController valve;

        [SerializeField]
        [Tooltip("Korotkoff audio engine. Its patient profile is swapped on StartCase.")]
        private KorotkoffAudioEngine korotkoffEngine;

        [SerializeField]
        [Tooltip("Brachial-artery anchor validator. OnPlaced auto-advances from PlaceStethoscope.")]
        private StethoscopePlacementValidator stethoscopeValidator;

        [SerializeField]
        [Tooltip("Final scoring component. OnCorrect auto-advances from EnterAndAssess to Debrief.")]
        private BPMeasurementAssessment assessment;

        [Header("Case Defaults")]
        [SerializeField]
        [Tooltip("Patient profile used by StartCase() when no argument is provided.")]
        private PatientProfile defaultPatientProfile;

        [Header("Auto-advance Tuning")]
        [SerializeField]
        [Tooltip("Target inflation headroom above systolic. Standard teaching is +30 mmHg so phase I is never missed.")]
        [Range(10, 60)]
        private int inflationHeadroomMmHg = 30;

        [Header("Events")]
        [Tooltip("Fires every time the current stage changes. Payload is the NEW stage.")]
        public UnityEvent<BPStage> OnStageChanged = new UnityEvent<BPStage>();

        [Tooltip("Re-broadcast of ValveController.InCorrectWindowChanged so the HUD can subscribe to one controller instead of six components.")]
        public UnityEvent<bool> OnDeflationWindowChanged = new UnityEvent<bool>();

        [Tooltip("Fires with the BPAssessmentResult when the learner submits a reading.")]
        public UnityEvent<BPAssessmentResult> OnAssessmentResult = new UnityEvent<BPAssessmentResult>();

        /// <summary>
        /// Currently-active stage. Writes go through <see cref="GoTo"/>
        /// so every transition broadcasts <see cref="OnStageChanged"/>.
        /// </summary>
        public BPStage CurrentStage { get; private set; } = BPStage.Onboarding;

        /// <summary>
        /// The profile in play for the current case. Null until
        /// <see cref="StartCase"/> has been called at least once.
        /// </summary>
        public PatientProfile ActiveProfile { get; private set; }

        private void OnEnable()
        {
            if (cuff != null) cuff.PressureChanged.AddListener(HandlePressureChanged);
            if (valve != null) valve.InCorrectWindowChanged.AddListener(HandleDeflationWindowChanged);
            if (stethoscopeValidator != null) stethoscopeValidator.OnPlaced.AddListener(HandleStethoscopePlaced);
            if (assessment != null)
            {
                assessment.OnResult.AddListener(HandleAssessmentResult);
                assessment.OnCorrect.AddListener(HandleAssessmentCorrect);
            }
        }

        private void OnDisable()
        {
            if (cuff != null) cuff.PressureChanged.RemoveListener(HandlePressureChanged);
            if (valve != null) valve.InCorrectWindowChanged.RemoveListener(HandleDeflationWindowChanged);
            if (stethoscopeValidator != null) stethoscopeValidator.OnPlaced.RemoveListener(HandleStethoscopePlaced);
            if (assessment != null)
            {
                assessment.OnResult.RemoveListener(HandleAssessmentResult);
                assessment.OnCorrect.RemoveListener(HandleAssessmentCorrect);
            }
        }

        /// <summary>
        /// Begin a new case. Resets the cuff to 0 mmHg, clears the
        /// assessment's recorded readings, pushes the profile into the
        /// Korotkoff engine + assessment so every subsystem is scoring
        /// against the same ground truth, and jumps to
        /// <see cref="BPStage.Onboarding"/>.
        ///
        /// Pass <c>null</c> to use <see cref="defaultPatientProfile"/>.
        /// </summary>
        public void StartCase(PatientProfile profile = null)
        {
            ActiveProfile = profile != null ? profile : defaultPatientProfile;

            if (ActiveProfile == null)
            {
                Debug.LogError("[BPTrainingController] StartCase called with no profile and no defaultPatientProfile assigned.", this);
                return;
            }

            if (cuff != null) cuff.SetPressure(0f);
            if (korotkoffEngine != null) korotkoffEngine.SetPatientProfile(ActiveProfile);
            if (assessment != null)
            {
                assessment.SetPatientProfile(ActiveProfile);
                assessment.Reset();
            }

            GoTo(BPStage.Onboarding);
        }

        /// <summary>
        /// Move to the next stage in sequence. No-op if already at
        /// <see cref="BPStage.Debrief"/>.
        /// </summary>
        public void Advance()
        {
            if (CurrentStage == BPStage.Debrief) return;
            GoTo(CurrentStage + 1);
        }

        /// <summary>
        /// Jump to a specific stage, e.g. from a debug menu or when
        /// authoring the stateflow and short-circuiting early stages.
        /// </summary>
        public void GoTo(BPStage stage)
        {
            if (stage == CurrentStage) return;
            CurrentStage = stage;
            OnStageChanged?.Invoke(stage);
        }

        // ------------------------------------------------------------
        // Auto-advance handlers. Each one only fires when the current
        // stage is the one the learner is expected to be on, so a
        // learner idly fiddling with the cuff during the "greet
        // patient" stage does not accidentally skip the entire
        // placement sequence.
        // ------------------------------------------------------------

        private void HandlePressureChanged(float pressureMmHg)
        {
            if (CurrentStage != BPStage.InflateCuff) return;
            if (ActiveProfile == null) return;

            float target = ActiveProfile.systolicMmHg + inflationHeadroomMmHg;
            if (pressureMmHg >= target)
            {
                GoTo(BPStage.DeflateCuff);
            }
        }

        private void HandleDeflationWindowChanged(bool inWindow)
        {
            // Pure re-broadcast so the HUD has one wiring target.
            OnDeflationWindowChanged?.Invoke(inWindow);
        }

        private void HandleStethoscopePlaced()
        {
            if (CurrentStage != BPStage.PlaceStethoscope) return;
            GoTo(BPStage.InflateCuff);
        }

        private void HandleAssessmentResult(BPAssessmentResult result)
        {
            OnAssessmentResult?.Invoke(result);
        }

        private void HandleAssessmentCorrect()
        {
            if (CurrentStage != BPStage.EnterAndAssess) return;
            GoTo(BPStage.Debrief);
        }
    }
}
