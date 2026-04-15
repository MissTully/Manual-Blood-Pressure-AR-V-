# Plan: Manual Blood Pressure Training App on Samsung Galaxy XR

## Context

This repository is a fork of **TrainAR** (`/home/user/Manual-Blood-Pressure-AR-V-`), an open-source Unity 2022.3.49f1 LTS authoring framework for procedural AR trainings. TrainAR ships with:
- A visual-scripting stateflow (`com.unity.visualscripting 1.9.4`) for non-programmer authoring
- An interaction/feedback layer (`Assets/Scripts/Interaction/`, `Assets/Scripts/Visual Scripting/`)
- A state-machine backbone (`Assets/Scripts/Static/StatemachineConnector.cs`)
- Handheld AR support via `com.unity.xr.arcore@5.1.5` + `com.unity.xr.arkit@5.1.5` + `com.unity.xr.arfoundation@5.1.5`
- An example "coffee machine" scenario as a template (`Assets/Models/CoffeeMachine/`)
- Main authoring scene at `Assets/Scene.unity` and tutorial at `Assets/Scenes/Tutorial.unity`

**Goal:** Extend TrainAR into a manual blood-pressure (BP) measurement training app that runs on a **Samsung Galaxy XR** headset. Galaxy XR is an Android-XR / OpenXR device, so the build target shifts from handheld ARCore/ARKit to head-mounted OpenXR with 6DOF controllers and/or hand tracking. There is currently **no BP-specific content or OpenXR plumbing** on the `claude/blood-pressure-app-requirements-JJQ1N` branch — everything below is net-new work that reuses TrainAR's authoring, state-machine, interaction, and feedback subsystems.

The training should cover the standard auscultatory BP procedure: patient/arm positioning → cuff placement → palpation of brachial/radial pulse → stethoscope placement → inflate cuff above estimated systolic → slow deflation (2–3 mmHg/s) → identify Korotkoff phase I (systolic) and phase V (diastolic) → record reading → debrief/assessment.

---

## Step-by-Step Plan

### 1. Project & Platform Setup (Galaxy XR / Android XR)
1. Add `com.unity.xr.management`, `com.unity.xr.openxr`, and the Android XR / Samsung OpenXR feature packages to `Packages/manifest.json`.
2. In `ProjectSettings/XRSettings` and `Project Settings → XR Plug-in Management`, enable **OpenXR** for the Android tab and enable the Android XR feature group + hand-tracking / controller interaction profiles.
3. Update `ProjectSettings/ProjectSettings.asset`:
   - Keep `minSdkVersion 24`, raise `targetSdkVersion` to the version required by Android XR.
   - Rename bundle id from `com.Mixality.TrainAR` to a BP-specific id (e.g. `com.mixality.bptrainer`).
   - Enable IL2CPP + ARM64 only (XR devices do not ship 32-bit).
   - Set color space to Linear, graphics API to Vulkan, and enable multi-view / single-pass instanced rendering.
4. Keep ARFoundation in the manifest for now but gate the ARCore-specific session objects behind a platform define so the handheld path still builds. All new BP content targets OpenXR.
5. Add the `com.unity.xr.interaction.toolkit` package (XRI) — it is the cleanest way to get ray interactors, direct interactors, and hand-tracking gestures usable from TrainAR's existing `TrainARObject` components.

### 2. XR Rig & Input Replacement
1. Create `Assets/Prefabs/BP_XRRig.prefab` containing an XR Origin, head/camera, two controller interactors, and a hand-tracking subsystem.
2. Replace the AR camera / plane detection in `Assets/Scene.unity` (and the new BP scene) with this rig. The existing `Assets/Prefabs/InfinityPlane.prefab` and `ARPointCloud` can be removed from the XR scene — Galaxy XR does not need them.
3. Add a thin adapter `Assets/Scripts/Interaction/XRInteractionAdapter.cs` that forwards XRI `SelectEnter/Exit` and `HoverEnter/Exit` events to TrainAR's existing `InteractionController.cs` so the visual-scripting action nodes (`Assets/Scripts/Visual Scripting/Action.cs`) continue to work unchanged. This is the key reuse — we do not rewrite the state machine, we just re-source its input events.
4. Respatialize the TrainAR UI: the current HUD canvas is a screen-space overlay. Convert the TrainAR instruction / feedback canvas (`Assets/Scripts/UI/`) to a **world-space** canvas parented to a head-locked but slightly damped anchor so instruction text, the gauge readout, and the Korotkoff listening indicator are always comfortably readable.

