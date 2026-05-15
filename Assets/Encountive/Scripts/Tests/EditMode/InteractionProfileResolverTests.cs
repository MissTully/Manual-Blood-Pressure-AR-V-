using Encountive.Domain;
using Encountive.Hal;
using NUnit.Framework;

namespace Encountive.SafetyGates.Tests
{
    /// <summary>Verifies the SDD §4.5 fallback chains resolve correctly
    /// per target capability profile, and that every chain always
    /// resolves (HAL never blocks task progress, SDD §4.6).</summary>
    public sealed class InteractionProfileResolverTests
    {
        private static CapabilityReport GalaxyXr() => new CapabilityReport
        {
            Target = "galaxy_xr",
            HasHandTracking = true,
            HasEyeGaze = true,
            HasControllers = true,
            HasMicrophone = true
        };

        private static CapabilityReport WebXr() => new CapabilityReport
        {
            Target = "webxr",
            HasHandTracking = false,
            HasEyeGaze = false,
            HasControllers = false,
            HasMicrophone = false
        };

        private static CapabilityReport AuraNoEyeGaze() => new CapabilityReport
        {
            Target = "xreal_aura",
            HasHandTracking = true,
            HasEyeGaze = false, // "pending dev kit" per SDD §4.4
            HasControllers = false,
            HasMicrophone = true
        };

        [Test]
        public void GalaxyXr_PrefersHandPrimaryProfiles()
        {
            var c = GalaxyXr();
            Assert.AreEqual(InteractionProfile.HandPinchGrab,
                InteractionProfileResolver.Resolve(InteractionId.PickUpCuff, c));
            Assert.AreEqual(InteractionProfile.EyeGazeStability,
                InteractionProfileResolver.Resolve(InteractionId.ReadManometer, c));
            Assert.AreEqual(InteractionProfile.SpeechToText,
                InteractionProfileResolver.Resolve(InteractionId.SpeakRationale, c));
        }

        [Test]
        public void WebXr_FallsBackToUniversalSubstitutes()
        {
            var c = WebXr();
            Assert.AreEqual(InteractionProfile.TouchTap,
                InteractionProfileResolver.Resolve(InteractionId.PickUpCuff, c));
            Assert.AreEqual(InteractionProfile.TouchDrag,
                InteractionProfileResolver.Resolve(InteractionId.WrapCuff, c));
            Assert.AreEqual(InteractionProfile.MouseHover,
                InteractionProfileResolver.Resolve(InteractionId.ReadManometer, c));
            Assert.AreEqual(InteractionProfile.MultipleChoiceCard,
                InteractionProfileResolver.Resolve(InteractionId.SpeakRationale, c));
        }

        [Test]
        public void Aura_WithoutEyeGaze_FallsBackToHandPointerForManometer()
        {
            var c = AuraNoEyeGaze();
            Assert.AreEqual(InteractionProfile.HandPointerStability,
                InteractionProfileResolver.Resolve(InteractionId.ReadManometer, c));
            Assert.AreEqual(InteractionProfile.HandPinchGrab,
                InteractionProfileResolver.Resolve(InteractionId.PickUpCuff, c));
        }

        [Test]
        public void EveryInteraction_AlwaysResolves_EvenWithNoCapabilities()
        {
            var none = new CapabilityReport();
            foreach (InteractionId id in System.Enum.GetValues(typeof(InteractionId)))
            {
                // Must not throw and must return a usable substitute.
                InteractionProfile p = InteractionProfileResolver.Resolve(id, none);
                Assert.IsTrue(
                    p == InteractionProfile.TouchTap ||
                    p == InteractionProfile.TouchDrag ||
                    p == InteractionProfile.MouseHover ||
                    p == InteractionProfile.MultipleChoiceCard,
                    $"{id} did not fall back to a universal substitute (got {p}).");
            }
        }
    }
}
