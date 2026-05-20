using Encountive.Domain;
using UnityEngine;

namespace Encountive.UnityGlue
{
    /// <summary>
    /// Designer-authorable wrapper around the Unity-free
    /// <see cref="Persona"/> domain object (SDD §12; Cuff Size Selection
    /// Blueprint v0.2 §5 / §5.5). Build assets under
    /// <c>Assets/Encountive/Content/Personas/</c> via the "Create →
    /// Encountive → Persona" menu and reference them from station
    /// controllers / case-sampler hosts.
    /// </summary>
    [CreateAssetMenu(menuName = "Encountive/Persona", fileName = "Persona")]
    public sealed class PersonaAsset : ScriptableObject
    {
        [Tooltip("Stable identifier (adultA, adultB, pedF, etc.). Used in xAPI activity IRIs.")]
        public string personaId = "adultA";

        [Tooltip("Display name shown to instructors and in coach utterances.")]
        public string displayName = "Adult A";

        public PopulationClass population = PopulationClass.Adult;
        public PediatricBand pediatricBand = PediatricBand.None;

        [Tooltip("Boundary persona (Adult C, Pediatric P-F) — always engages DP-3.")]
        public bool isBoundaryPersona;

        [Tooltip("Pediatric distress for Gate-1b (CSS-SG-3).")]
        public bool showsDistress;

        public Persona ToDomain() => new Persona(
            personaId, displayName, population, pediatricBand,
            isBoundaryPersona, showsDistress);
    }
}
