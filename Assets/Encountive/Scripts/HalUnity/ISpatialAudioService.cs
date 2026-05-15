using UnityEngine;

namespace Encountive.HalUnity
{
    /// <summary>Logical mixer channels so per-station code never
    /// references a device audio path (SDD §4.8).</summary>
    public enum AudioMixerChannel
    {
        Coach,
        Patient,
        Stethoscope,
        Ambient
    }

    /// <summary>
    /// SDD §7.4 / §4.8 — hides per-target spatial audio routing
    /// (Galaxy XR HMD binaural, Aura tethered-puck headphones, WebXR
    /// host headphones) behind one call. Per-station code passes a
    /// world position and a clip id and never branches on target.
    /// </summary>
    public interface ISpatialAudioService
    {
        void Play(string clipId, Vector3 worldPos, AudioMixerChannel channel);
    }
}
