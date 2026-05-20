using System.Collections.Generic;
using System.Threading.Tasks;
using Encountive.Domain;
using Encountive.Telemetry;
using Encountive.Triggers;

namespace Encountive.Stations
{
    public sealed class FullEncounterResult
    {
        public bool Completed { get; }
        public bool Aborted { get; }
        public string AbortReason { get; }
        public IReadOnlyList<StationRunResult> StationResults { get; }
        public IReadOnlyList<XApiStatement> AllEmitted { get; }

        public FullEncounterResult(
            bool completed, bool aborted, string abortReason,
            IReadOnlyList<StationRunResult> results,
            IReadOnlyList<XApiStatement> allEmitted)
        {
            Completed = completed;
            Aborted = aborted;
            AbortReason = abortReason;
            StationResults = results;
            AllEmitted = allEmitted;
        }
    }

    /// <summary>
    /// Glues the module-wide FSM (SDD §8.2) to the per-station runners.
    /// In Full Encounter mode it locks one persona and drives the six
    /// stations in <see cref="ModuleStateMachine.EncounterOrder"/>,
    /// emitting the <c>initialized</c> session statement up front and
    /// <c>terminated</c> at the end (profile §4.1). A failed/aborted
    /// station signals the module FSM to escalate to remediation and
    /// the encounter stops there.
    /// </summary>
    public sealed class FullEncounterOrchestrator
    {
        public async Task<FullEncounterResult> RunAsync(
            ModuleStateMachine module,
            IReadOnlyDictionary<string, IStationRunner> runners,
            Persona persona,
            XApiStatementFactory xapi,
            ITriggerClient triggers)
        {
            var allEmitted = new List<XApiStatement> { xapi.Initialized() };
            var results = new List<StationRunResult>();

            module.StartFullEncounter(persona);
            string abortReason = null;

            while (module.Phase == ModulePhase.InStation)
            {
                string id = module.CurrentStationId;
                if (!runners.TryGetValue(id, out IStationRunner runner))
                {
                    abortReason = "no runner registered for " + id;
                    module.EscalateToRemediation(abortReason);
                    break;
                }

                StationRunResult r = await runner.RunAsync(
                    module.PersonaForActiveStation(), module.Mode, xapi, triggers);
                results.Add(r);
                allEmitted.AddRange(r.Emitted);

                if (!r.Passed)
                {
                    abortReason = id + " failed";
                    module.EscalateToRemediation(abortReason);
                    break;
                }
                module.CompleteStation();
            }

            allEmitted.Add(xapi.Terminated());

            return new FullEncounterResult(
                completed: module.Phase == ModulePhase.Completed,
                aborted: module.Phase == ModulePhase.Remediation,
                abortReason: abortReason,
                results: results,
                allEmitted: allEmitted);
        }
    }
}
