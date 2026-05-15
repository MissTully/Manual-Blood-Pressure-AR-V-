using System.Collections.Generic;

namespace Encountive.Domain
{
    /// <summary>
    /// One-time per-session report the HAL emits at session start
    /// enumerating the OpenXR features / interaction profiles available
    /// on the active target (SDD §4.6, §9.6). Plain data so it can be
    /// written verbatim into the audit log and an xAPI context
    /// extension without a Unity dependency.
    /// </summary>
    public sealed class CapabilityReport
    {
        /// <summary>"galaxy_xr" | "xreal_aura" | "webxr".</summary>
        public string Target { get; set; }
        public string RuntimeName { get; set; }

        public bool HasHandTracking { get; set; }
        public bool HasEyeGaze { get; set; }
        public bool HasControllers { get; set; }
        public bool HasSpatialAudio { get; set; }
        public bool HasPassthrough { get; set; }
        public bool HasMicrophone { get; set; }

        /// <summary>Interaction → selected input profile, after the
        /// fallback chain in SDD §4.5 has been resolved.</summary>
        public IDictionary<string, string> SelectedProfiles { get; set; }
            = new Dictionary<string, string>();

        /// <summary>True only when a hard prerequisite is missing (e.g.
        /// no OpenXR runtime bound at all). The HAL refuses to start the
        /// session and emits a diagnostic rather than proceeding
        /// silently (SDD §4.6).</summary>
        public bool HasBlockingDeficiency { get; set; }
        public string Diagnostic { get; set; }
    }
}
