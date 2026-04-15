using UnityEngine;
using UnityEngine.Events;

namespace BloodPressure
{
    /// <summary>
    /// Sits on the brachial-artery anchor GameObject (a small invisible
    /// child of the patient's upper-arm mesh with a trigger collider).
    /// When the stethoscope chestpiece enters the trigger, it flips
    /// <see cref="KorotkoffAudioEngine.SetListening"/> to true; on exit,
    /// back to false. This is the second of the three gates that keep
    /// the BP simulation from degenerating into "press button to hear
    /// the answer" — see KorotkoffAudioEngine for the other two.
    ///
    /// Why this script lives on the anchor, not on the chestpiece:
    /// the brachial anchor is a scene-authored landmark that knows
    /// where the clinically correct auscultation point is on this
    /// specific patient model. Multiple future patient models can ship
    /// with their own anchor placement without any change to the
    /// chestpiece prefab.
    ///
    /// Chestpiece identification: by default, matches the entering
    /// collider by tag (<see cref="chestpieceTag"/>, default
    /// <c>"Chestpiece"</c>) so one anchor prefab works across many
    /// chestpiece variants. Authors can optionally pin to a single
    /// specific chestpiece via <see cref="chestpieceOverride"/>.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class StethoscopePlacementValidator : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField]
        [Tooltip("KorotkoffAudioEngine whose listening state this validator toggles on contact.")]
        private KorotkoffAudioEngine korotkoffEngine;

        [Header("Matching")]
        [SerializeField]
        [Tooltip("Tag used to identify the chestpiece collider. Leave chestpieceOverride unset to match any collider with this tag.")]
        private string chestpieceTag = "Chestpiece";

        [SerializeField]
        [Tooltip("Optional. When set, only this specific GameObject (or a child of it) counts as the chestpiece; the tag check is skipped.")]
        private GameObject chestpieceOverride;

        [Header("Events")]
        [Tooltip("Fires when the chestpiece first enters the brachial-artery trigger.")]
        public UnityEvent OnPlaced = new UnityEvent();

        [Tooltip("Fires when the chestpiece leaves the brachial-artery trigger.")]
        public UnityEvent OnRemoved = new UnityEvent();

        private bool chestpieceInside = false;

        private void Awake()
        {
            var col = GetComponent<Collider>();
            if (!col.isTrigger)
            {
                Debug.LogWarning(
                    "[StethoscopePlacementValidator] The attached collider should be set to isTrigger; forcing it on at runtime.",
                    this);
                col.isTrigger = true;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsChestpiece(other)) return;
            if (chestpieceInside) return;

            chestpieceInside = true;
            if (korotkoffEngine != null) korotkoffEngine.SetListening(true);
            OnPlaced?.Invoke();
        }

        private void OnTriggerExit(Collider other)
        {
            if (!IsChestpiece(other)) return;
            if (!chestpieceInside) return;

            chestpieceInside = false;
            if (korotkoffEngine != null) korotkoffEngine.SetListening(false);
            OnRemoved?.Invoke();
        }

        private void OnDisable()
        {
            // Defensive: if the validator is disabled mid-case (e.g. the
            // patient model is despawned) make sure the Korotkoff engine
            // does not get stranded in the "listening" state and keep
            // playing heartbeats into a detached chestpiece.
            if (chestpieceInside && korotkoffEngine != null)
            {
                korotkoffEngine.SetListening(false);
            }
            chestpieceInside = false;
        }

        private bool IsChestpiece(Collider other)
        {
            if (chestpieceOverride != null)
            {
                // Accept the override itself or any of its children, since
                // the trigger-collider is usually on a child of the
                // chestpiece root (the actual bell mesh).
                return other.transform.IsChildOf(chestpieceOverride.transform) ||
                       other.gameObject == chestpieceOverride;
            }

            return other.CompareTag(chestpieceTag);
        }
    }
}
