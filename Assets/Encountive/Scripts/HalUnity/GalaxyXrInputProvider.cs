using System;
using UnityEngine;
#if TRAINAR_XR_HMD
using UnityEngine.InputSystem;
#endif

namespace Encountive.HalUnity
{
    /// <summary>
    /// Phase 1 Galaxy XR <see cref="IXrInputProvider"/>. Normalizes XRI
    /// hand / controller input into the target-agnostic events the
    /// per-station code consumes. It complements the existing TrainAR
    /// <c>Interaction.XRInteractionAdapter</c> (which re-sources
    /// TrainAR's legacy raycast) by exposing the same gestures through
    /// the SDD §7.4 HAL contract — the two coexist; this is the path new
    /// Encountive station code uses.
    ///
    /// Compiles to an inert component when the XR input packages are not
    /// resolved (no TRAINAR_XR_HMD), mirroring the proven guard pattern
    /// already used in <c>XRInteractionAdapter</c> so the handheld build
    /// is never broken.
    /// </summary>
    public sealed class GalaxyXrInputProvider : MonoBehaviour, IXrInputProvider
    {
        public event Action<XrPointerEvent> Pinch;
        public event Action<XrPointerEvent> Tap;
        public event Action<XrPointerEvent> DragUpdate;
        public event Action DragEnd;

        [SerializeField]
        [Tooltip("Pose whose forward is the gaze/aim ray. Typically the XR camera or eye-gaze pose.")]
        private Transform gazePose;

#if TRAINAR_XR_HMD
        [SerializeField]
        [Tooltip("InputAction for the primary pinch/select. Performed = pinch; Canceled = drag end.")]
        private InputActionProperty pinchAction;

        [SerializeField]
        [Tooltip("InputAction for a discrete tap/confirm.")]
        private InputActionProperty tapAction;

        private bool _dragging;

        private void OnEnable()
        {
            if (pinchAction.action != null)
            {
                pinchAction.action.performed += OnPinch;
                pinchAction.action.canceled += OnPinchReleased;
                pinchAction.action.Enable();
            }
            if (tapAction.action != null)
            {
                tapAction.action.performed += OnTap;
                tapAction.action.Enable();
            }
        }

        private void OnDisable()
        {
            if (pinchAction.action != null)
            {
                pinchAction.action.performed -= OnPinch;
                pinchAction.action.canceled -= OnPinchReleased;
            }
            if (tapAction.action != null)
                tapAction.action.performed -= OnTap;
        }

        private void Update()
        {
            if (_dragging) DragUpdate?.Invoke(BuildEvent());
        }

        private void OnPinch(InputAction.CallbackContext _)
        {
            _dragging = true;
            Pinch?.Invoke(BuildEvent());
        }

        private void OnPinchReleased(InputAction.CallbackContext _)
        {
            if (!_dragging) return;
            _dragging = false;
            DragEnd?.Invoke();
        }

        private void OnTap(InputAction.CallbackContext _) => Tap?.Invoke(BuildEvent());
#endif

        public bool TryGetGazeRay(out Ray ray)
        {
            Transform t = gazePose != null ? gazePose
                : (Camera.main != null ? Camera.main.transform : null);
            if (t == null)
            {
                ray = default;
                return false;
            }
            ray = new Ray(t.position, t.forward);
            return true;
        }

        private XrPointerEvent BuildEvent()
        {
            bool hasRay = TryGetGazeRay(out Ray ray);
            return new XrPointerEvent
            {
                WorldPosition = hasRay ? ray.origin : Vector3.zero,
                Ray = ray,
                HasRay = hasRay
            };
        }
    }
}
