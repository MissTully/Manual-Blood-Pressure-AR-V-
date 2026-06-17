# GitHub Project Setup — Feature Release Mapping

This document maps the repository's implementation plan to the GitHub Project feature-release workflow referenced by the Encountive project view.

Project reference:
- Organization project view using `system_template=feature_release`

Repository reference:
- `MissTully/Manual-Blood-Pressure-AR-V-`

Planning sources:
- `docs/project-charter.md`
- `docs/roadmap.md`
- `docs/implementation-backlog.md`
- `Documentation/BloodPressureTraining.md`
- `Documentation/GalaxyXRSetup.md`

## Purpose
Use the GitHub Project as the delivery control surface for the Manual Blood Pressure AR Trainer feature release.

The board should track:
- platform setup completion,
- XR integration,
- BP simulation implementation,
- training-flow delivery,
- QA/device validation,
- pilot release readiness.

## Suggested project configuration

### Status values
- Backlog
- Ready
- In Progress
- Blocked
- In Review
- QA / Device Test
- Done

### Priority values
- P0
- P1
- P2

### Workstream values
- Product
- Unity/XR
- Android
- 3D Content
- QA
- Release
- UX

### Phase values
- Setup
- Prototype
- MVP
- Validation
- Release

### Optional fields
- **Target Milestone**
  - M1 Platform Stabilization
  - M2 XR Interaction Baseline
  - M3 BP Simulation Systems
  - M4 End-to-End Scenario
  - M5 Device-Validated MVP
  - M6 Pilot Release Candidate

- **Risk Level**
  - High
  - Medium
  - Low

- **Device Required**
  - Yes
  - No

## Recommended epic structure
Create the following epic-level items in the project:
1. Platform stabilization for Galaxy XR build path
2. XR rig and interaction integration
3. Blood pressure content foundation
4. Blood pressure simulation systems
5. Training scenario implementation
6. UX and accessibility adaptation
7. Validation and pilot release readiness

## Recommended item import order

### First items to add
1. Complete Unity Editor XR Plug-in Management setup
2. Enable Android scripting define for headset path
3. Generate and commit XR configuration assets
4. Verify Android build configuration for Galaxy XR
5. Perform first device build smoke test
6. Author reusable XR rig prefab
7. Integrate `XRInteractionAdapter` with TrainAR interaction controller
8. Create `PatientProfile` data model
9. Implement cuff pressure simulation
10. Create dedicated blood pressure training scene

### Second-wave items
- Source/create BP assets
- Add BP audio asset set
- Implement valve control
- Implement Korotkoff audio engine
- Implement stethoscope placement validation
- Implement learner scoring
- Convert critical UI to world-space XR
- End-to-end editor validation
- End-to-end device validation

## Suggested board views

### 1. Feature release board
Primary kanban view grouped by **Status**.

### 2. Phase roadmap
Table or roadmap grouped by **Phase** and sorted by **Priority**.

### 3. Device-critical work
Filtered view where **Device Required = Yes**.

### 4. QA and validation
Filtered view for **Workstream = QA** or **Phase = Validation**.

### 5. High-risk items
Filtered view where **Risk Level = High**.

## Suggested milestone mapping

### M1 — Platform Stabilization
- Complete Unity Editor XR Plug-in Management setup
- Enable Android scripting define for headset path
- Generate and commit XR configuration assets
- Verify Android build configuration for Galaxy XR
- Perform first device build smoke test

### M2 — XR Interaction Baseline
- Author reusable XR rig prefab
- Configure right-hand XR controller interaction path
- Integrate `XRInteractionAdapter` with TrainAR interaction controller
- Validate XR interaction in editor simulator

### M3 — BP Simulation Systems
- Create `PatientProfile` data model
- Implement cuff pressure simulation
- Implement squeeze-based inflation interaction
- Implement valve-controlled deflation behavior
- Implement Korotkoff audio playback logic
- Implement stethoscope placement validation
- Implement learner reading capture and scoring

### M4 — End-to-End Scenario
- Create dedicated blood pressure training scene
- Implement onboarding and placement flow
- Implement cuff placement training step
- Implement palpation and stethoscope placement steps
- Implement inflate / deflate / mark reading steps
- Implement debrief and summary screen

### M5 — Device-Validated MVP
- Convert required instruction UI to world-space XR presentation
- Perform end-to-end editor validation
- Perform end-to-end Galaxy XR device validation
- Review procedure with clinical SME

### M6 — Pilot Release Candidate
- Prepare pilot release checklist
- Track defects and stabilization work
- Finalize documentation and demo readiness

## Suggested operating rules
- Treat `Documentation/BloodPressureTraining.md` as the technical implementation source of truth.
- Treat `Documentation/GalaxyXRSetup.md` as the platform setup source of truth.
- Keep planning docs in `docs/` focused on execution, prioritization, and project tracking.
- Mark any Unity Editor-only work with **Device Required** or equivalent notes when hardware/editor validation is mandatory.
- Split large technical items into smaller implementation tasks only after the milestone owner is clear.

## Definition of Done for project tracking
A work item should be marked **Done** only when:
- implementation is committed,
- scene/prefab/config changes are included if required,
- affected behavior is verified in editor or on device as appropriate,
- any follow-up validation notes are documented.

## Recommended next project operations
1. Add epic-level items to the feature-release project.
2. Add the first 10 implementation items from `docs/implementation-backlog.md`.
3. Assign Phase, Priority, Workstream, and Target Milestone fields.
4. Start with M1 Platform Stabilization and M2 XR Interaction Baseline before expanding the content backlog.
