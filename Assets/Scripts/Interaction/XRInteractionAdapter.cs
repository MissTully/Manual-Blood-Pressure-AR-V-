using UnityEngine;
#if TRAINAR_XR_HMD
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
#endif

namespace Interaction
{
    /// <summary>
    /// Bridges XR Interaction Toolkit (XRI) controller / hand input into
    /// TrainAR's existing <see cref="InteractionController"/> on head-mounted
    /// XR builds (Samsung Galaxy XR / Android XR via OpenXR).
    ///
    /// Rather than rewriting the TrainAR interaction + visual-scripting
    /// action nodes for XR, this adapter reuses them by:
    ///
    /// 1. Feeding the aim pose of an XRI controller into
    ///    <see cref="InteractionController.interactionRaySource"/> so
    ///    the existing center-of-screen raycast now originates from the
    ///    controller's pointer.
    /// 2. Translating XRI select press / release into calls to
    ///    <see cref="InteractionController.GrabObject"/> and
    ///    <see cref="InteractionController.ReleaseGrabbedObject"/>.
    /// 3. Translating a secondary "activate" press into
    ///    <see cref="InteractionController.Interact"/> (or
    ///    <see cref="InteractionController.Combine"/> when the grabbed
    ///    object is intersecting another TrainAR object).
    ///
    /// The adapter compiles to a no-op stub when the XRI package is not
    /// installed, so the existing handheld ARCore / ARKit build still
    /// builds on contributors who have not yet resolved the new packages.
    ///
    /// Wire this component up on the root of <c>BP_XRRig.prefab</c> with
    /// references to: the <see cref="InteractionController"/> in the
    /// scene, the controller aim <see cref="Transform"/>, and the XRI
    /// controller input-action properties for select + activate.
    /// </summary>
    public class XRInteractionAdapter : MonoBehaviour
    {
        [Header("TrainAR References")]
        [SerializeField]
        [Tooltip("The scene's InteractionController whose raycast we re-source from the XR controller.")]
        private InteractionController interactionController;

        [SerializeField]
        [Tooltip("Transform whose forward is used as the selection ray. Typically the 'aim' child of an XRI controller.")]
        private Transform controllerAim;

#if TRAINAR_XR_HMD
        [Header("XRI Input")]
        [SerializeField]
        [Tooltip("InputAction that fires on grip/trigger press. Performed = grab, Canceled = release.")]
        private InputActionProperty selectAction;

        [SerializeField]
        [Tooltip("InputAction that fires on the secondary 'activate' button (primary button / A / X).")]
        private InputActionProperty activateAction;

        private void OnEnable()
        {
            if (selectAction.action != null)
            {
                selectAction.action.performed += OnSelectPerformed;
                selectAction.action.canceled += OnSelectCanceled;
                selectAction.action.Enable();
            }

            if (activateAction.action != null)
            {
                activateAction.action.performed += OnActivatePerformed;
                activateAction.action.Enable();
            }
        }

        private void OnDisable()
        {
            if (selectAction.action != null)
            {
                selectAction.action.performed -= OnSelectPerformed;
                selectAction.action.canceled -= OnSelectCanceled;
            }

            if (activateAction.action != null)
            {
                activateAction.action.performed -= OnActivatePerformed;
            }
        }
#endif

        private void Start()
        {
            if (interactionController == null)
            {
                Debug.LogError(
                    "[XRInteractionAdapter] interactionController is not assigned. Selection will fall back to the AR camera center ray.",
                    this);
                return;
            }

            if (controllerAim == null)
            {
                Debug.LogError(
                    "[XRInteractionAdapter] controllerAim is not assigned. Assign it to an XRI controller's aim pose transform.",
                    this);
                return;
            }

            // Redirect TrainAR's selection raycast to originate from the
            // XR controller instead of the AR camera's viewport center.
            interactionController.interactionRaySource = controllerAim;
        }

        private void OnDestroy()
        {
            // Restore the original selection source so re-entering a
            // handheld scene after the XR adapter was used still works.
            if (interactionController != null &&
                interactionController.interactionRaySource == controllerAim)
            {
                interactionController.interactionRaySource = null;
            }
        }

#if TRAINAR_XR_HMD
        private void OnSelectPerformed(InputAction.CallbackContext ctx)
        {
            if (interactionController == null) return;

            // Clear any stale "failed grab" state from a previous frame so
            // the user can retry grabbing immediately after a miss.
            interactionController.tryedGrabbingObjectUnsuccessfully = false;

            if (interactionController.isSelectingObject && !interactionController.isGrabbingObject)
            {
                interactionController.GrabObject();
            }
        }

        private void OnSelectCanceled(InputAction.CallbackContext ctx)
        {
            if (interactionController == null) return;

            if (interactionController.isGrabbingObject)
            {
                interactionController.ReleaseGrabbedObject();
            }

            interactionController.tryedGrabbingObjectUnsuccessfully = false;
        }

        private void OnActivatePerformed(InputAction.CallbackContext ctx)
        {
            if (interactionController == null) return;

            // If the grabbed object is intersecting another TrainAR object,
            // the activate button triggers Combine; otherwise it triggers
            // Interact on the currently selected object.
            if (interactionController.isGrabbingObject && interactionController.isIntersecting)
            {
                interactionController.Combine();
                return;
            }

            if (interactionController.isSelectingObject)
            {
                interactionController.Interact();
            }
        }
#endif
    }
}
