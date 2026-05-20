using System.Collections.Generic;
using System.Threading.Tasks;
using Encountive.Domain;
using Encountive.Telemetry;
using Encountive.Triggers;

namespace Encountive.Stations
{
    /// <summary>
    /// Placeholder station runner for Stations 1, 3, 4, 5, 6 in
    /// Phase 1. Emits the minimum profile-conformant lesson lifecycle
    /// (attempted at entry, passed/failed at exit) plus a single
    /// scenario-framing received-coaching event, so the Module FSM can
    /// chain a full encounter end-to-end while the deeper rubric work
    /// for these stations is staged later (SDD build plan WP-9).
    /// </summary>
    public sealed class ScaffoldStation : IStationRunner
    {
        private readonly string _id;
        private readonly string _name;
        private readonly string _scenarioFramingTrigger;
        private readonly double _scaledScore;
        private readonly bool _passes;

        public ScaffoldStation(
            string stationId, string displayName,
            string scenarioFramingTrigger = null,
            double scaledScore = 1.0, bool passes = true)
        {
            _id = stationId;
            _name = displayName;
            _scenarioFramingTrigger = scenarioFramingTrigger;
            _scaledScore = scaledScore;
            _passes = passes;
        }

        public string StationId => _id;
        public string DisplayName => _name;

        public async Task<StationRunResult> RunAsync(
            Persona persona, TrainingMode mode,
            XApiStatementFactory xapi, ITriggerClient triggers)
        {
            var emitted = new List<XApiStatement>();
            emitted.Add(xapi.EnterLesson(_id, _name));

            if (_scenarioFramingTrigger != null && triggers != null)
            {
                // The trigger client may return text; we don't surface it
                // here — the host UI listens to its own coach pipeline.
                await triggers.Fire(new TriggerEvent
                {
                    StationId = _id,
                    TriggerId = _scenarioFramingTrigger,
                    Family = TriggerFamily.ScenarioFraming,
                    Mode = mode,
                    Target = "galaxy_xr"
                });
                emitted.Add(xapi.ReceivedCoaching(_scenarioFramingTrigger));
            }

            emitted.Add(_passes
                ? xapi.PassedLesson(_id, _name, _scaledScore)
                : xapi.FailedLesson(_id, _name, _scaledScore));

            return new StationRunResult(_id, _passes, _scaledScore, emitted);
        }
    }

    /// <summary>The Phase 1 canonical scaffold for the five non-Station-2
    /// stations. Real implementations replace these station-by-station.</summary>
    public static class Phase1StationScaffolds
    {
        public static IReadOnlyList<IStationRunner> NonStation2() => new IStationRunner[]
        {
            new ScaffoldStation("S1", "Patient preparation and consent"),
            new ScaffoldStation("S3", "Cuff application"),
            new ScaffoldStation("S4", "Stethoscope placement and bulb inflation"),
            new ScaffoldStation("S5", "Auscultation and reading"),
            new ScaffoldStation("S6", "Patient communication and documentation"),
        };
    }
}
