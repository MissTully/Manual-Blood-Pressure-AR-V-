using System.Collections.Generic;
using Encountive.Domain;

namespace Encountive.Hal
{
    /// <summary>The per-station interactions that need a HAL-resolved
    /// input profile (SDD §4.5, §15.4).</summary>
    public enum InteractionId
    {
        PickUpCuff,
        WrapCuff,
        PlaceStethoscope,
        SqueezeBulb,
        TapMarker,
        ReadManometer,
        SpeakRationale,
        ConfirmGateRedirect
    }

    /// <summary>Concrete input profiles a HAL target can expose.</summary>
    public enum InteractionProfile
    {
        HandPinchGrab,
        HandDrag,
        HandPinchPlace,
        HandPinchReleaseRate,
        HandPinchTimeline,
        HandPinchConfirm,
        HandPointerStability,
        ControllerGrip,
        ControllerDrag,
        ControllerPinch,
        ControllerTriggerPulse,
        ControllerTrigger,
        ButtonHold,
        GazeTap,
        GazeDrag,
        EyeGazeStability,
        TouchTap,
        TouchDrag,
        MouseHover,
        SpeechToText,
        MultipleChoiceCard
    }

    /// <summary>SDD §7.4 — capability probe contract. Returns plain data
    /// (no Unity dependency) so the report is auditable verbatim.</summary>
    public interface ICapabilityProbe
    {
        CapabilityReport ReadOnce();
    }

    /// <summary>
    /// Deterministic implementation of the SDD §4.5 interaction → primary
    /// profile → fallback-chain selection. Pure logic, no Unity, so the
    /// graceful-degradation policy is unit-tested headlessly rather than
    /// only discovered on-device. The HAL exposes the resolved profile;
    /// per-station code never branches on target (SDD §4.6).
    ///
    /// Every chain terminates in a universally-available substitute
    /// (touch / multiple-choice) so a profile is always resolvable —
    /// the HAL never blocks task progress (SDD §4.6 rule 2).
    /// </summary>
    public static class InteractionProfileResolver
    {
        private static readonly Dictionary<InteractionId, InteractionProfile[]> Chains =
            new Dictionary<InteractionId, InteractionProfile[]>
            {
                [InteractionId.PickUpCuff] = new[]
                {
                    InteractionProfile.HandPinchGrab, InteractionProfile.ControllerGrip,
                    InteractionProfile.GazeTap, InteractionProfile.TouchTap
                },
                [InteractionId.WrapCuff] = new[]
                {
                    InteractionProfile.HandDrag, InteractionProfile.ControllerDrag,
                    InteractionProfile.GazeDrag, InteractionProfile.TouchDrag
                },
                [InteractionId.PlaceStethoscope] = new[]
                {
                    InteractionProfile.HandPinchPlace, InteractionProfile.ControllerPinch,
                    InteractionProfile.GazeTap, InteractionProfile.TouchTap
                },
                [InteractionId.SqueezeBulb] = new[]
                {
                    InteractionProfile.HandPinchReleaseRate, InteractionProfile.ControllerTriggerPulse,
                    InteractionProfile.ButtonHold
                },
                [InteractionId.TapMarker] = new[]
                {
                    InteractionProfile.HandPinchTimeline, InteractionProfile.ControllerTrigger,
                    InteractionProfile.GazeTap, InteractionProfile.TouchTap
                },
                [InteractionId.ReadManometer] = new[]
                {
                    InteractionProfile.EyeGazeStability, InteractionProfile.HandPointerStability,
                    InteractionProfile.MouseHover
                },
                [InteractionId.SpeakRationale] = new[]
                {
                    InteractionProfile.SpeechToText, InteractionProfile.MultipleChoiceCard
                },
                [InteractionId.ConfirmGateRedirect] = new[]
                {
                    InteractionProfile.HandPinchConfirm, InteractionProfile.ControllerTrigger,
                    InteractionProfile.GazeTap, InteractionProfile.TouchTap
                }
            };

        public static InteractionProfile Resolve(InteractionId interaction, CapabilityReport caps)
        {
            foreach (var profile in Chains[interaction])
            {
                if (IsSupported(profile, caps))
                    return profile;
            }
            // Defensive: chains are authored to always end in a
            // universal substitute, so this is unreachable in practice.
            return InteractionProfile.MultipleChoiceCard;
        }

        private static bool IsSupported(InteractionProfile p, CapabilityReport c)
        {
            switch (p)
            {
                case InteractionProfile.HandPinchGrab:
                case InteractionProfile.HandDrag:
                case InteractionProfile.HandPinchPlace:
                case InteractionProfile.HandPinchReleaseRate:
                case InteractionProfile.HandPinchTimeline:
                case InteractionProfile.HandPinchConfirm:
                case InteractionProfile.HandPointerStability:
                    return c.HasHandTracking;

                case InteractionProfile.ControllerGrip:
                case InteractionProfile.ControllerDrag:
                case InteractionProfile.ControllerPinch:
                case InteractionProfile.ControllerTriggerPulse:
                case InteractionProfile.ControllerTrigger:
                case InteractionProfile.ButtonHold:
                    return c.HasControllers;

                case InteractionProfile.GazeTap:
                case InteractionProfile.GazeDrag:
                case InteractionProfile.EyeGazeStability:
                    return c.HasEyeGaze;

                case InteractionProfile.SpeechToText:
                    return c.HasMicrophone;

                // Universal substitutes: always resolvable so the HAL
                // never blocks task progress (SDD §4.6).
                case InteractionProfile.TouchTap:
                case InteractionProfile.TouchDrag:
                case InteractionProfile.MouseHover:
                case InteractionProfile.MultipleChoiceCard:
                    return true;

                default:
                    return false;
            }
        }
    }
}
