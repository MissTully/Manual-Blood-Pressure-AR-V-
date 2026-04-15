using System;
using System.Reflection;
using UnityEngine;

namespace BloodPressure
{
    /// <summary>
    /// Runtime smoke tests for the six BP simulation scripts. Attach this
    /// MonoBehaviour to an empty GameObject in any scene and right-click
    /// the component header in the Inspector to invoke individual tests
    /// via the <see cref="ContextMenu"/> entries. Each test builds its
    /// own temporary objects, logs PASS/FAIL with a <c>[BPSmoke]</c>
    /// prefix, and cleans up after itself.
    ///
    /// Designed so it compiles against Unity 2022.3 with only the six
    /// existing BP scripts and <c>UnityEngine</c> — no XRI, no AR
    /// Foundation, no test framework.
    /// </summary>
    public class BPSmokeTest : MonoBehaviour
    {
        private const string LogPrefix = "[BPSmoke]";

        [Header("Optional pre-wired references")]
        [Tooltip("Optional: if assigned, tests that need a cuff will use this one instead of constructing a fresh one. Currently informational — each ContextMenu test constructs its own temp instance.")]
        public CuffPressureSimulator cuff;

        [Tooltip("Optional: pre-wired valve controller.")]
        public ValveController valve;

        [Tooltip("Optional: pre-wired Korotkoff audio engine.")]
        public KorotkoffAudioEngine korotkoffEngine;

        [Tooltip("Optional: pre-wired assessment.")]
        public BPMeasurementAssessment assessment;

        [Tooltip("Optional: pre-wired training controller.")]
        public BPTrainingController trainingController;

        // ------------------------------------------------------------
        // Reflection helpers — the BP scripts serialize most of their
        // dependencies as private fields, so the only way to wire a
        // fresh in-code test environment without modifying those
        // scripts is to poke the fields directly.
        // ------------------------------------------------------------

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            var type = target.GetType();
            FieldInfo field = null;
            while (type != null && field == null)
            {
                field = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
                type = type.BaseType;
            }
            if (field == null)
            {
                throw new InvalidOperationException(
                    $"Field '{fieldName}' not found on {target.GetType().Name}");
            }
            field.SetValue(target, value);
        }

        private static void SafeDestroy(UnityEngine.Object obj)
        {
            if (obj == null) return;
            // ContextMenu entries can run at edit-time as well as play-time.
            // DestroyImmediate is the only safe call in edit-mode.
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }

        private static void LogPass(string testName, string detail = null)
        {
            Debug.Log($"{LogPrefix} PASS: {testName}" + (string.IsNullOrEmpty(detail) ? "" : $" — {detail}"));
        }

        private static void LogFail(string testName, string detail)
        {
            Debug.LogError($"{LogPrefix} FAIL: {testName} — {detail}");
        }

        // ============================================================
        // Test 1: Cuff inflate / deflate / clamping
        // ============================================================
        [ContextMenu("BP Smoke: Cuff Inflate/Deflate Clamping")]
        public void TestCuffInflateDeflateClamping()
        {
            const string name = "Cuff Inflate/Deflate Clamping";
            GameObject go = null;
            try
            {
                go = new GameObject("BPSmoke_Cuff");
                var c = go.AddComponent<CuffPressureSimulator>();

                c.Inflate(50f);
                if (!Mathf.Approximately(c.CurrentPressureMmHg, 50f))
                {
                    LogFail(name, $"after Inflate(50), expected 50, got {c.CurrentPressureMmHg}");
                    return;
                }

                // 50 + 500 = 550, must clamp to 300.
                c.Inflate(500f);
                if (!Mathf.Approximately(c.CurrentPressureMmHg, 300f))
                {
                    LogFail(name, $"after Inflate(500), expected clamp to 300, got {c.CurrentPressureMmHg}");
                    return;
                }

                c.Deflate(100f);
                if (!Mathf.Approximately(c.CurrentPressureMmHg, 200f))
                {
                    LogFail(name, $"after Deflate(100), expected 200, got {c.CurrentPressureMmHg}");
                    return;
                }

                // Deflate past zero — must clamp to 0, not go negative.
                c.Deflate(9999f);
                if (!Mathf.Approximately(c.CurrentPressureMmHg, 0f))
                {
                    LogFail(name, $"after large Deflate, expected clamp to 0, got {c.CurrentPressureMmHg}");
                    return;
                }

                LogPass(name);
            }
            catch (Exception e)
            {
                LogFail(name, $"exception: {e}");
            }
            finally
            {
                SafeDestroy(go);
            }
        }

