using UnityEngine;
using UnityEngine.Events;

namespace BloodPressure
{
    /// <summary>
    /// Result payload delivered to <see cref="BPMeasurementAssessment.OnResult"/>
    /// and the debrief UI. Kept as a small serializable container so
    /// visual-scripting nodes and world-space UI can bind to individual
    /// fields without the script having to expose seven parallel events.
    /// </summary>
    [System.Serializable]
    public struct BPAssessmentResult
    {
        public int learnerSystolic;
        public int learnerDiastolic;
        public int trueSystolic;
        public int trueDiastolic;
        public int toleranceMmHg;
        public bool correct;
    }

    /// <summary>
    /// Collects the learner's systolic and diastolic readings during an
    /// auscultation run and scores them against the active
    /// <see cref="PatientProfile"/>. Supports two input modes that can
    /// be mixed freely in the same run:
    ///
    /// 1. <see cref="MarkSystolic"/> / <see cref="MarkDiastolic"/> — the
    ///    learner presses a world-space "Mark" button the instant they
    ///    hear the Korotkoff transition, and the assessment samples the
    ///    authoritative cuff pressure from <see cref="CuffPressureSimulator"/>
    ///    at that moment. This is the primary pedagogical path: the
    ///    learner has to listen, not type.
    ///
    /// 2. <see cref="SetSystolic"/> / <see cref="SetDiastolic"/> — the
    ///    learner enters a value on a world-space numeric keypad.
    ///    Useful for the "enter final reading" screen at the end of the
    ///    case, and as a fallback for learners who want to correct a
    ///    mistimed Mark press without redoing the whole deflation.
    ///
    /// <see cref="Submit"/> locks in the current learnerSystolic /
    /// learnerDiastolic, scores them via
    /// <see cref="PatientProfile.IsReadingCorrect"/>, and broadcasts
    /// the result so the existing TrainAR Feedback / Instructions
    /// visual-scripting nodes can drive the debrief screen and
    /// state-machine advancement.
    /// </summary>
    public class BPMeasurementAssessment : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField]
        [Tooltip("Active patient case — ground truth for scoring.")]
        private PatientProfile patientProfile;

        [SerializeField]
        [Tooltip("Cuff simulator sampled on MarkSystolic / MarkDiastolic.")]
        private CuffPressureSimulator cuff;

        [Header("Events")]
        [Tooltip("Fires when the learner marks a systolic reading. Argument is the rounded mmHg value captured at the moment of the mark.")]
        public UnityEvent<int> OnSystolicMarked = new UnityEvent<int>();

        [Tooltip("Fires when the learner marks a diastolic reading.")]
        public UnityEvent<int> OnDiastolicMarked = new UnityEvent<int>();

        [Tooltip("Fires on Submit(). Payload contains learner reading, ground truth, tolerance, and correct bool so the debrief UI can bind to individual fields.")]
        public UnityEvent<BPAssessmentResult> OnResult = new UnityEvent<BPAssessmentResult>();

        [Tooltip("Fires on Submit() if the reading is within tolerance. Lets visual scripting advance the state machine on a correct answer without inspecting the payload.")]
        public UnityEvent OnCorrect = new UnityEvent();

        [Tooltip("Fires on Submit() if the reading is outside tolerance.")]
        public UnityEvent OnIncorrect = new UnityEvent();

        /// <summary>
        /// Most recently recorded systolic reading (from a Mark press or
        /// a keypad entry). -1 until the learner provides one.
        /// </summary>
        public int LearnerSystolic { get; private set; } = -1;

        /// <summary>
        /// Most recently recorded diastolic reading. -1 until the learner
        /// provides one.
        /// </summary>
        public int LearnerDiastolic { get; private set; } = -1;

        /// <summary>
        /// Samples the current cuff pressure as the learner's systolic
        /// reading. Called by the world-space "Mark systolic" button
        /// the instant the learner hears Korotkoff phase I appear.
        /// </summary>
        public void MarkSystolic()
        {
            if (cuff == null)
            {
                Debug.LogWarning("[BPMeasurementAssessment] No cuff reference; MarkSystolic ignored.", this);
                return;
            }

            int value = Mathf.RoundToInt(cuff.CurrentPressureMmHg);
            LearnerSystolic = value;
            OnSystolicMarked?.Invoke(value);
        }

        /// <summary>
        /// Samples the current cuff pressure as the learner's diastolic
        /// reading. Called by the world-space "Mark diastolic" button
        /// the instant the learner hears Korotkoff phase V disappear.
        /// </summary>
        public void MarkDiastolic()
        {
            if (cuff == null)
            {
                Debug.LogWarning("[BPMeasurementAssessment] No cuff reference; MarkDiastolic ignored.", this);
                return;
            }

            int value = Mathf.RoundToInt(cuff.CurrentPressureMmHg);
            LearnerDiastolic = value;
            OnDiastolicMarked?.Invoke(value);
        }

        /// <summary>
        /// Overwrites the systolic reading with an explicit value from
        /// the world-space numeric keypad at the end of the case.
        /// </summary>
        public void SetSystolic(int systolicMmHg)
        {
            LearnerSystolic = systolicMmHg;
            OnSystolicMarked?.Invoke(systolicMmHg);
        }

        /// <summary>
        /// Overwrites the diastolic reading with an explicit value from
        /// the world-space numeric keypad at the end of the case.
        /// </summary>
        public void SetDiastolic(int diastolicMmHg)
        {
            LearnerDiastolic = diastolicMmHg;
            OnDiastolicMarked?.Invoke(diastolicMmHg);
        }

        /// <summary>
        /// Locks in the current reading, scores it against the active
        /// patient profile, and broadcasts the result. No-op if the
        /// learner has not yet provided both values, or if no patient
        /// profile is assigned.
        /// </summary>
        public void Submit()
        {
            if (patientProfile == null)
            {
                Debug.LogWarning("[BPMeasurementAssessment] No patient profile; Submit ignored.", this);
                return;
            }

            if (LearnerSystolic < 0 || LearnerDiastolic < 0)
            {
                Debug.LogWarning("[BPMeasurementAssessment] Submit called before both readings were provided.", this);
                return;
            }

            bool correct = patientProfile.IsReadingCorrect(LearnerSystolic, LearnerDiastolic);

            var result = new BPAssessmentResult
            {
                learnerSystolic = LearnerSystolic,
                learnerDiastolic = LearnerDiastolic,
                trueSystolic = patientProfile.systolicMmHg,
                trueDiastolic = patientProfile.diastolicMmHg,
                toleranceMmHg = patientProfile.toleranceMmHg,
                correct = correct,
            };

            OnResult?.Invoke(result);
            if (correct) OnCorrect?.Invoke();
            else OnIncorrect?.Invoke();
        }

        /// <summary>
        /// Clears the learner's recorded readings so the same assessment
        /// component can be reused for a second case in the same session.
        /// </summary>
        public void Reset()
        {
            LearnerSystolic = -1;
            LearnerDiastolic = -1;
        }

        /// <summary>
        /// Runtime patient case swap. Note this does NOT reset the
        /// learner's readings; call <see cref="Reset"/> separately if
        /// you want a fresh attempt.
        /// </summary>
        public void SetPatientProfile(PatientProfile profile)
        {
            patientProfile = profile;
        }
    }
}
