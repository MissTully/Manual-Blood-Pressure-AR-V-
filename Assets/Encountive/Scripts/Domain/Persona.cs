namespace Encountive.Domain
{
    /// <summary>
    /// Plain, Unity-free projection of a Cuff Size Selection persona
    /// (SDD §12). The Unity-side ScriptableObject persona asset produces
    /// one of these for the deterministic layer so the rules and gate
    /// engine never depend on UnityEngine.
    ///
    /// Boundary personas (Adult C, Pediatric P-F) carry
    /// <see cref="IsBoundaryPersona"/> so SG-7 / DP-3 fire regardless of
    /// where the sampled MUAC happens to land.
    /// </summary>
    public sealed class Persona
    {
        public string PersonaId { get; }
        public string DisplayName { get; }
        public PopulationClass Population { get; }
        public PediatricBand PediatricBand { get; }

        /// <summary>True for Adult C and Pediatric P-F. Forces boundary
        /// handling (DP-3) independent of the sampled MUAC.</summary>
        public bool IsBoundaryPersona { get; }

        /// <summary>Pediatric distress state used by Gate-1b (CSS-SG-3).</summary>
        public bool ShowsDistress { get; }

        public Persona(
            string personaId,
            string displayName,
            PopulationClass population,
            PediatricBand pediatricBand = PediatricBand.None,
            bool isBoundaryPersona = false,
            bool showsDistress = false)
        {
            PersonaId = personaId;
            DisplayName = displayName;
            Population = population;
            PediatricBand = pediatricBand;
            IsBoundaryPersona = isBoundaryPersona;
            ShowsDistress = showsDistress;
        }
    }

    /// <summary>
    /// Deterministic case parameter set (SDD §12.3). The same
    /// (<see cref="CaseId"/>, <see cref="ParameterSetVersion"/>) always
    /// reproduces identical values — required for assessment fairness.
    /// </summary>
    public sealed class CaseParameters
    {
        public string CaseId { get; }
        public string ParameterSetVersion { get; }
        public double MuacCm { get; }
        public Persona Persona { get; }

        public CaseParameters(string caseId, string parameterSetVersion, double muacCm, Persona persona)
        {
            CaseId = caseId;
            ParameterSetVersion = parameterSetVersion;
            MuacCm = muacCm;
            Persona = persona;
        }
    }
}