        // ============================================================
        // Test 2: PatientProfile audibility window (+ auscultatory gap)
        // ============================================================
        [ContextMenu("BP Smoke: PatientProfile Audibility Window")]
        public void TestPatientProfileAudibility()
        {
            const string name = "PatientProfile Audibility Window";
            PatientProfile profile = null;
            try
            {
                profile = ScriptableObject.CreateInstance<PatientProfile>();
                profile.systolicMmHg = 120;
                profile.diastolicMmHg = 80;
                profile.heartRateBpm = 72;
                profile.auscultatoryGap = false;

                // Inside window.
                if (!profile.IsKorotkoffAudibleAt(100f)) { LogFail(name, "100 mmHg should be audible"); return; }
                if (!profile.IsKorotkoffAudibleAt(120f)) { LogFail(name, "120 mmHg (systolic edge) should be audible"); return; }
                if (!profile.IsKorotkoffAudibleAt(80f))  { LogFail(name, "80 mmHg (diastolic edge) should be audible"); return; }

                // Outside window.
                if (profile.IsKorotkoffAudibleAt(60f))  { LogFail(name, "60 mmHg should NOT be audible"); return; }
                if (profile.IsKorotkoffAudibleAt(140f)) { LogFail(name, "140 mmHg should NOT be audible"); return; }

                // Now flip on the auscultatory gap. The profile's OnValidate
                // only clamps lower>upper, so we must assign upper first then
                // lower to stay consistent with the [140, 150] window the
                // caller requested (gap lives above systolic in this demo,
                // which still exercises the code path — the gap check runs
                // unconditionally before the systolic bound kicks in).
                // We want a gap with pressures inside the [diastolic, systolic]
                // window so the gap actually has bite: use [85, 95] instead
                // and re-verify.
                profile.auscultatoryGap = true;
                profile.auscultatoryGapUpperMmHg = 95;
                profile.auscultatoryGapLowerMmHg = 85;

                if (profile.IsKorotkoffAudibleAt(90f))
                {
                    LogFail(name, "90 mmHg inside gap [85,95] should NOT be audible");
                    return;
                }
                if (!profile.IsKorotkoffAudibleAt(100f))
                {
                    LogFail(name, "100 mmHg outside gap should still be audible");
                    return;
                }

                // Spec also asked for a [140, 150] gap + 145 check. Pressure
                // 145 sits above systolic=120 so it is already inaudible
                // regardless of the gap, but verify explicitly.
                profile.auscultatoryGapUpperMmHg = 150;
                profile.auscultatoryGapLowerMmHg = 140;
                if (profile.IsKorotkoffAudibleAt(145f))
                {
                    LogFail(name, "145 mmHg should NOT be audible (above systolic, and inside gap)");
                    return;
                }

                LogPass(name);
            }
            catch (Exception e)
            {
                LogFail(name, $"exception: {e}");
            }
            finally
            {
                SafeDestroy(profile);
            }
        }

        // ============================================================
        // Test 3: Assessment scoring (correct + incorrect path)
        // ============================================================
        [ContextMenu("BP Smoke: Assessment Scoring")]
        public void TestAssessmentScoring()
        {
            const string name = "Assessment Scoring";
            GameObject cuffGo = null;
            GameObject assessGo = null;
            PatientProfile profile = null;
            try
            {
                profile = ScriptableObject.CreateInstance<PatientProfile>();
                profile.systolicMmHg = 120;
                profile.diastolicMmHg = 80;
                profile.toleranceMmHg = 4;

                cuffGo = new GameObject("BPSmoke_Cuff");
                var c = cuffGo.AddComponent<CuffPressureSimulator>();

                assessGo = new GameObject("BPSmoke_Assessment");
                var a = assessGo.AddComponent<BPMeasurementAssessment>();

                // SetPatientProfile is public; the cuff field is private so
                // we wire it via reflection.
                a.SetPatientProfile(profile);
                SetPrivateField(a, "cuff", c);

                bool correctFired = false;
                bool incorrectFired = false;
                a.OnCorrect.AddListener(() => correctFired = true);
                a.OnIncorrect.AddListener(() => incorrectFired = true);

                // --- Correct run: 118 / 79 with tolerance 4 => correct. ---
                c.SetPressure(118f);
                a.MarkSystolic();
                c.SetPressure(79f);
                a.MarkDiastolic();
                a.Submit();

                if (!correctFired)
                {
                    LogFail(name, "expected OnCorrect to fire for 118/79 against 120/80 ±4");
                    return;
                }
                if (incorrectFired)
                {
                    LogFail(name, "OnIncorrect fired unexpectedly on correct run");
                    return;
                }

                // --- Incorrect run: 110 / 70 exceeds tolerance. ---
                correctFired = false;
                incorrectFired = false;
                a.Reset();

                c.SetPressure(110f);
                a.MarkSystolic();
                c.SetPressure(70f);
                a.MarkDiastolic();
                a.Submit();

                if (!incorrectFired)
                {
                    LogFail(name, "expected OnIncorrect to fire for 110/70 against 120/80 ±4");
                    return;
                }
                if (correctFired)
                {
                    LogFail(name, "OnCorrect fired unexpectedly on incorrect run");
                    return;
                }

                LogPass(name);
            }
            catch (Exception e)
            {
                LogFail(name, $"exception: {e}");
            }
            finally
            {
                SafeDestroy(cuffGo);
                SafeDestroy(assessGo);
                SafeDestroy(profile);
            }
        }

