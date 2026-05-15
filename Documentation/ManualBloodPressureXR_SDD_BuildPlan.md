# Build Plan — Manual Blood Pressure XR Module (SDD v0.3, Phase 1)

Derived from `ENC-MBPXR-SDD-v0.1` (Manual Blood Pressure XR Module Software
Design Document, working draft v0.3). This plan turns the SDD into an
ordered, buildable Phase 1 work breakdown for **this** Unity/TrainAR
repository running on **Samsung Galaxy XR**.

It supersedes the scope of the earlier `Documentation/BloodPressureTraining.md`
plan (a single flat auscultatory procedure). That earlier work is **not
discarded** — Section 9 below maps every existing
`Assets/Scripts/BloodPressure/*` script onto its SDD home.

---

## 1. What Phase 1 Is (and Is Not)

Per SDD §17.3 (ADR-XR-008), Phase 1 is the **Deterministic** release:

- All six instructional stations exist with **hand-authored** content.
- Safety gates are **deterministic, rule-based, pure C#** — never AI.
- Coach utterances come from a **fixed local fallback library**.
- ElevenLabs voice is optional; pre-cached clips only.
- xAPI events are emitted and **queue locally** when offline.
- **No AI surface in the runtime.** No Gemini, no live Façade generation.
  The Trigger API is a **stub** that returns hand-authored utterances.

Out of scope for Phase 1 (deferred to Phases 2–4): Gemini coach,
generated case narratives, XReal Aura HAL impl, WebXR build, cohort
dashboards, the live Façade Service. We build the **seams** for these
(interfaces, stub Façade contract) but not the implementations.

**First vertical slice:** the **Cuff Size Selection sub-scene
(Station 2)** — SDD Part B. It is the prerequisite gate into the module
and the most fully specified blueprint. Stations 1, 3–6 are scaffolded
to the shared state-machine vocabulary but only Station 2 is built to
full rubric depth in Phase 1.

---

## 2. Target Architecture (SDD §3, §4, §6, §8)

Six layers, mirroring the SDD Logical View. Per-station code calls only
the abstract interfaces; nothing in a station references a device SDK or
a network call directly.

```
Encountive/Scripts/
  Core/        SessionContext, ModeEngine, ModuleStateMachine,
               TriggerDispatcher client, XapiEmitter (+offline queue)
  Hal/         IXrInputProvider, ISpatialAudioService, ICapabilityProbe
               GalaxyXR/  (Phase 1 impl)   AuraXR/  WebXR/  (stubs)
  SafetyGates/ ISafetyGateEngine — pure C#, NO UnityEngine using,
               100% unit-test coverage (hard quality gate, SDD §16.1)
  Triggers/    TriggerEvent types, TriggerBinding ScriptableObject,
               local hand-authored fallback library, ITriggerClient
  Stations/
    Station1..6/  per-station deterministic FSM + rubric scorer
  Tests/       EditMode (gate + rubric) + PlayMode (state machine)
Encountive/Prefabs/      XR Origin per target, cuff/tape/tray prefabs
Encountive/Content/      Personas/  Korotkoff/  Audio/PreCached/
```

### 2.1 The HAL C# interface set (SDD §7.4 — adopt verbatim)

```csharp
public interface IXrInputProvider   { /* pinch, drag, tap, gaze, normalized */ }
public interface ISpatialAudioService { void Play(string clipId, Vector3 worldPos, AudioMixerChannel ch); }
public interface ICapabilityProbe    { CapabilityReport ReadOnce(); }
public interface ITriggerClient      { Task<CoachUtterance> Fire(TriggerEvent ev); }
public interface IXapiEmitter        { void Emit(XApiStatement s); Task FlushAsync(); }
public interface ISafetyGateEngine   { GateResolution Evaluate(GateInput input); }
```

`ITriggerClient` Phase 1 implementation = `LocalFallbackTriggerClient`
(reads the hand-authored library, never touches the network). The
**HTTPS contract** to the future Façade (`POST /v1/xr/trigger`,
`POST /v1/xr/xapi[/batch]`, schemas in SDD §7.2–7.3) is recorded as a
data-class spec now so Phase 2 swaps the implementation without touching
station code.

### 2.2 Shared state-machine vocabulary (SDD §8.1)

Every station FSM uses the same state types so cross-station tooling is
uniform: `EntryState → StageState* → DecisionPointState →
SafetyGateState → DebriefState → ExitState`. The module-wide FSM
(SDD §8.2) sits above them and, in Full Encounter mode, binds one
persona at session start and propagates it to all six stations.

This **replaces** the existing flat `BPStage` enum (Section 9).

