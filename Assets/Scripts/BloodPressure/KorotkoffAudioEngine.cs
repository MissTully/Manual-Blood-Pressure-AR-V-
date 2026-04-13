using UnityEngine;

namespace BloodPressure
{
    /// <summary>
    /// Schedules Korotkoff heartbeat sounds on the stethoscope chestpiece
    /// based on the authoritative cuff pressure from
    /// <see cref="CuffPressureSimulator"/> and the ground-truth reading
    /// authored on a <see cref="PatientProfile"/>.
    ///
    /// Audio is played through a <see cref="chestpieceAudio"/>
    /// AudioSource that sits on the stethoscope chestpiece GameObject.
    /// Keeping the source there (rather than 2D-mixed into the master
    /// channel) means the sound attenuates with distance from the
    /// simulated brachial artery — which, combined with
    /// <see cref="StethoscopePlacementValidator"/> gating this engine's
    /// listening state, forces the learner to actually aim the
    /// chestpiece at the right spot on the arm instead of hearing the
    /// heartbeat regardless of where the stethoscope is.
    ///
    /// Clip selection across the audible window:
    /// <list type="bullet">
    ///   <item>fraction ≈ 1 (just below systolic) → <c>phaseClips[0]</c>
    ///     — crisp appearance tap, Korotkoff phase I</item>
    ///   <item>fraction ≈ 0 (just above diastolic) → <c>phaseClips[N-1]</c>
    ///     — muffled disappearance, Korotkoff phase IV/V</item>
    ///   <item>Intermediate pressures pick intermediate clips linearly,
    ///     so authors can drop in as many or as few phase clips as
    ///     they have recordings for.</item>
    /// </list>
    /// </summary>
    public class KorotkoffAudioEngine : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField]
        [Tooltip("Source of the authoritative cuff pressure. Subscribed in OnEnable.")]
        private CuffPressureSimulator cuff;

        [SerializeField]
        [Tooltip("Active patient case. Provides systolic, diastolic, heart rate, and the audible-window calculation.")]
        private PatientProfile patientProfile;

        [SerializeField]
        [Tooltip("AudioSource on the stethoscope chestpiece GameObject. Should be 3D-spatialised so sound attenuates off the brachial-artery anchor.")]
        private AudioSource chestpieceAudio;

        [Header("Clips")]
        [SerializeField]
        [Tooltip("Korotkoff heartbeat recordings ordered from phase I (index 0, near systolic) to phase IV/V (last index, near diastolic).")]
        private AudioClip[] phaseClips;

        [SerializeField]
        [Tooltip("Optional looping low-level chestpiece hiss played while the learner is listening but no Korotkoff sound should fire.")]
        private AudioClip ambientHissLoop;

        [Header("Volume")]
        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Volume applied to each heartbeat PlayOneShot call.")]
        private float heartbeatVolume = 1f;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Volume of the ambient hiss AudioSource while the learner is listening.")]
        private float ambientHissVolume = 0.15f;

        /// <summary>
        /// Seconds until the next scheduled heartbeat. Counted down in
        /// <see cref="Update"/> while the engine is listening and the
        /// current pressure is inside the audible window.
        /// </summary>
        private float timeUntilNextBeat = 0f;

        /// <summary>
        /// True when the stethoscope chestpiece is currently placed on
        /// the brachial-artery anchor. Toggled by
        /// <see cref="StethoscopePlacementValidator"/>.
        /// </summary>
        private bool isListening = false;

        /// <summary>
        /// Cached last pressure so callers that only provide late
        /// <see cref="SetPatientProfile"/> updates still behave sensibly.
        /// </summary>
        private float lastPressureMmHg = 0f;

        private void OnEnable()
        {
            if (cuff != null)
            {
                cuff.PressureChanged.AddListener(OnPressureChanged);
                // Seed with the current value so we do not wait for the
                // next inflate/deflate event to learn about the starting
                // pressure (mostly 0 but the scene may have authored a
                // non-zero initial state).
                lastPressureMmHg = cuff.CurrentPressureMmHg;
            }

            ApplyAmbientHissState();
        }

        private void OnDisable()
        {
            if (cuff != null)
            {
                cuff.PressureChanged.RemoveListener(OnPressureChanged);
            }
        }

        /// <summary>
        /// Called by <see cref="StethoscopePlacementValidator"/> as the
        /// chestpiece enters / exits the brachial-artery trigger.
        /// </summary>
        public void SetListening(bool listening)
        {
            if (isListening == listening) return;
            isListening = listening;

            // Reset the beat timer whenever listening flips so the first
            // heartbeat after re-placing the stethoscope lands at a
            // realistic phase offset rather than firing instantly.
            if (isListening)
            {
                timeUntilNextBeat = SecondsPerBeat();
            }

            ApplyAmbientHissState();
        }

        /// <summary>
        /// Swap patient cases at runtime (e.g. between two cases in the
        /// same training session).
        /// </summary>
        public void SetPatientProfile(PatientProfile profile)
        {
            patientProfile = profile;
        }

        private void OnPressureChanged(float pressureMmHg)
        {
            lastPressureMmHg = pressureMmHg;
        }

        private void Update()
        {
            if (!isListening) return;
            if (patientProfile == null) return;
            if (!patientProfile.IsKorotkoffAudibleAt(lastPressureMmHg)) return;
            if (phaseClips == null || phaseClips.Length == 0) return;
            if (chestpieceAudio == null) return;

            timeUntilNextBeat -= Time.deltaTime;
            if (timeUntilNextBeat > 0f) return;

            AudioClip clip = PickPhaseClip(lastPressureMmHg);
            if (clip != null)
            {
                chestpieceAudio.PlayOneShot(clip, heartbeatVolume);
            }

            timeUntilNextBeat += SecondsPerBeat();
            // Guard against underflow if deltaTime was enormous (breakpoint).
            if (timeUntilNextBeat < 0f) timeUntilNextBeat = SecondsPerBeat();
        }

        private float SecondsPerBeat()
        {
            int bpm = patientProfile != null ? Mathf.Max(1, patientProfile.heartRateBpm) : 60;
            return 60f / bpm;
        }

        private AudioClip PickPhaseClip(float pressureMmHg)
        {
            // fraction = 1 near systolic (phase I), fraction = 0 near diastolic (phase V).
            float fraction = Mathf.InverseLerp(
                patientProfile.diastolicMmHg,
                patientProfile.systolicMmHg,
                pressureMmHg);

            // Map so index 0 = highest pressure = phase I,
            // index N-1 = lowest pressure = phase IV/V.
            int lastIndex = phaseClips.Length - 1;
            int index = Mathf.Clamp(
                Mathf.RoundToInt((1f - fraction) * lastIndex),
                0,
                lastIndex);
            return phaseClips[index];
        }

        private void ApplyAmbientHissState()
        {
            if (chestpieceAudio == null) return;

            if (isListening && ambientHissLoop != null)
            {
                if (chestpieceAudio.clip != ambientHissLoop)
                {
                    chestpieceAudio.clip = ambientHissLoop;
                    chestpieceAudio.loop = true;
                }
                chestpieceAudio.volume = ambientHissVolume;
                if (!chestpieceAudio.isPlaying) chestpieceAudio.Play();
            }
            else
            {
                // Not listening: silence the hiss loop but leave the
                // AudioSource otherwise untouched so PlayOneShot calls
                // from any other path still work.
                if (chestpieceAudio.isPlaying) chestpieceAudio.Stop();
            }
        }
    }
}