        // ============================================================
        // Test 4: Valve rate -> InCorrectWindowChanged event
        // ============================================================
        // NOTE: ValveController.Update() reads its control value from the
        // XR InputAction which only exists under the TRAINAR_XR_HMD define.
        // When that define is off, ReadControlValue() returns 0 and
        // ApplyControlValue() calls SetRate(0f) every frame, which would
        // immediately overwrite any SetRate we push from this test.
        //
        // To work around that we DISABLE the ValveController component
        // after construction (so Update never runs) and drive SetRate
        // directly. This verifies the window-transition event firing but
        // not the per-frame Update deflation path.
        [ContextMenu("BP Smoke: Valve Rate In-Window Event")]
        public void TestValveRateInWindowEvent()
        {
            const string name = "Valve Rate In-Window Event";
            GameObject cuffGo = null;
            GameObject valveGo = null;
            try
            {
                cuffGo = new GameObject("BPSmoke_Cuff");
                var c = cuffGo.AddComponent<CuffPressureSimulator>();
                c.SetPressure(200f);

                valveGo = new GameObject("BPSmoke_Valve");
                var v = valveGo.AddComponent<ValveController>();
                // Stop Update() from overwriting SetRate every frame.
                v.enabled = false;

                SetPrivateField(v, "cuff", c);

                int trueEvents = 0;
                int falseEvents = 0;
                v.InCorrectWindowChanged.AddListener(inWindow =>
                {
                    if (inWindow) trueEvents++;
                    else falseEvents++;
                });

                // Default window is [2, 3] mmHg/s.
                v.SetRate(5f);   // out  — starts at false, no transition yet (lastInWindow was false)
                v.SetRate(2.5f); // in   — transition to true (+1 trueEvents)
                v.SetRate(1f);   // out  — transition to false (+1 falseEvents)

                if (trueEvents != 1)
                {
                    LogFail(name, $"expected 1 true transition, got {trueEvents}");
                    return;
                }
                if (falseEvents != 1)
                {
                    LogFail(name, $"expected 1 false transition, got {falseEvents}");
                    return;
                }

                // Cuff pressure should still be 200: SetRate alone does not
                // tick deflation; Update() drives the Deflate call and we
                // disabled the component.
                if (!Mathf.Approximately(c.CurrentPressureMmHg, 200f))
                {
                    LogFail(name, $"cuff pressure drifted from 200 (SetRate shouldn't deflate by itself); got {c.CurrentPressureMmHg}");
                    return;
                }

                LogPass(name, "rate event transitions verified; deflation Update path not exercised (TRAINAR_XR_HMD off)");
            }
            catch (Exception e)
            {
                LogFail(name, $"exception: {e}");
            }
            finally
            {
                SafeDestroy(cuffGo);
                SafeDestroy(valveGo);
            }
        }

