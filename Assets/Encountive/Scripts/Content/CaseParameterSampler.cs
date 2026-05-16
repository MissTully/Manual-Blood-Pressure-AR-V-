using Encountive.Domain;

namespace Encountive.Content
{
    /// <summary>
    /// Deterministic case-parameter sampler (SDD §12.3, ADR-XR-005).
    /// The same (caseId, parameterSetVersion) always reproduces the
    /// same parameters for a persona — required for assessment fairness
    /// across learners and for replay determinism (SDD §16.6). Phase 1
    /// is the sampler only; the generative narrative wrapper is Phase 3
    /// (ADR-XR-008) and never originates clinical facts.
    ///
    /// Determinism note: a hand-rolled FNV-1a hash is used rather than
    /// string.GetHashCode, which is randomized per-process in .NET and
    /// would break reproducibility.
    /// </summary>
    public sealed class CaseParameterSampler
    {
        private readonly string _parameterSetVersion;

        public CaseParameterSampler(string parameterSetVersion = "v0-phase1")
        {
            _parameterSetVersion = parameterSetVersion;
        }

        public CaseParameters Sample(string caseId, PersonaSpec spec)
        {
            double muac;
            if (spec.Persona.Population == PopulationClass.Pediatric ||
                spec.MuacMaxCm <= spec.MuacMinCm)
            {
                // Pediatric cuff selection is band-driven; MUAC is not
                // the deciding input, so it is left at 0.
                muac = 0.0;
            }
            else
            {
                double unit = UnitInterval(
                    caseId + "|" + _parameterSetVersion + "|" + spec.Persona.PersonaId);
                double raw = spec.MuacMinCm + unit * (spec.MuacMaxCm - spec.MuacMinCm);
                muac = System.Math.Round(raw, 1); // 0.1 cm precision
            }

            return new CaseParameters(
                caseId, _parameterSetVersion, muac, spec.Persona);
        }

        /// <summary>FNV-1a 32-bit → [0,1). Stable across processes and
        /// platforms (pure integer arithmetic).</summary>
        private static double UnitInterval(string key)
        {
            const uint offset = 2166136261;
            const uint prime = 16777619;
            uint hash = offset;
            foreach (char c in key)
            {
                hash ^= c;
                hash *= prime;
            }
            return hash / 4294967296.0; // / 2^32
        }
    }
}