### 3. BP Domain Assets
1. Create `Assets/Models/BloodPressure/` and import (or author) these meshes, each converted via the TrainAR object conversion pipeline documented in `Documentation/manual/`:
   - Seated patient with a rigged left arm (bendable at elbow, posable)
   - Aneroid sphygmomanometer cuff (with separable bladder, tubing, bulb, release valve)
   - Aneroid gauge with animatable needle (single float `pressureMmHg` drives a local Z-rotation)
   - Binaural stethoscope (chestpiece, tubing, earpieces) — only the chestpiece needs physics
2. Add audio assets under `Assets/Audio/BloodPressure/`: cuff inflation hiss, deflation hiss, bulb squeeze, valve click, ambient clinic room tone, and a library of Korotkoff recordings (phase I–V) at several heart rates.
3. Author a `PatientProfile` ScriptableObject (`Assets/ScriptableObjects/PatientProfile.asset`) with `systolicMmHg`, `diastolicMmHg`, `heartRateBpm`, `armCircumferenceCm`, and `auscultatoryGap` fields so instructors can author multiple patient cases without code changes.

### 4. BP Simulation Scripts (new, under `Assets/Scripts/BloodPressure/`)
1. `CuffPressureSimulator.cs` — maintains `currentPressureMmHg`, exposes `Inflate(float deltaPerSecond)` and `Deflate(float deltaPerSecond)`, drives the gauge needle, and raises a `PressureChanged` UnityEvent each frame. Clamp 0–300 mmHg.
2. `BulbSqueezeInteractor.cs` — a `TrainARObject` subclass that converts controller grip-trigger squeezes into inflation pulses (`CuffPressureSimulator.Inflate`). Haptic pulse on each squeeze via the XRI controller haptic API.
3. `ValveController.cs` — maps the controller thumbstick or a twist gesture on the valve to a deflation rate in mmHg/s; highlights green when the rate is within the clinically correct 2–3 mmHg/s window.
4. `KorotkoffAudioEngine.cs` — subscribes to `PressureChanged`. When the cuff pressure is between `diastolic` and `systolic` (from the active `PatientProfile`), it schedules one heartbeat sound per `60/heartRate` seconds, selecting the Korotkoff phase clip appropriate to the current pressure window. Routes audio only to the stethoscope chestpiece's `AudioSource` so sound attenuates with distance from the brachial artery — giving the learner a real "listening" task.
5. `StethoscopePlacementValidator.cs` — uses a trigger collider on the brachial-artery landmark; while the chestpiece is inside, `KorotkoffAudioEngine` is unmuted. Otherwise the stethoscope emits only ambient hiss.
6. `BPMeasurementAssessment.cs` — records the learner's final systolic/diastolic entries (from a world-space numeric keypad), compares to the `PatientProfile` ground truth, scores within ±4 mmHg, and raises completion / error events that feed the existing TrainAR `Feedback.cs` + `Instructions.cs` visual-scripting nodes.

Reuse rather than duplicate: every script above should publish events that the **existing** TrainAR Feedback nodes (`Assets/Scripts/Visual Scripting/Feedback.cs`) and instruction nodes (`Assets/Scripts/Visual Scripting/Instructions.cs`) already consume, so the trainer/author can still drag-and-drop the lesson flow in the visual scripting graph.

