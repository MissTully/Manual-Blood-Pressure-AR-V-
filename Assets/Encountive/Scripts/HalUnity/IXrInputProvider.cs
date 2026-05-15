using System;
using UnityEngine;

namespace Encountive.HalUnity
{
    /// <summary>A normalized XR interaction event, target-agnostic. The
    /// HAL fills these from whichever profile the resolver selected; the
    /// per-station code consumes the same shape on every device.</summary>
    public struct XrPointerEvent
    {
        public Vector3 WorldPosition;
        public Ray Ray;
        public bool HasRay;
    }

    /// <summary>
    /// SDD §7.4 — pinch / drag / tap / gaze events normalized across
    /// targets. This interface lives in the Unity assembly (it carries
    /// UnityEngine math types per the SDD), while the profile-selection
    /// policy that decides which device input feeds it is the pure,
    /// unit-tested <c>Encountive.Hal.InteractionProfileResolver</c>.
    /// </summary>
    public interface IXrInputProvider
    {
        event Action<XrPointerEvent> Pinch;
        event Action<XrPointerEvent> Tap;
        event Action<XrPointerEvent> DragUpdate;
        event Action DragEnd;

        bool TryGetGazeRay(out Ray ray);
    }
}
