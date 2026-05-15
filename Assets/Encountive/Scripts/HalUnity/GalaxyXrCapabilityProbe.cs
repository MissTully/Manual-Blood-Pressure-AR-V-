using Encountive.Domain;
using Encountive.Hal;
using UnityEngine;

namespace Encountive.HalUnity
{
    /// <summary>
    /// Phase 1 Galaxy XR <see cref="ICapabilityProbe"/>. Queries the
    /// active XR input subsystems once and builds the plain
    /// <see cref="CapabilityReport"/> the HAL emits into the audit log
    /// at session start (SDD §4.6, §6.1.1).
    ///
    /// Galaxy XR runs Android XR via OpenXR with hand and eye tracking
    /// (SDD §4.4). The probe degrades to conservative values rather than
    /// throwing if a subsystem is absent, and sets
    /// <see cref="CapabilityReport.HasBlockingDeficiency"/> only when no
    /// XR runtime is bound at all — the HAL then refuses to start the
    /// session with a clear diagnostic instead of proceeding silently
    /// (SDD §4.6 rule 3).
    /// </summary>
    public sealed class GalaxyXrCapabilityProbe : ICapabilityProbe
    {
        public CapabilityReport ReadOnce()
        {
            var report = new CapabilityReport
            {
                Target = "galaxy_xr",
                RuntimeName = "OpenXR (Android XR)",
                HasControllers = true,
                HasSpatialAudio = true,
                HasPassthrough = true,
                HasMicrophone = true
            };

#if TRAINAR_XR_HMD
            bool xrActive =
                UnityEngine.XR.XRSettings.enabled &&
                !string.IsNullOrEmpty(UnityEngine.XR.XRSettings.loadedDeviceName);

            if (!xrActive)
            {
                report.HasBlockingDeficiency = true;
                report.Diagnostic =
                    "No OpenXR runtime bound. Refusing to start the session " +
                    "(SDD §4.6): verify XR Plug-in Management has OpenXR + " +
                    "Android XR enabled for the Android target.";
                return report;
            }

            report.HasHandTracking = true; // OpenXR Hand Tracking on Galaxy XR
            report.HasEyeGaze = true;      // OpenXR Eye Gaze Interaction
#else
            // Editor / non-HMD build: report a usable profile so the
            // experience still runs through the controller/touch chain.
            report.HasHandTracking = false;
            report.HasEyeGaze = false;
            report.Diagnostic = "TRAINAR_XR_HMD not defined; non-HMD capability profile.";
#endif

            report.SelectedProfiles["PickUpCuff"] =
                InteractionProfileResolver.Resolve(InteractionId.PickUpCuff, report).ToString();
            report.SelectedProfiles["ReadManometer"] =
                InteractionProfileResolver.Resolve(InteractionId.ReadManometer, report).ToString();
            report.SelectedProfiles["SpeakRationale"] =
                InteractionProfileResolver.Resolve(InteractionId.SpeakRationale, report).ToString();

            return report;
        }
    }
}