### 5. Training Stateflow (visual scripting)
Author a new stateflow graph in a BP scene (`Assets/Scenes/BloodPressureTraining.unity`) using TrainAR's existing node palette. States, in order:
1. Onboarding / room placement (reuse TrainAR's onboarding animations)
2. "Greet and seat the patient" — proximity trigger on the chair
3. "Expose upper arm" — pickup + state toggle on a sleeve `TrainARObject`
4. "Wrap cuff ~2 cm above antecubital fossa" — combination check between `Cuff` and `Arm.BrachialAnchor` (mirrors the coffee-machine object-combination pattern already in `Assets/Models/CoffeeMachine/`)
5. "Palpate radial pulse" — timed hold interaction
6. "Place stethoscope" — `StethoscopePlacementValidator` gate
7. "Inflate to estimated systolic + 30 mmHg" — `CuffPressureSimulator` threshold
8. "Deflate at 2–3 mmHg/s" — `ValveController` rate window
9. "Mark systolic" and "Mark diastolic" — learner presses a world-space button at each Korotkoff transition
10. "Enter reading" + assessment + debrief screen

### 6. Ergonomics, Comfort & Accessibility
1. Add a seated/standing toggle in a preferences panel.
2. Gaze + pinch fallback for users without controllers (Galaxy XR supports hand tracking).
3. Captions track for every audio cue (Korotkoff listening is hearing-dependent; surface a visual waveform as an accessibility aid).
4. Vignette during any teleport/recentering to minimize motion sickness.

### 7. Build, Deploy, Verify
1. Switch platform to Android, verify OpenXR loader shows "Android XR" under enabled runtimes.
2. Build an APK, sideload to Galaxy XR via `adb install`.
3. End-to-end verification checklist:
   - XR rig tracks head + both controllers in the BP scene
   - Cuff can be grabbed, wrapped, and the state machine advances to the next step
   - Bulb squeeze raises gauge needle; haptic pulse fires per squeeze
   - Valve release drops pressure at a rate shown on the HUD; "too fast / too slow" feedback appears when out of 2–3 mmHg/s
   - Korotkoff sounds are audible only through the stethoscope and only inside the systolic–diastolic window
   - Assessment screen reports learner's reading vs. `PatientProfile` ground truth
4. Run TrainAR's existing authoring tool in-Editor against the new scene to confirm the visual-scripting graph remains editable by non-programmers.

---

## Critical Files

**Will be created**
- `Assets/Scenes/BloodPressureTraining.unity`
- `Assets/Prefabs/BP_XRRig.prefab`
- `Assets/Models/BloodPressure/*`
- `Assets/ScriptableObjects/PatientProfile.asset`
- `Assets/Scripts/BloodPressure/CuffPressureSimulator.cs`
- `Assets/Scripts/BloodPressure/BulbSqueezeInteractor.cs`
- `Assets/Scripts/BloodPressure/ValveController.cs`
- `Assets/Scripts/BloodPressure/KorotkoffAudioEngine.cs`
- `Assets/Scripts/BloodPressure/StethoscopePlacementValidator.cs`
- `Assets/Scripts/BloodPressure/BPMeasurementAssessment.cs`
- `Assets/Scripts/Interaction/XRInteractionAdapter.cs`

**Will be modified**
- `Packages/manifest.json` — add OpenXR + XRI packages
- `ProjectSettings/ProjectSettings.asset` — bundle id, API level, IL2CPP, Vulkan
- `ProjectSettings/XRSettings.asset` (new XR Plug-in Management asset)
- `Assets/Scripts/UI/*` — respatialize canvases to world space

**Reused as-is (no edits)**
- `Assets/Scripts/Static/StatemachineConnector.cs` — state machine backbone
- `Assets/Scripts/Visual Scripting/Action.cs`, `Feedback.cs`, `Instructions.cs` — authoring nodes
- `Assets/Scripts/Interaction/TrainARObject.cs`, `InteractionController.cs` — interaction base classes
- `Documentation/manual/` — authoring / object-conversion guides

---

## Authoring patient cases

Patient cases are authored as `PatientProfile` ScriptableObject assets
(`Assets/Scripts/BloodPressure/PatientProfile.cs`):

1. In the Project window, right-click →
   **Create → TrainAR → BloodPressure → Patient Profile**.
2. Save the new asset under `Assets/ScriptableObjects/PatientProfiles/`
   (e.g. `Normotensive_Adult.asset`, `StageII_Hypertension.asset`,
   `AuscultatoryGap_Case.asset`).
3. Set `systolicMmHg`, `diastolicMmHg`, `heartRateBpm`,
   `armCircumferenceCm`, `toleranceMmHg`, and the optional
   `auscultatoryGap*` fields. `OnValidate` enforces
   `diastolic < systolic` and a sane gap window.
4. Reference the active profile from the scene's BP controller (added in
   step 4 of this plan) so every simulation script reads ground truth
   from the same asset.

Two helper methods are exposed for the simulation layer:

- `PatientProfile.IsKorotkoffAudibleAt(pressureMmHg)` — used by
  `KorotkoffAudioEngine` to decide whether the cuff pressure currently
  sits inside the audible window (accounting for auscultatory gaps).
- `PatientProfile.IsReadingCorrect(learnerSystolic, learnerDiastolic)` —
  used by `BPMeasurementAssessment` to score the learner's final entry
  within `±toleranceMmHg`.

---

## Verification

- **Editor smoke test:** Open `Assets/Scenes/BloodPressureTraining.unity`, press Play with the XR Device Simulator, walk the full stateflow end-to-end. No console errors, every state transition fires.
- **Device smoke test:** Build Android (OpenXR), sideload to Galaxy XR, run the checklist in step 7.3.
- **Authoring regression:** Open `Assets/Scene.unity` (the original TrainAR authoring scene) and confirm the existing coffee-machine example still builds and runs in-Editor — guarantees we did not break the base framework.
- **Pedagogical validation:** Have at least one clinical SME walk the full BP flow and confirm the 2–3 mmHg/s deflation gating, Korotkoff phase transitions, and ±4 mmHg scoring match standard teaching.
