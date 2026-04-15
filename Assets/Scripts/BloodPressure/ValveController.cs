using UnityEngine;
using UnityEngine.Events;
#if TRAINAR_XR_HMD
using UnityEngine.InputSystem;
#endif

namespace BloodPressure
{
    /// <summary>
    /// Drives continuous deflation of the cuff through the release valve.
    /// A 0..1 control value (thumbstick axis, trigger value, or — if the
    /// art team models the valve as a twistable knob — a local-rotation
    /// mapping) is scaled to a mmHg/s rate and fed to
    /// <see cref="CuffPressureSimulator.Deflate"/> every frame.
    ///
    /// Critically this component is also the source of the clinical
    /// feedback that teaches the learner the correct 2–3 mmHg/s
    /// deflation window. It broadcasts the current rate and a
    /// <see cref="InCorrectWindowChanged"/> boolean so the HUD can
    /// highlight "too fast / too slow / just right" in real time.
    /// That gated feedback is the single most important pedagogical
    /// affordance of the whole BP simulator: real learners under-read
    /// systolic when they deflate too fast, and a silent mistake in
    /// VR teaches nothing.
    /// </summary>
    public class ValveController : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField]
        [Tooltip("The cuff whose pressure this valve releases.")]
        private CuffPressureSimulator cuff;

        [Header("Rate mapping")]
        [SerializeField]
        [Tooltip("Maximum deflation rate in mmHg/s when the control value is fully 1.")]
        [Range(1f, 20f)]
        private float maxRateMmHgPerSecond = 10f;

        [SerializeField]
        [Tooltip("Lower bound of the clinically correct deflation window (mmHg/s).")]
        [Range(0f, 10f)]
        private float correctWindowLow = 2f;

        [SerializeField]
        [Tooltip("Upper bound of the clinically correct deflation window (mmHg/s).")]
        [Range(0f, 10f)]
        private float correctWindowHigh = 3f;

        [SerializeField]
        [Tooltip("Below this threshold the valve is considered closed and no deflation / feedback fires.")]
        [Range(0f, 1f)]
        private float deadZone = 0.05f;

        [Header("Gating")]
        [SerializeField]
        [Tooltip("If true the valve only vents while the cuff pressure is non-zero, suppressing feedback during setup.")]
        private bool onlyWhileCuffInflated = true;

        [Header("Events")]
        [Tooltip("Fires every frame the rate changes. Argument is the current deflation rate in mmHg/s (0 when closed).")]
        public UnityEvent<float> DeflationRateChanged = new UnityEvent<float>();

        [Tooltip("Fires only when the in-window state flips. Argument is true while the current rate sits inside [correctWindowLow, correctWindowHigh].")]
        public UnityEvent<bool> InCorrectWindowChanged = new UnityEvent<bool>();

#if TRAINAR_XR_HMD
        [Header("XR Input")]
        [SerializeField]
        [Tooltip("InputAction returning a 0..1 float that controls how open the valve is (e.g. XRI RightHand Locomotion/Move.y, clamped to positive, or a trigger Value action).")]
        private InputActionProperty valveAction;
#endif

        private float currentRateMmHgPerSecond = 0f;
        private bool lastInWindow = false;

        /// <summary>
        /// Current deflation rate in mmHg/s. Zero when the valve is
        /// closed or inside the configured dead zone.
        /// </summary>
        public float CurrentRateMmHgPerSecond => currentRateMmHgPerSecond;

        /// <summary>
        /// True while the current rate sits inside the clinically
        /// correct deflation window.
        /// </summary>
        public bool IsInCorrectWindow =>
            currentRateMmHgPerSecond >= correctWindowLow &&
            currentRateMmHgPerSecond <= correctWindowHigh;

#if TRAINAR_XR_HMD
        private void OnEnable()
        {
            if (valveAction.action != null)
            {
                valveAction.action.Enable();
            }
        }
#endif

        private void Update()
        {
            float controlValue = ReadControlValue();
            ApplyControlValue(controlValue);
        }

        private float ReadControlValue()
        {
#if TRAINAR_XR_HMD
            if (valveAction.action != null)
            {
                // Clamp to positive so a negative thumbstick axis cannot
                // "close past closed" and confuse the dead-zone check.
                return Mathf.Clamp01(valveAction.action.ReadValue<float>());
            }
#endif
            return 0f;
        }

        private void ApplyControlValue(float controlValue)
        {
            // Apply a dead zone so a resting thumbstick does not trickle
            // the cuff down and silently ruin a case mid-auscultation.
            if (controlValue < deadZone)
            {
                SetRate(0f);
                return;
            }

            if (onlyWhileCuffInflated && cuff != null && cuff.CurrentPressureMmHg <= 0f)
            {
                SetRate(0f);
                return;
            }

            // Remap [deadZone, 1] -> [0, 1] so the usable range of the
            // stick starts at zero rate right after crossing the
            // dead-zone threshold.
            float t = Mathf.InverseLerp(deadZone, 1f, controlValue);
            float rate = t * maxRateMmHgPerSecond;

            SetRate(rate);

            if (cuff != null && rate > 0f)
            {
                cuff.Deflate(rate * Time.deltaTime);
            }
        }

        /// <summary>
        /// Public entry point so visual scripting, UI sliders, or the
        /// handheld AR build can drive the valve without XRI.
        /// </summary>
        public void SetRate(float rateMmHgPerSecond)
        {
            float clamped = Mathf.Clamp(rateMmHgPerSecond, 0f, maxRateMmHgPerSecond);

            if (!Mathf.Approximately(clamped, currentRateMmHgPerSecond))
            {
                currentRateMmHgPerSecond = clamped;
                DeflationRateChanged?.Invoke(currentRateMmHgPerSecond);
            }

            bool inWindow = IsInCorrectWindow;
            if (inWindow != lastInWindow)
            {
                lastInWindow = inWindow;
                InCorrectWindowChanged?.Invoke(inWindow);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (correctWindowLow > correctWindowHigh)
            {
                correctWindowLow = correctWindowHigh;
            }
        }
#endif
    }
}
