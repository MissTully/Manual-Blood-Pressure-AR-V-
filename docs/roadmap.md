# Roadmap — Manual Blood Pressure AR Trainer

This roadmap is derived from the repository's existing planning and setup documents:
- `Documentation/BloodPressureTraining.md`
- `Documentation/GalaxyXRSetup.md`
- `docs/project-charter.md`

It translates the current implementation direction into phased project delivery for a Galaxy XR / Unity / Android build.

## Current baseline

The repository already contains:
- TrainAR-based Unity project structure
- Unity 2022.3.49f1 project configuration
- OpenXR, XR Management, XR Interaction Toolkit, XR Hands packages in `Packages/manifest.json`
- Galaxy XR setup notes documenting committed changes and remaining Unity Editor tasks
- A detailed blood pressure training implementation plan

Because of that, this roadmap starts from **platform adaptation and scenario implementation**, not initial discovery from zero.

## Phase 1 — Platform stabilization
**Goal:** Confirm the project runs as a headset-targeted XR app baseline.

### Outcomes
- Unity Editor resolves XR dependencies successfully
- Android OpenXR path is configured in the Unity Editor
- `TRAINAR_XR_HMD` define is enabled for Android builds
- Generated XR settings/assets are committed
- APK builds and installs on target device

### Key tasks
- Complete Unity Editor XR Plug-in Management setup
- Enable OpenXR on Android tab
- Configure interaction profiles and hand tracking
- Generate and commit `Assets/XR/**` and related XR settings assets
- Validate Vulkan, IL2CPP, ARM64, Linear color space, Single Pass Instanced rendering
- Perform first smoke test on Galaxy XR or supported stand-in device

### Exit criteria
- Android XR/OpenXR build completes without blocking errors
- Device launch reaches stereo XR view with tracked head/controller input

## Phase 2 — XR interaction integration
**Goal:** Replace handheld AR interaction assumptions with headset-based XR interaction.

### Outcomes
- XR rig prefab exists and is reusable
- TrainAR interaction flow works through XR controller or hand-based adapters
- Scene can run without depending on handheld AR session readiness

### Key tasks
- Author `Assets/Prefabs/BP_XRRig.prefab`
- Configure XR Origin and action-based controllers
- Add ray interactors and line visuals
- Wire `XRInteractionAdapter` to `InteractionController`
- Validate controller aim, select, and activate mappings
- Test with XR Device Simulator in the editor

### Exit criteria
- User can target, select, grab, and activate TrainAR objects in the BP flow using XR controls

## Phase 3 — Blood pressure domain foundation
**Goal:** Introduce the core domain assets and simulation systems for manual blood pressure training.

### Outcomes
- Blood pressure-specific models, audio, and patient profile structure are present
- Core pressure, valve, stethoscope, and assessment systems exist
- Simulation events can feed TrainAR instructions and feedback

### Key tasks
- Create `Assets/Models/BloodPressure/`
- Import/author cuff, gauge, bulb, tubing, patient arm, and stethoscope assets
- Add BP audio assets
- Create `PatientProfile` ScriptableObject pipeline
- Implement pressure simulation, valve control, Korotkoff audio, placement validation, and assessment scripts

### Exit criteria
- Pressure state, audible cue logic, and learner scoring work in isolation and can be exercised in a test scene

## Phase 4 — Training scenario implementation
**Goal:** Build the full manual blood pressure training experience.

### Outcomes
- Dedicated blood pressure training scene exists
- Procedural flow is represented in TrainAR-compatible stateflow
- Learners can complete the training start to finish

### Key tasks
- Create `Assets/Scenes/BloodPressureTraining.unity`
- Implement onboarding and room placement flow
- Implement cuff placement, pulse palpation, stethoscope placement, inflate/deflate, systolic/diastolic marking, and summary flow
- Connect UI/instruction/feedback components to state transitions
- Create world-space instructional UI where needed

### Exit criteria
- Complete end-to-end BP scenario runs in-editor and on device

## Phase 5 — Validation and optimization
**Goal:** Make the app stable, comfortable, and instructionally valid.

### Outcomes
- Performance is acceptable on Galaxy XR hardware
- Interaction comfort and accessibility gaps are addressed
- Clinical procedure logic is reviewed against expected workflow

### Key tasks
- Device QA on headset
- Validate frame stability and interaction responsiveness
- Tune deflation-rate guidance and timing windows
- Add seated/standing options, captions, and fallback interaction modes where feasible
- Conduct clinical SME review of the training sequence

### Exit criteria
- MVP build is functionally stable and instructionally credible for pilot use

## Phase 6 — Pilot release readiness
**Goal:** Prepare the project for coordinated tracking, testing, and demonstration.

### Outcomes
- Release checklist exists
- Known risks are tracked
- GitHub Project work items are aligned to the implementation plan
- Documentation is sufficient for ongoing team execution

### Key tasks
- Finalize backlog and project field mapping
- Create test checklist and defect triage process
- Prepare pilot/demo build notes
- Document install and deployment steps

### Exit criteria
- Team can manage the work as a feature-release project and produce pilot-ready builds

## Risks and dependencies

### Dependencies
- Access to Galaxy XR hardware or a reliable stand-in XR device
- Unity Editor completion of XR settings generation
- Availability of BP-specific models, audio, and clinical review input

### Risks
- Editor-generated XR asset setup may drift from documented configuration
- Existing TrainAR handheld AR assumptions may surface in additional scripts/scenes
- XR interaction and world-space UI may require iterative usability tuning
- Clinical correctness for BP simulation and feedback may require SME validation

## Suggested milestone mapping
- **Milestone 1:** Platform stabilization complete
- **Milestone 2:** XR interaction baseline complete
- **Milestone 3:** BP simulation systems complete
- **Milestone 4:** End-to-end scenario playable
- **Milestone 5:** Device-validated MVP
- **Milestone 6:** Pilot-ready release candidate