        // ============================================================
        // Test 5: Full case orchestrator end-to-end stage progression
        // ============================================================
        [ContextMenu("BP Smoke: Full Case Orchestrator")]
        public void TestFullCaseOrchestrator()
        {
            const string name = "Full Case Orchestrator";
            GameObject cuffGo = null;
            GameObject valveGo = null;
            GameObject korotkoffGo = null;
            GameObject assessGo = null;
            GameObject ctrlGo = null;
            PatientProfile profile = null;
            try
            {
                profile = ScriptableObject.CreateInstance<PatientProfile>();
                profile.systolicMmHg = 120;
                profile.diastolicMmHg = 80;
                profile.toleranceMmHg = 4;
                profile.heartRateBpm = 72;

                cuffGo = new GameObject("BPSmoke_Cuff");
                var c = cuffGo.AddComponent<CuffPressureSimulator>();

                valveGo = new GameObject("BPSmoke_Valve");
                var v = valveGo.AddComponent<ValveController>();
                v.enabled = false; // avoid Update clobbering rate
                SetPrivateField(v, "cuff", c);

                korotkoffGo = new GameObject("BPSmoke_Korotkoff");
                var k = korotkoffGo.AddComponent<KorotkoffAudioEngine>();
                // Korotkoff engine requires no AudioSource / phaseClips to
                // simply wire up — its Update early-outs when those are
                // null. We set the cuff field so OnEnable can subscribe.
                SetPrivateField(k, "cuff", c);
                SetPrivateField(k, "patientProfile", profile);

                assessGo = new GameObject("BPSmoke_Assessment");
                var a = assessGo.AddComponent<BPMeasurementAssessment>();
                a.SetPatientProfile(profile);
                SetPrivateField(a, "cuff", c);

                ctrlGo = new GameObject("BPSmoke_Controller");
                var ctrl = ctrlGo.AddComponent<BPTrainingController>();
                // Wire the controller to every simulation component via
                // reflection. stethoscopeValidator is left null — the
                // controller tolerates a null validator at enable time
                // and we do not test the PlaceStethoscope auto-advance
                // path here; we manually Advance() through it instead.
                SetPrivateField(ctrl, "cuff", c);
                SetPrivateField(ctrl, "valve", v);
                SetPrivateField(ctrl, "korotkoffEngine", k);
                SetPrivateField(ctrl, "assessment", a);

                // OnEnable already ran when the component was added, but
                // at that point the private fields were still null, so
                // the event subscriptions inside OnEnable did nothing.
                // Toggle enabled to re-run the subscription pass.
                ctrl.enabled = false;
                ctrl.enabled = true;

                ctrl.StartCase(profile);

                if (ctrl.CurrentStage != BPStage.Onboarding)
                {
                    LogFail(name, $"after StartCase expected Onboarding, got {ctrl.CurrentStage}");
                    return;
                }

                // Advance six times: Onboarding -> GreetPatient ->
                // ExposeUpperArm -> WrapCuff -> PalpatePulse ->
                // PlaceStethoscope -> InflateCuff.
                for (int i = 0; i < 6; i++) ctrl.Advance();

                if (ctrl.CurrentStage != BPStage.InflateCuff)
                {
                    LogFail(name, $"after 6 Advance() calls expected InflateCuff, got {ctrl.CurrentStage}");
                    return;
                }

                // Cuff above systolic+headroom should auto-advance to
                // DeflateCuff. Default headroom is 30 so 120+30 = 150.
                c.SetPressure(155f);
                if (ctrl.CurrentStage != BPStage.DeflateCuff)
                {
                    LogFail(name, $"cuff at 155 should auto-advance to DeflateCuff, stage is {ctrl.CurrentStage}");
                    return;
                }

                // Walk forward to EnterAndAssess: DeflateCuff -> MarkReadings -> EnterAndAssess.
                ctrl.Advance();
                ctrl.Advance();
                if (ctrl.CurrentStage != BPStage.EnterAndAssess)
                {
                    LogFail(name, $"expected EnterAndAssess after 2 more Advance, got {ctrl.CurrentStage}");
                    return;
                }

                // A correct submission should auto-advance to Debrief.
                a.SetSystolic(120);
                a.SetDiastolic(80);
                a.Submit();

                if (ctrl.CurrentStage != BPStage.Debrief)
                {
                    LogFail(name, $"correct Submit should auto-advance to Debrief, stage is {ctrl.CurrentStage}");
                    return;
                }

                LogPass(name);
            }
            catch (Exception e)
            {
                LogFail(name, $"exception: {e}");
            }
            finally
            {
                SafeDestroy(ctrlGo);
                SafeDestroy(assessGo);
                SafeDestroy(korotkoffGo);
                SafeDestroy(valveGo);
                SafeDestroy(cuffGo);
                SafeDestroy(profile);
            }
        }

        // ============================================================
        // Convenience: run every test in sequence.
        // ============================================================
        [ContextMenu("BP Smoke: Run All")]
        public void RunAll()
        {
            Debug.Log($"{LogPrefix} --- Running all BP smoke tests ---");
            TestCuffInflateDeflateClamping();
            TestPatientProfileAudibility();
            TestAssessmentScoring();
            TestValveRateInWindowEvent();
            TestFullCaseOrchestrator();
            Debug.Log($"{LogPrefix} --- BP smoke tests complete ---");
        }
    }
}
