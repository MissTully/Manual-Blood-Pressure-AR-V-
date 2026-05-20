using System;
using System.Threading.Tasks;
using Encountive.Domain;
using Encountive.SafetyGates;
using Encountive.Stations;
using Encountive.Telemetry;
using Encountive.Triggers;
using UnityEngine;
using UnityEngine.Events;

namespace Encountive.UnityGlue
{
    /// <summary>
    /// Unity host for the Cuff Size Selection state machine (WP-6).
    /// Owns the per-session FSM and surfaces lifecycle events via
    /// <see cref="UnityEvent"/>s so a scene's UI, audio, and visual
    /// scaffolding components can subscribe without referencing the
    /// pure FSM types directly.
    ///
    /// Phase 1 wiring: the trigger client is the hand-authored local
    /// fallback (ADR-XR-008); the xAPI sink is whatever the scene
    /// supplies (file/HTTPS in later phases). Every emitted statement
    /// is run through <see cref="StatementValidator"/> before being
    /// queued — invalid statements are logged but never transmitted
    /// (AR BP Cuff Trainer xAPI profile §10).
    /// </summary>
    public sealed class Station2Controller : MonoBehaviour
    {
        [Header("Persona")]
        [SerializeField] private PersonaAsset persona;

        [Header("Session")]
        [SerializeField] private string sessionId = Guid.NewGuid().ToString();
        [SerializeField] private string learnerPseudonym = "learner-anon";
        [SerializeField] private string institutionalLearnerId = "anon-001";
        [SerializeField] private string profileVersion = "v1.0";
        [SerializeField] private string appVersion = "0.1.0";
        [SerializeField] private string sdkVersion = "androidxr";
        [SerializeField] private string audienceTag = "vma";
        [SerializeField] private TrainingMode mode = TrainingMode.Guided;

        [Header("Events")]
        public UnityEvent<CssStage> OnStageChanged = new UnityEvent<CssStage>();
        public UnityEvent<string> OnCoachUtterance = new UnityEvent<string>();
        public UnityEvent<string> OnGateFired = new UnityEvent<string>();
        public UnityEvent<bool> OnStationComplete = new UnityEvent<bool>();

        private CuffSizeSelectionStateMachine _fsm;
        private XApiStatementFactory _xapi;
        private XApiEmitter _emitter;
        private readonly StatementValidator _validator = new StatementValidator();
        private CssStage _lastStage;

        public CssStage Stage => _fsm?.Stage ?? CssStage.S1_IdentityConsent;
        public XApiEmitter Emitter => _emitter;

        /// <summary>Initialize with an external sink (HTTPS façade in
        /// Phase 2; local file in Phase 1).</summary>
        public void Initialize(IXApiSink sink)
        {
            if (persona == null)
            {
                Debug.LogError("[Station2Controller] PersonaAsset not assigned.", this);
                return;
            }

            _xapi = new XApiStatementFactory(new XApiSessionInfo(
                sessionId, learnerPseudonym, institutionalLearnerId,
                profileVersion: profileVersion, appVersion: appVersion,
                sdkVersion: sdkVersion, audienceTag: audienceTag));
            _emitter = new XApiEmitter(sink);

            _fsm = new CuffSizeSelectionStateMachine(
                new SafetyGateEngine(),
                new LocalFallbackTriggerClient(),
                _xapi, persona.ToDomain(), mode);
            _lastStage = _fsm.Stage;
        }

        public async Task StartStationAsync()
        {
            if (_fsm == null) throw new InvalidOperationException("Initialize first.");
            await Handle(await _fsm.Start());
        }

        public async Task StepAsync(CuffAction action, CuffActionData data = null)
        {
            if (_fsm == null) throw new InvalidOperationException("Initialize first.");
            await Handle(await _fsm.Step(action, data));
        }

        private Task Handle(StepResult r)
        {
            foreach (XApiStatement s in r.Emitted)
            {
                var v = _validator.Validate(s);
                if (!v.Ok)
                {
                    Debug.LogWarning(
                        $"[Station2Controller] invalid xAPI statement dropped: " +
                        string.Join("; ", v.Errors), this);
                    continue;
                }
                _emitter.Emit(s);
            }

            if (r.CoachTriggerId != null)
                OnCoachUtterance?.Invoke(r.CoachText ?? r.CoachTriggerId);

            if (r.GateFired && r.Gate != null)
                OnGateFired?.Invoke(r.Gate.GateId);

            if (r.Stage != _lastStage)
            {
                _lastStage = r.Stage;
                OnStageChanged?.Invoke(r.Stage);
            }

            if (r.StationComplete)
                OnStationComplete?.Invoke(_fsm.Evidence != null);

            // Fire and forget — the host can also call FlushAsync()
            // explicitly at session end.
            _ = _emitter.FlushAsync();
            return Task.CompletedTask;
        }
    }
}