### 2.3 Mode engine (SDD §2.3, §8.3)

`Guided | Practice | Evaluation | FullEncounter`. Mode is chosen at
session start, immutable within a case, propagated to every trigger and
stamped on every xAPI statement. It drives overlay visibility,
coach-intervention threshold, case selection, and debrief depth per the
SDD §8.3 table.

---

## 3. Phase 1 Work Breakdown (ordered)

Each item is a feature branch / commit on `claude/plan-build-Sh05Q`,
sequenced so every step compiles and is testable on its own.

### WP-0 — Project & platform baseline (SDD §2.5.1, §3.3)
- Confirm Unity 6 LTS + Android Build Support + OpenXR + Android XR
  provider per `Documentation/GalaxyXRSetup.md` (already started:
  commit `be6fbfe`). Verify the OpenXR loader shows "Android XR".
- Create the `Encountive/` folder tree from Section 2. Add an assembly
  definition per top-level folder so `SafetyGates` can be **pure C#**
  with **no** `UnityEngine` reference (required for headless 100%-
  coverage unit tests).

### WP-1 — Core session + data models (SDD §5.1)
- Implement runtime models: `SessionContext`, `StationState`,
  `Persona`, `CaseParameters`, `TriggerEvent`, `CoachUtterance`,
  `SafetyGateFire`, `XApiStatement` (plain serializable C#).
- `SessionContext` is created once at session start and immutable.

### WP-2 — Deterministic Safety Gate Engine (SDD §8.5, §13.3, §16.1)
**Highest-priority correctness component.** Pure C#, no Unity deps.
- `ISafetyGateEngine.Evaluate(GateInput) → {pass | fire | escalate}`.
- Implement the 8 Cuff Size Selection gates **CSS-SG-1..8** exactly per
  SDD §13.3, including the AHA 80%/40% rule check (CSS-SG-5) and
  boundary-mishandled (CSS-SG-7).
- Encode adult branches **B-1..B-5** and pediatric **B-6..B-9**
  (SDD §11.2) as a deterministic decision table keyed by MUAC band +
  persona age band.
- **100% unit-test coverage** over every (gate, persona, mode) and
  every MUAC off-by-one boundary (SDD §16.1) — this is a hard quality
  gate, build fails below it.

### WP-3 — HAL: capability probe + Galaxy XR input (SDD §4.5–4.8)
- `ICapabilityProbe.ReadOnce()` queries the OpenXR runtime once at
  session start, builds a `CapabilityReport`, and the report is written
  to the audit log (Phase 1: local JSON; Phase 2: Façade).
- `GalaxyXR` impl of `IXrInputProvider` (hand pinch/drag/tap/gaze via
  OpenXR Hand Tracking + Eye Gaze Interaction) and `ISpatialAudioService`
  (HMD binaural).
- Interaction-profile fallback chain per SDD §4.5 table; `AuraXR` and
  `WebXR` folders get **interface stubs only** (compile, throw
  `NotSupported`) so Phase 2 has a home.
- Reuse the existing `XRInteractionAdapter.cs` (commit `4bb3d11`) as the
  Galaxy XR `IXrInputProvider` backing — wrap it, do not rewrite.

### WP-4 — Trigger subsystem, local fallback (SDD §8.4, §13, §7.2)
- `TriggerEvent` payload builder (abstracted state only — no raw
  biometrics, SDD §2.5.2).
- `TriggerBinding` ScriptableObject: trigger id + firing `Predicate` +
  prompt-template id + post-fire transition (SDD §13.5). This is the
  canonical "which trigger fires when" registry.
- Hand-authored fallback library for all Cuff Size Selection triggers:
  scenario framing **CSS-SF-1..5**, decision points **CSS-DP-1..3**,
  safety-gate redirects **CSS-SG-1..8**, debrief **CSS-DB-1**, with the
  word budgets in SDD §13 and the sample utterances in SDD Appendix F as
  the starting copy.
- `LocalFallbackTriggerClient : ITriggerClient` — returns library text +
  `source="authored"`, optional pre-cached `voicePlayback`.

### WP-5 — xAPI emitter + offline queue (SDD §5.2, §3.2.2, §7.3)
- `XApiEmitter` builds xAPI 1.0.3 statements (verbs under
  `https://xapi.encountive.com/...`, sample in SDD Appendix D).
- Emit on every FSM transition, gate fire, utterance played, rubric
  score. **Append-only, idempotent on statement id.**
- Offline: queue to local durable store, flush on reconnect. Phase 1
  "Façade" is a local sink writing the same JSON shape the Phase 2
  Supabase endpoint will accept.

### WP-6 — Station 2 state machine: Cuff Size Selection (SDD §11)
- FSM stages **S1..S5** per SDD §11.1: identity/consent → landmark id →
  MUAC measurement → cuff-class commit → confirmation/rationale.
- Decision points wired: **CSS-DP-1** before commit, **CSS-DP-2** after
  commit, **CSS-DP-3** on boundary detection (Persona C / P-F).
- Boundary detection (SDD §11.3) is deterministic, fired **before**
  learner commit at S4.
- Interaction mapping per SDD §15.4 (stylus place, two-hand tape drag,
  pinch cuff from tray, gaze read label).

### WP-7 — Personas & deterministic case sampler (SDD §12)
- Adult personas **A, B (Margarita Delgado), C**; pediatric **P-A..P-H**
  with MUAC + age band + dialogue JSON (SDD §12.1–12.2). Asset bundle
  layout per SDD §5.5 / §12.4 under `Encountive/Content/Personas/`.
- `CaseParameterSampler`: deterministic, keyed by
  `(caseId, parameterSetVersion)` → identical params every time
  (assessment fairness, SDD §12.3). Phase 1 = sampler only, **no**
  generative wrapper.
- Migrate the 3 existing `Assets/ScriptableObjects/PatientProfiles/*`
  assets into this persona model (Section 9).

### WP-8 — Rubric & mastery scoring (SDD §14)
- Per-criterion 0–4 scorer for the 7 Cuff Size Selection criteria
  (SDD §14.1); mastery threshold ≥ 3 on every criterion.
- Emit one `xr_rubric_scores`-shaped record per criterion at S5, with
  the `evidence` field carrying the contributing xAPI statement ids /
  gate ids / DP capture text (SDD §14.2).
- Per-station mastery rule: 3 consecutive clean Evaluation-mode passes,
  no critical-error trigger, no SG-6 bias pattern (SDD §14.3). Cross-
  station / full-encounter / persistent mastery: stub the data shape,
  defer logic to Phase 4.

### WP-9 — Station scaffolds 1, 3, 4, 5, 6 (SDD §2.2)
- Each gets an FSM in the shared vocabulary + a hand-authored trigger
  set + entry/exit + xAPI, but only enough depth to chain a **Full
  Encounter** end-to-end on Galaxy XR. Station 4/5 reuse the existing
  simulation scripts (Section 9). Full rubric depth for these is a
  later phase.
- Module-wide FSM (SDD §8.2): single-station routing for
  Guided/Practice/Evaluation; persona-locked chaining for Full Encounter.

### WP-10 — Scene, prefabs, accessibility (SDD §9.5, §15)
- `Encountive/Prefabs/` XR Origin (Galaxy XR), cuff tray, virtual tape,
  stylus, manometer, persona prefabs.
- World-space coach/caption canvas (re-spatialize, per the older plan
  §2.4).
- Accessibility (SDD §9.5): closed captions on every utterance,
  high-contrast labels, colour-blind-safe glow zones, comfort-mode
  toggle, configurable pre-intervention wait time.

### WP-11 — Test strategy execution (SDD §16)
- EditMode: Safety-gate 100% coverage (WP-2 gate); rubric scoring.
- PlayMode: every Station 2 FSM transition with a deterministic Façade
  mock returning fixed utterances (SDD §16.2).
- Trigger coverage: ≥1 positive + ≥1 near-miss negative per trigger id
  (SDD §16.3).
- Persona coverage: every A/B/C and P-A..P-H through happy path + ≥1
  gate path; C and P-F additionally exercise DP-3 + SG-7 (SDD §16.4).
- **Replay determinism** (SDD §16.6): load saved session JSON, replay
  through the deterministic FSM, assert gate fires + rubric scores +
  xAPI sequence match recorded values exactly. Canonical regression
  guard — extend the existing `BPSmokeTest.cs` harness for this.
- Galaxy XR hardware smoke: Persona Adult A, Guided mode, happy path,
  expected xAPI sequence (SDD §4.10, §16.5).

---

## 4. Critical Files

**Created (Phase 1)**
- `Assets/Encountive/Scripts/SafetyGates/ISafetyGateEngine.cs`
  + `SafetyGateEngine.cs` (pure C#, asmdef with no UnityEngine ref)
- `Assets/Encountive/Scripts/SafetyGates/CuffSizeRules.cs` (B-1..B-9,
  AHA 80%/40%)
- `Assets/Encountive/Scripts/Core/{SessionContext,ModeEngine,ModuleStateMachine,XapiEmitter}.cs`
- `Assets/Encountive/Scripts/Hal/{IXrInputProvider,ICapabilityProbe,ISpatialAudioService}.cs`
  + `Hal/GalaxyXR/*`
- `Assets/Encountive/Scripts/Triggers/{TriggerEvent,TriggerBinding,LocalFallbackTriggerClient}.cs`
  + fallback library asset
- `Assets/Encountive/Scripts/Stations/Station2/CuffSizeSelectionStateMachine.cs`
  + `Station2/CuffSizeSelectionRubric.cs`
- `Assets/Encountive/Content/Personas/*` (A, B, C, P-A..P-H)
- `Assets/Encountive/Scripts/Tests/*` (EditMode + PlayMode + replay)

**Modified / migrated**
- `Packages/manifest.json`, `ProjectSettings/*` — OpenXR/Android XR
  (continue WP-0 from commit `be6fbfe`)
- `Assets/Scripts/BloodPressure/*` → wrapped as Station 4/5 components
  (Section 9), not deleted
- `Assets/Scripts/Interaction/XRInteractionAdapter.cs` → backs the
  Galaxy XR `IXrInputProvider`

**Reused as-is**
- TrainAR `StatemachineConnector.cs`, visual-scripting Feedback/
  Instructions nodes (drive coach/caption surfaces)
- `BPSmokeTest.cs` harness → extended into the replay-determinism guard

---

## 5. Reconciliation With Existing Code (Section 9)

| Existing script | SDD home in Phase 1 |
|---|---|
| `CuffPressureSimulator.cs` | Station 4 inflation sim component |
| `BulbSqueezeInteractor.cs` | Station 4 input → HAL-mediated |
| `ValveController.cs` | Station 4 deflation-rate sim |
| `KorotkoffAudioEngine.cs` | Station 5 continuous-perception core |
| `StethoscopePlacementValidator.cs` | Station 4/5 placement gate |
| `BPMeasurementAssessment.cs` | folds into the rubric scorer (WP-8) |
| `BPTrainingController.cs` (`BPStage` enum) | **replaced** by the module-wide + per-station FSM (WP-6/WP-9); logic preserved as Station 4/5 stage flow |
| `PatientProfile` + 3 `.asset`s | migrated into `Persona`/`CaseParameters` (WP-7) |
| `BPSmokeTest.cs` | becomes the replay-determinism regression harness (WP-11) |

The flat 10-stage `BPStage` controller collapses Stations 3–6 into one
linear flow. The SDD requires six independently-practiceable stations
sharing one state vocabulary with a mode engine on top — so the
orchestrator is restructured, but every simulation behaviour it wired up
is retained inside the new station FSMs.

---

## 6. Sequencing & Dependencies

```
WP-0 ─┬─ WP-1 ─┬─ WP-2 (gate engine, critical)
      │        ├─ WP-4 (triggers) ─┐
      │        ├─ WP-5 (xAPI)      ├─ WP-6 (Station 2 FSM) ─ WP-8 (rubric)
      ├─ WP-3 (HAL/Galaxy XR) ─────┘            │
      │                                          └─ WP-9 (other stations)
      └────────────────────────── WP-7 (personas) ┘
                                   WP-10 (scene/a11y) ─ WP-11 (tests/verify)
```

WP-2 (deterministic safety-gate engine) is the correctness keystone and
should land first and stay green; everything clinical depends on it.

---

## 7. Phase 1 Done = Acceptance (SDD §16, §17.3)

- Cuff Size Selection runs end-to-end on Galaxy XR in Guided, Practice,
  and Evaluation modes; Full Encounter chains all six stations with one
  persona.
- Safety-gate engine at 100% unit coverage; all 8 CSS gates correct on
  every persona/mode/MUAC boundary.
- Every Cuff Size Selection trigger has positive + negative tests.
- Replay-determinism test passes from saved session JSON.
- xAPI emits offline and flushes on reconnect with no data hole.
- No AI surface in the runtime (ADR-XR-008 satisfied).

---

## 8. Open Decisions That Block Phase 1 (SDD §19.1)

These need a Founder/CTO answer before or during the build and are
flagged here so they are not silently assumed:

- **HAL interface set finalization** — adopt SDD §7.4 verbatim unless an
  objection is raised during this build (default: adopt).
- **Persona library scope** — add Adult A/B/C + P-A..P-H to the
  canonical Encountive persona library for cross-module reuse.
- **Cohort analytics MVP in Phase 1** — default recommendation is a
  minimal instructor view; confirm before Phase 1 freeze.
- **Pediatric Korotkoff** — deferred; pediatric handled in Cuff Size
  Selection only (does not block Station 2).

---

*End of Phase 1 build plan. Source of authority: SDD v0.3 Parts A–C;
clinical content authority: Cuff Size Selection Blueprint v0.2.*
