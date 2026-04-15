using UnityEngine;

namespace BloodPressure
{
    /// <summary>
    /// Authoring data for one simulated patient in the Manual Blood Pressure
    /// training app. Instructors can create multiple <c>PatientProfile</c>
    /// assets (one per clinical case) via
    /// <c>Create → TrainAR → BloodPressure → Patient Profile</c> and assign
    /// one to the active training scene. Every BP simulation script reads
    /// ground-truth values from this asset so no code changes are needed
    /// to add or tune patient cases.
    ///
    /// Field units and clinical notes:
    /// <list type="bullet">
    ///   <item><c>systolicMmHg</c> / <c>diastolicMmHg</c> — the "true"
    ///     reading the learner is supposed to arrive at. Korotkoff phase I
    ///     starts at systolic, phase V ends at diastolic.</item>
    ///   <item><c>heartRateBpm</c> — beats per minute, used by
    ///     KorotkoffAudioEngine to schedule one heartbeat sound per
    ///     <c>60 / heartRate</c> seconds.</item>
    ///   <item><c>armCircumferenceCm</c> — used by the optional
    ///     cuff-size validator; the standard adult cuff is correct in the
    ///     22–32 cm range.</item>
    ///   <item><c>auscultatoryGap</c> — when true, Korotkoff sounds
    ///     disappear in a mid-systolic window, reproducing the clinical
    ///     pitfall where the learner can under-read systolic if they
    ///     start deflating too low.</item>
    /// </list>
    /// </summary>
    [CreateAssetMenu(
        fileName = "PatientProfile",
        menuName = "TrainAR/BloodPressure/Patient Profile",
        order = 200)]
    public class PatientProfile : ScriptableObject
    {
        [Header("Ground-truth reading (mmHg)")]
        [Tooltip("True systolic pressure in mmHg. Korotkoff phase I starts here on deflation.")]
        [Range(60, 260)]
        public int systolicMmHg = 120;

        [Tooltip("True diastolic pressure in mmHg. Korotkoff phase V ends here on deflation.")]
        [Range(30, 160)]
        public int diastolicMmHg = 80;

        [Header("Patient physiology")]
        [Tooltip("Heart rate in beats per minute; drives the Korotkoff heartbeat cadence.")]
        [Range(30, 200)]
        public int heartRateBpm = 72;

        [Tooltip("Upper-arm circumference in cm. Standard adult cuffs fit 22–32 cm.")]
        [Range(15f, 55f)]
        public float armCircumferenceCm = 28f;

        [Header("Clinical pitfalls")]
        [Tooltip("If enabled, Korotkoff sounds disappear in a mid-systolic window to simulate an auscultatory gap.")]
        public bool auscultatoryGap = false;

        [Tooltip("If auscultatoryGap is on, this is the upper bound of the silent window (mmHg).")]
        [Range(60, 260)]
        public int auscultatoryGapUpperMmHg = 160;

        [Tooltip("If auscultatoryGap is on, this is the lower bound of the silent window (mmHg).")]
        [Range(60, 260)]
        public int auscultatoryGapLowerMmHg = 140;

        [Header("Scoring")]
        [Tooltip("Learner readings within ±toleranceMmHg of the ground truth are counted as correct.")]
        [Range(0, 20)]
        public int toleranceMmHg = 4;

        /// <summary>
        /// Returns true when <paramref name="pressureMmHg"/> sits inside the
        /// window where a Korotkoff sound should be audible for this
        /// patient — i.e. between diastolic and systolic, and not inside an
        /// auscultatory gap (if one is configured).
        /// </summary>
        public bool IsKorotkoffAudibleAt(float pressureMmHg)
        {
            if (pressureMmHg < diastolicMmHg || pressureMmHg > systolicMmHg)
                return false;

            if (auscultatoryGap &&
                pressureMmHg >= auscultatoryGapLowerMmHg &&
                pressureMmHg <= auscultatoryGapUpperMmHg)
                return false;

            return true;
        }

        /// <summary>
        /// True when a learner's reading is within <see cref="toleranceMmHg"/>
        /// of both the true systolic and diastolic values.
        /// </summary>
        public bool IsReadingCorrect(int learnerSystolic, int learnerDiastolic)
        {
            return Mathf.Abs(learnerSystolic - systolicMmHg) <= toleranceMmHg
                && Mathf.Abs(learnerDiastolic - diastolicMmHg) <= toleranceMmHg;
        }

        private void OnValidate()
        {
            // Keep diastolic strictly below systolic so the Korotkoff window
            // is never empty or inverted when an author drags the sliders.
            if (diastolicMmHg >= systolicMmHg)
            {
                diastolicMmHg = Mathf.Max(30, systolicMmHg - 20);
            }

            if (auscultatoryGapLowerMmHg > auscultatoryGapUpperMmHg)
            {
                auscultatoryGapLowerMmHg = auscultatoryGapUpperMmHg;
            }
        }
    }
}
