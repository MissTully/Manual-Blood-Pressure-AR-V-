using System.Collections.Generic;
using System.Threading.Tasks;
using Encountive.Domain;
using Encountive.Telemetry;
using Encountive.Triggers;

namespace Encountive.Stations
{
    public sealed class StationRunResult
    {
        public string StationId { get; }
        public bool Passed { get; }
        public double ScoreScaled { get; }
        public IReadOnlyList<XApiStatement> Emitted { get; }

        public StationRunResult(
            string stationId, bool passed, double scoreScaled,
            IReadOnlyList<XApiStatement> emitted)
        {
            StationId = stationId;
            Passed = passed;
            ScoreScaled = scoreScaled;
            Emitted = emitted;
        }
    }

    /// <summary>
    /// One station's host contract for the module orchestrator. Each
    /// station is modeled as a lesson under the profile (§5), so a
    /// runner emits at minimum <c>attempted</c> at entry and
    /// <c>passed</c> / <c>failed</c> at exit (§4.1). Station 2 (Cuff
    /// Size Selection) has a full FSM; Stations 1, 3–6 use a Phase 1
    /// scaffold (see <see cref="ScaffoldStation"/>) and will be
    /// fleshed out in later iterations.
    /// </summary>
    public interface IStationRunner
    {
        string StationId { get; }
        string DisplayName { get; }
        Task<StationRunResult> RunAsync(
            Persona persona, TrainingMode mode,
            XApiStatementFactory xapi, ITriggerClient triggers);
    }
}
