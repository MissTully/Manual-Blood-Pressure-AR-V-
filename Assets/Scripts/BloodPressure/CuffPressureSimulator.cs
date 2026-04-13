using UnityEngine;
using UnityEngine.Events;

namespace BloodPressure
{
    /// <summary>
    /// Core simulation state for the sphygmomanometer cuff. Owns a single
    /// <c>currentPressureMmHg</c> value clamped to [0, 300], exposes an
    /// <see cref="Inflate"/> / <see cref="Deflate"/> API that
    /// <see cref="BulbSqueezeInteractor"/> and <see cref="ValveController"/>
    /// call into, and broadcasts a <see cref="PressureChanged"/> event that
    /// every other BP system subscribes to.
    ///
    /// Why a central owner rather than computing pressure per-system:
    /// Korotkoff audio, the gauge needle, the assessment script, the HUD
    /// "deflation rate" indicator, and the visual-scripting stateflow all
    /// need to agree on one authoritative pressure value. Centralising it
    /// here means each consumer reacts to the same event stream and
    /// there is exactly one place to debug if the number drifts.
    /// </summary>
    public class CuffPressureSimulator : MonoBehaviour
    {
        /// <summary>Minimum pressure the simulator will hold (mmHg).</summary>
        public const float MinPressureMmHg = 0f;

        /// <summary>
        /// Maximum safe pressure. Real aneroid gauges typically read up to
        /// 300 mmHg; clamping here prevents a runaway squeeze from driving
        /// the gauge needle past the end of its dial.
        /// </summary>
        public const float MaxPressureMmHg = 300f;

        [Header("State")]
        [SerializeField]
        [Tooltip("Current cuff pressure in mmHg. Authoritative source of truth for every other BP system.")]
        private float currentPressureMmHg = 0f;

        [Header("Gauge needle")]
        [SerializeField]
        [Tooltip("Transform rotated to visualise the current pressure (the aneroid gauge needle).")]
        private Transform gaugeNeedle;

        [SerializeField]
        [Tooltip("Local-space axis the needle rotates around. Default is Z (screen-facing dials).")]
        private Vector3 needleRotationAxis = Vector3.forward;

        [SerializeField]
        [Tooltip("Needle angle in degrees that corresponds to 0 mmHg.")]
        private float needleAngleAtZero = 0f;

        [SerializeField]
        [Tooltip("Needle angle in degrees that corresponds to MaxPressureMmHg.")]
        private float needleAngleAtMax = -270f;

        [Header("Events")]
        [Tooltip("Fires whenever the pressure value changes. Argument is the new pressure in mmHg.")]
        public UnityEvent<float> PressureChanged = new UnityEvent<float>();

        /// <summary>
        /// Authoritative cuff pressure in mmHg. Always clamped to
        /// [<see cref="MinPressureMmHg"/>, <see cref="MaxPressureMmHg"/>].
        /// </summary>
        public float CurrentPressureMmHg => currentPressureMmHg;

        private void Start()
        {
            // Make sure the gauge needle, HUD, and Korotkoff engine start
            // in sync with whatever initial value was authored on the asset.
            ApplyNeedleRotation();
            PressureChanged?.Invoke(currentPressureMmHg);
        }

        /// <summary>
        /// Raises cuff pressure by <paramref name="deltaMmHg"/>. Negative
        /// deltas are ignored (use <see cref="Deflate"/> to drop pressure)
        /// so a mis-wired caller cannot silently flip the sign.
        /// </summary>
        public void Inflate(float deltaMmHg)
        {
            if (deltaMmHg <= 0f) return;
            SetPressure(currentPressureMmHg + deltaMmHg);
        }

        /// <summary>
        /// Drops cuff pressure by <paramref name="deltaMmHg"/>. Negative
        /// deltas are ignored.
        /// </summary>
        public void Deflate(float deltaMmHg)
        {
            if (deltaMmHg <= 0f) return;
            SetPressure(currentPressureMmHg - deltaMmHg);
        }

        /// <summary>
        /// Sets the pressure to an absolute value. Primarily used by the
        /// visual-scripting "reset cuff" node at the start of a new case.
        /// </summary>
        public void SetPressure(float pressureMmHg)
        {
            float clamped = Mathf.Clamp(pressureMmHg, MinPressureMmHg, MaxPressureMmHg);
            if (Mathf.Approximately(clamped, currentPressureMmHg)) return;

            currentPressureMmHg = clamped;
            ApplyNeedleRotation();
            PressureChanged?.Invoke(currentPressureMmHg);
        }

        private void ApplyNeedleRotation()
        {
            if (gaugeNeedle == null) return;

            float t = Mathf.InverseLerp(MinPressureMmHg, MaxPressureMmHg, currentPressureMmHg);
            float angle = Mathf.Lerp(needleAngleAtZero, needleAngleAtMax, t);
            gaugeNeedle.localRotation = Quaternion.AngleAxis(angle, needleRotationAxis.normalized);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Allow designers to scrub currentPressureMmHg in the Inspector
            // and see the needle update live, without pushing a duplicate
            // event into the listener graph at edit time.
            currentPressureMmHg = Mathf.Clamp(currentPressureMmHg, MinPressureMmHg, MaxPressureMmHg);
            if (!Application.isPlaying)
                ApplyNeedleRotation();
        }
#endif
    }
}
