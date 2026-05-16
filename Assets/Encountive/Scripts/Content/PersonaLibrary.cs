using System.Collections.Generic;
using Encountive.Domain;

namespace Encountive.Content
{
    /// <summary>A persona plus the mid-upper-arm-circumference range the
    /// deterministic sampler may draw from. The range is authored to
    /// stay inside the persona's intended cuff band (boundary personas
    /// straddle a partition edge on purpose).</summary>
    public sealed class PersonaSpec
    {
        public Persona Persona { get; }
        public double MuacMinCm { get; }
        public double MuacMaxCm { get; }

        public PersonaSpec(Persona persona, double muacMinCm, double muacMaxCm)
        {
            Persona = persona;
            MuacMinCm = muacMinCm;
            MuacMaxCm = muacMaxCm;
        }
    }

    /// <summary>
    /// Cuff Size Selection persona library (SDD §12.1–12.2): adult
    /// A/B/C and pediatric P-A..P-H. Adult B is "Margarita Delgado",
    /// the Full Encounter persona shared across all six stations
    /// (SDD §12.1, §3.5).
    ///
    /// MUAC ranges and pediatric band assignments are documented
    /// Phase 1 placeholders consistent with the SDD §11.2 branch
    /// boundaries; they are PENDING the verbatim Cuff Size Selection
    /// Blueprint v0.2 Section 5/5.5 and must be reconciled before
    /// cohort exposure.
    /// </summary>
    public static class PersonaLibrary
    {
        // Canonical ranges are kept clear of the ±BoundaryToleranceCm
        // zones around the SDD §11.2 partition edges (26.5 / 34.5 /
        // 44.5) so these personas always resolve to a single cuff class
        // and never trip boundary detection — that is Adult C's job.
        public static readonly PersonaSpec AdultA = new PersonaSpec(
            new Persona("adultA", "Adult A", PopulationClass.Adult),
            28.0, 33.0); // canonical Adult band (B-2)

        public static readonly PersonaSpec AdultB = new PersonaSpec(
            new Persona("adultB", "Margarita Delgado", PopulationClass.Adult),
            36.0, 43.0); // Adult Large band (B-3); Full Encounter persona

        public static readonly PersonaSpec AdultC = new PersonaSpec(
            new Persona("adultC", "Adult C", PopulationClass.Adult,
                isBoundaryPersona: true),
            33.5, 35.5); // straddles the Adult ↔ Adult Large edge (B-5)

        public static readonly PersonaSpec PedA = Ped("pedA", "Pediatric P-A", PediatricBand.Infant);
        public static readonly PersonaSpec PedB = Ped("pedB", "Pediatric P-B", PediatricBand.Infant);
        public static readonly PersonaSpec PedC = Ped("pedC", "Pediatric P-C", PediatricBand.Child);
        public static readonly PersonaSpec PedD = Ped("pedD", "Pediatric P-D", PediatricBand.Child);
        public static readonly PersonaSpec PedE = Ped("pedE", "Pediatric P-E", PediatricBand.Child);
        public static readonly PersonaSpec PedF = new PersonaSpec(
            new Persona("pedF", "Pediatric P-F", PopulationClass.Pediatric,
                PediatricBand.Adolescent, isBoundaryPersona: true),
            0, 0); // pediatric boundary (B-9); MUAC band-authored
        public static readonly PersonaSpec PedG = Ped("pedG", "Pediatric P-G", PediatricBand.Adolescent);
        public static readonly PersonaSpec PedH = new PersonaSpec(
            new Persona("pedH", "Pediatric P-H", PopulationClass.Pediatric,
                PediatricBand.AdolescentAdultCrossover),
            0, 0); // adolescent ↔ small adult crossover (B-8)

        private static PersonaSpec Ped(string id, string name, PediatricBand band) =>
            new PersonaSpec(
                new Persona(id, name, PopulationClass.Pediatric, band),
                0, 0); // pediatric cuff is band-driven, not MUAC-driven

        public static readonly IReadOnlyList<PersonaSpec> All = new[]
        {
            AdultA, AdultB, AdultC,
            PedA, PedB, PedC, PedD, PedE, PedF, PedG, PedH
        };

        public static readonly IReadOnlyList<PersonaSpec> Adults = new[]
        {
            AdultA, AdultB, AdultC
        };

        public static PersonaSpec ById(string personaId)
        {
            foreach (var s in All)
                if (s.Persona.PersonaId == personaId) return s;
            return null;
        }
    }
}
