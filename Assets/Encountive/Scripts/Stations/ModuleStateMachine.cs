using System.Collections.Generic;
using Encountive.Domain;

namespace Encountive.Stations
{
    public enum ModulePhase
    {
        Selector,
        InStation,
        Remediation,
        Completed
    }

    /// <summary>
    /// Module-wide state machine above the per-station FSMs (SDD §8.2).
    /// In single-station modes it routes the learner into the chosen
    /// station and back to the selector at exit. In Full Encounter it
    /// binds one persona at session start and propagates it unchanged
    /// to all six stations (SDD §2.3, §3.5 persona consistency); a
    /// gate escalation (e.g. Gate-1 identity skipped, SDD §8.5) aborts
    /// the encounter to a remediation pathway (Master Design v0.3
    /// §12.4).
    ///
    /// Pure C#: the ordering, persona-lock and abort policy are
    /// deterministic and unit-tested headlessly.
    /// </summary>
    public sealed class ModuleStateMachine
    {
        public static readonly IReadOnlyList<string> EncounterOrder =
            new[] { "S1", "S2", "S3", "S4", "S5", "S6" };

        public ModulePhase Phase { get; private set; } = ModulePhase.Selector;
        public TrainingMode Mode { get; private set; }
        public Persona LockedPersona { get; private set; }
        public string CurrentStationId { get; private set; }
        public string RemediationReason { get; private set; }

        private int _encounterIndex = -1;
        private bool _fullEncounter;

        /// <summary>Single-station entry (Guided / Practice /
        /// Evaluation). Returns false if a session is already active.</summary>
        public bool StartSingleStation(string stationId, Persona persona, TrainingMode mode)
        {
            if (Phase != ModulePhase.Selector) return false;
            if (mode == TrainingMode.FullEncounter) return false;
            Mode = mode;
            LockedPersona = persona;
            CurrentStationId = stationId;
            _fullEncounter = false;
            Phase = ModulePhase.InStation;
            return true;
        }

        /// <summary>Full Encounter entry: lock the persona for all six
        /// stations.</summary>
        public bool StartFullEncounter(Persona persona)
        {
            if (Phase != ModulePhase.Selector) return false;
            Mode = TrainingMode.FullEncounter;
            LockedPersona = persona;
            _fullEncounter = true;
            _encounterIndex = 0;
            CurrentStationId = EncounterOrder[0];
            Phase = ModulePhase.InStation;
            return true;
        }

        /// <summary>Report the active station finished cleanly. In
        /// single-station mode this returns to the selector; in Full
        /// Encounter it advances to the next station or completes after
        /// S6.</summary>
        public void CompleteStation()
        {
            if (Phase != ModulePhase.InStation) return;

            if (!_fullEncounter)
            {
                CurrentStationId = null;
                Phase = ModulePhase.Selector;
                return;
            }

            _encounterIndex++;
            if (_encounterIndex >= EncounterOrder.Count)
            {
                CurrentStationId = null;
                Phase = ModulePhase.Completed;
            }
            else
            {
                CurrentStationId = EncounterOrder[_encounterIndex];
            }
        }

        /// <summary>A station raised a gate escalation. The encounter
        /// aborts to remediation and no further stations run
        /// (SDD §8.5).</summary>
        public void EscalateToRemediation(string reason)
        {
            if (Phase != ModulePhase.InStation) return;
            RemediationReason = reason;
            CurrentStationId = null;
            Phase = ModulePhase.Remediation;
        }

        /// <summary>The persona handed to whichever station is active.
        /// Always the locked persona — stations never re-pick it
        /// (SDD §3.5).</summary>
        public Persona PersonaForActiveStation() => LockedPersona;
    }
}
