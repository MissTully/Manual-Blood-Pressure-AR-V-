# Samsung Galaxy XR — Project Setup Notes

This document records the step-1 "Project & Platform Setup" work for turning
TrainAR into a head-mounted Manual Blood Pressure training app on Samsung
Galaxy XR (Android XR / OpenXR).

## What has already been committed

| Change | File | Purpose |
| --- | --- | --- |
| Added OpenXR, XR Management, XR Interaction Toolkit, XR Hands | `Packages/manifest.json` | Brings in the OpenXR runtime, plug-in loader, and XRI interactors needed by the headset path |
| Bundle id → `com.mixality.bptrainer` | `ProjectSettings/ProjectSettings.asset` | BP-specific application identifier |
| Color space → Linear | `ProjectSettings/ProjectSettings.asset` | Required for PBR/XR rendering on Galaxy XR |
| Stereo rendering path → Single Pass Instanced (2) | `ProjectSettings/ProjectSettings.asset` | Recommended stereo path for OpenXR on Android XR |
| Android graphics API → Vulkan | `ProjectSettings/ProjectSettings.asset` | Android XR requires Vulkan |
| `ARSessionIsReady` gated behind `TRAINAR_XR_HMD` | `Assets/Scripts/Others/ARSessionIsReady.cs` | When the XR define is on, the loading screen dismisses without waiting for an ARFoundation session |

IL2CPP (`scriptingBackend.Android: 1`) and ARM64-only builds
(`AndroidTargetArchitectures: 2`) were already set by upstream TrainAR, so no
edit was required.

## What MUST be finished inside the Unity Editor

Some XR settings live in Unity-generated asset files whose GUIDs are written
by the Editor itself. These cannot be safely hand-edited from the command
line. Open the project in Unity 2022.3.49f1 LTS and run the following once:

1. **Open the project.** Unity will resolve the new packages from
   `Packages/manifest.json`. Wait for the Package Manager to finish.
2. **Edit → Project Settings → Player → Other Settings → Scripting Define
   Symbols (Android tab)** and append `;TRAINAR_XR_HMD`. This activates the
   compile-time gates in `ARSessionIsReady.cs` (and any future BP scripts that
   need to differ between handheld AR and head-mounted XR).
3. **Edit → Project Settings → XR Plug-in Management**
   - Click *Install XR Plug-in Management* if prompted.
   - On the **Android** tab, tick **OpenXR**.
   - If the Android XR feature group / Samsung Galaxy XR feature is available,
     enable it under *OpenXR → Feature Groups → Android XR*.
4. **Edit → Project Settings → XR Plug-in Management → OpenXR (Android tab)**
   - Set the **Render Mode** to *Single Pass Instanced*.
   - Under *Interaction Profiles*, add: **Khronos Simple Controller Profile**,
     **Oculus Touch Controller Profile** (as a dev fallback), and enable
     **Hand Tracking Subsystem** from the OpenXR feature list.
5. **File → Build Settings → Android → Switch Platform.** Confirm the
   resulting player settings show:
   - Scripting Backend: IL2CPP
   - Target Architectures: ARM64 only
   - Graphics API: Vulkan (only)
   - Color Space: Linear
   - Minimum API Level: Android 7.0 (24) — unchanged
   - Target API Level: highest installed (leave *Automatic*)

After step 5 commit the generated files:

```
Assets/XR/**
Assets/XR.meta
ProjectSettings/XRSettings.asset        (if created)
Packages/packages-lock.json              (updated by the package resolver)
```

## Verification

- The project compiles with no console errors in both configurations:
  - `TRAINAR_XR_HMD` **off** → existing handheld ARCore scene still runs.
  - `TRAINAR_XR_HMD` **on** → `ARSessionIsReady` no longer waits on
    `ARSession.state` and the Android build succeeds against the OpenXR loader.
- `adb install` of the resulting APK on a Galaxy XR (or Meta Quest as a stand-in
  during development) boots into a stereo view with tracked head + controllers.

## Authoring `BP_XRRig.prefab` (step 2, Editor-side)

`XRInteractionAdapter.cs` is committed and ready to drive the existing
`InteractionController` from XR controllers, but the rig prefab itself must
be authored in the Unity Editor because `.prefab` files reference the GUIDs
of components that only exist after the OpenXR + XRI packages have been
resolved.

Once the packages are installed (see step 3 above), do the following in the
Editor and commit the result:

1. **GameObject → XR → XR Origin (Action-based)**. This creates the XR
   Origin with a Camera Offset, Main Camera, and two `ActionBasedController`
   children (LeftHand, RightHand).
2. Add an `XRRayInteractor` to each controller (if not already present) and
   an `XRInteractorLineVisual` for a pointer line. Set **Raycast Mask** to
   exclude the `TrainARObject` tag so the XRI visual ray does not fight the
   TrainAR raycast — the TrainAR selection ray is what actually drives
   object selection via the adapter.
3. On the root of the XR Origin, add the `XRInteractionAdapter` component
   and wire:
   - `interactionController` → the `InteractionController` in the scene
     (from `Assets/Scene.unity` or the new BP scene).
   - `controllerAim` → the right-hand controller's aim pose transform
     (e.g. the `RightHand Controller` GameObject itself — its forward is
     the aim direction).
   - `selectAction` → `XRI RightHand Interaction/Select` from the default
     `XRI Default Input Actions` asset.
   - `activateAction` → `XRI RightHand Interaction/Activate`.
4. Add a second `XRInteractionAdapter` for the left hand if you want both
   hands to drive selection, or leave it right-hand-only to start.
5. Enable **Hand Tracking Subsystem** on both controllers via the XRI
   Hands Interaction Setup (GameObject → XR → Hand-Tracking).
6. **Drag the configured XR Origin into `Assets/Prefabs/BP_XRRig.prefab`.**
7. Commit `Assets/Prefabs/BP_XRRig.prefab` and its `.meta` file.

The adapter script is guarded by the `TRAINAR_XR_HMD` define set in step 2
of the *in-Editor* instructions above, so the input-action wiring only
compiles into the XR build and the handheld ARCore build is unaffected.

### Verification of the rig

- Press Play in the Editor with the **XR Device Simulator** enabled
  (Package Manager → XR Interaction Toolkit → Samples → XR Device
  Simulator). The simulated right controller's ray should highlight
  TrainAR objects in the coffee-machine example scene, pressing the
  simulated grip should grab them, and pressing the activate button
  should fire their `Interact` / `Combine` action.
- If nothing selects: confirm `interactionController.interactionRaySource`
  is being set (add a `Debug.Log` in `XRInteractionAdapter.Start`) and that
  the controller ray's forward actually hits the TrainAR object's collider.

## Why the XR settings asset was not created from the CLI

Unity's XR Plug-in Management stores its settings in
`Assets/XR/XRGeneralSettings.asset` and companion loader/feature assets whose
GUID references are generated by the Editor. Hand-authoring them risks
producing files whose GUIDs do not match the installed package versions,
which silently disables the XR loader at runtime. The safe workflow is to let
the Editor generate them once (step 3 above) and commit the result in a
follow-up change.
