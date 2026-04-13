using Interaction;
using UnityEngine;
using UnityEngine.Events;
#if TRAINAR_XR_HMD
using UnityEngine.InputSystem;
#endif

namespace BloodPressure
{
    /// <summary>
    /// Sits on the inflation bulb TrainAR object. When the bulb is the
    /// currently grabbed object in the scene and the user presses the
    /// configured squeeze input, raises the cuff pressure by a fixed
    /// bolus, plays a squeeze sound, and raises a UnityEvent that
    /// BP_XRRig wires to an XRI haptic-impulse sender.
    ///
    /// Real rubber bulbs behave as discrete "pump once, deliver one
    /// bolus of air" devices rather than a continuous rate, so this
    /// component is edge-triggered on the squeeze input rather than
    /// applying a while-held rate. A short <see cref="cooldownSeconds"/>
    /// prevents a shaky trigger from double-counting a single squeeze.
    ///
    /// Why haptics live on a UnityEvent instead of being called directly:
    /// keeping BulbSqueezeInteractor free of XRI haptic types means this
    /// script compiles on the handheld ARCore path too, and the rig
    /// designer can retarget haptics to a left-hand device, a gamepad
    /// rumble, or nothing at all without recompiling.
    /// </summary>
    [RequireComponent(typeof(TrainARObject))]
    public class BulbSqueezeInteractor : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField]
        [Tooltip("The cuff whose pressure is raised by each squeeze.")]
        private CuffPressureSimulator cuff;

        [SerializeField]
        [Tooltip("Scene InteractionController used to gate squeeze input on 'is this bulb currently grabbed?'.")]
        private InteractionController interactionController;

        [Header("Inflation")]
        [SerializeField]
        [Tooltip("How much pressure one squeeze adds to the cuff, in mmHg.")]
        [Range(1f, 60f)]
        private float mmHgPerSqueeze = 20f;

        [SerializeField]
        [Tooltip("Minimum seconds between two squeezes so a shaky trigger does not double-count.")]
        [Range(0f, 1f)]
        private float cooldownSeconds = 0.08f;

        [SerializeField]
        [Tooltip("Require the bulb to be the currently grabbed object before a squeeze counts. Turn this off for a practice mode where the user can inflate without picking the bulb up first.")]
        private bool requireBulbGrabbed = true;

        [Header("Feedback")]
        [SerializeField]
        [Tooltip("Optional AudioSource on the bulb that plays a squeeze cue.")]
        private AudioSource bulbAudio;

        [SerializeField]
        [Tooltip("Clip played on each successful squeeze.")]
        private AudioClip squeezeClip;

        [Tooltip("Fires on each successful squeeze. BP_XRRig wires this to an XRI haptic-impulse sender on the hand that is holding the bulb.")]
        public UnityEvent OnSqueezed = new UnityEvent();

#if TRAINAR_XR_HMD
        [Header("XR Input")]
        [SerializeField]
        [Tooltip("InputAction bound to the squeeze trigger (e.g. XRI RightHand Interaction/Activate Value, button threshold).")]
        private InputActionProperty squeezeAction;
#endif

        private TrainARObject trainARObject;
        private float lastSqueezeTime = -999f;

        private void Awake()
        {
            trainARObject = GetComponent<TrainARObject>();
        }

#if TRAINAR_XR_HMD
        private void OnEnable()
        {
            if (squeezeAction.action != null)
            {
                squeezeAction.action.performed += OnSqueezeActionPerformed;
                squeezeAction.action.Enable();
            }
        }

        private void OnDisable()
        {
            if (squeezeAction.action != null)
            {
                squeezeAction.action.performed -= OnSqueezeActionPerformed;
            }
        }

        private void OnSqueezeActionPerformed(InputAction.CallbackContext ctx)
        {
            TrySqueeze();
        }
#endif

        /// <summary>
        /// Public entry point for a squeeze attempt. Exposed so visual
        /// scripting, UI buttons, or the handheld AR build can drive the
        /// bulb without going through XRI input actions.
        /// </summary>
        public void TrySqueeze()
        {
            if (cuff == null)
            {
                Debug.LogWarning("[BulbSqueezeInteractor] No cuff reference; squeeze ignored.", this);
                return;
            }

            if (Time.time - lastSqueezeTime < cooldownSeconds) return;

            if (requireBulbGrabbed)
            {
                if (interactionController == null) return;
                if (interactionController.grabbedObject != gameObject) return;
            }

            lastSqueezeTime = Time.time;
            cuff.Inflate(mmHgPerSqueeze);

            if (bulbAudio != null && squeezeClip != null)
            {
                bulbAudio.PlayOneShot(squeezeClip);
            }

            OnSqueezed?.Invoke();
        }
    }
}
