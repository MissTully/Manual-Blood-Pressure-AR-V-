using System.Collections.Generic;
using System.Linq;
using Encountive.Domain;

namespace Encountive.SafetyGates
{
    /// <summary>Outcome of evaluating the cuff-sizing branch for a case.</summary>
    public sealed class CuffRecommendation
    {
        /// <summary>Branch identifier (B-1..B-9) per SDD §11.2.</summary>
        public string BranchId { get; }

        /// <summary>The single dominant class, or <see cref="CuffClass.None"/>
        /// when the case is a boundary (no clear winner).</summary>
        public CuffClass PrimaryClass { get; }

        /// <summary>Every class that satisfies the AHA 80%/40% rule for
        /// this case. On a boundary this holds the two adjacent classes.</summary>
        public IReadOnlyList<CuffClass> AcceptableClasses { get; }

        /// <summary>True when the AHA rule produces no single dominant
        /// choice (B-5 / B-9, or an explicit boundary persona). DP-3 must
        /// be engaged before commit (SDD §11.3).</summary>
        public bool IsBoundary { get; }

        /// <summary>True when MUAC is outside the supported 22–52 cm
        /// adult measurement domain (escalation, not a normal branch).</summary>
        public bool IsOutOfSupportedRange { get; }

        public CuffRecommendation(
            string branchId,
            CuffClass primaryClass,
            IReadOnlyList<CuffClass> acceptableClasses,
            bool isBoundary,
            bool isOutOfSupportedRange)
        {
            BranchId = branchId;
            PrimaryClass = primaryClass;
            AcceptableClasses = acceptableClasses;
            IsBoundary = isBoundary;
            IsOutOfSupportedRange = isOutOfSupportedRange;
        }
    }

    /// <summary>
    /// Deterministic encoding of the American Heart Association 80%/40%
    /// bladder-fit rule mapped onto the AAMI cuff labeling boundaries
    /// (SDD §11.2, branches B-1..B-9; clinical authority: Cuff Size
    /// Selection Blueprint v0.2).
    ///
    /// Adult bands are partitioned at the midpoints of the gaps in the
    /// SDD §11.2 integer ranges (26↔27, 34↔35, 44↔45) so a continuous
    /// MUAC always resolves. A MUAC within <see cref="BoundaryToleranceCm"/>
    /// of a partition edge is a boundary case (B-5): both adjacent
    /// classes are acceptable and DP-3 is required. Pediatric banding is
    /// authored on the persona (AAP 2017 age bands).
    ///
    /// Nothing here is AI-driven and nothing reads UnityEngine — this is
    /// the auditable, reproducible safety core (ADR-XR-004).
    /// </summary>
    public static class CuffSizeRules
    {
        public const double MinSupportedMuacCm = 22.0;
        public const double MaxSupportedMuacCm = 52.0;

        /// <summary>Half-width of the boundary band around each partition
        /// edge. Sourced from Blueprint v0.2; configurable there, fixed
        /// here for the deterministic Phase 1 build.</summary>
        public const double BoundaryToleranceCm = 1.0;

        // Adult partition edges (midpoints of the SDD §11.2 gaps).
        private const double EdgeSmallToAdult = 26.5;
        private const double EdgeAdultToLarge = 34.5;
        private const double EdgeLargeToThigh = 44.5;

        public static CuffRecommendation Recommend(Persona persona, double muacCm)
        {
            return persona.Population == PopulationClass.Pediatric
                ? RecommendPediatric(persona)
                : RecommendAdult(persona, muacCm);
        }

        private static CuffRecommendation RecommendAdult(Persona persona, double muacCm)
        {
            if (muacCm < MinSupportedMuacCm || muacCm > MaxSupportedMuacCm)
            {
                return new CuffRecommendation(
                    "OUT_OF_RANGE", CuffClass.None,
                    new List<CuffClass>(), isBoundary: false, isOutOfSupportedRange: true);
            }

            CuffClass band = AdultBand(muacCm);

            // Explicit boundary persona (Adult C) is always a boundary
            // case regardless of where the sampled MUAC lands.
            bool nearEdge =
                NearEdge(muacCm, EdgeSmallToAdult) ||
                NearEdge(muacCm, EdgeAdultToLarge) ||
                NearEdge(muacCm, EdgeLargeToThigh);

            if (persona.IsBoundaryPersona || nearEdge)
            {
                // Near a partition edge: exactly the two adjacent classes.
                // Persona-forced boundary away from an edge: the natural
                // band plus its immediate neighbors (clinical judgment
                // window taught at DP-3).
                var acceptable = nearEdge
                    ? BoundaryPair(muacCm)
                    : BandWithNeighbors(AdultBand(muacCm));
                return new CuffRecommendation(
                    "B-5", CuffClass.None, acceptable, isBoundary: true, isOutOfSupportedRange: false);
            }

            string branch = band switch
            {
                CuffClass.SmallAdult => "B-1",
                CuffClass.Adult => "B-2",
                CuffClass.AdultLarge => "B-3",
                CuffClass.AdultThigh => "B-4",
                _ => "B-2"
            };

            return new CuffRecommendation(
                branch, band, new List<CuffClass> { band },
                isBoundary: false, isOutOfSupportedRange: false);
        }

        private static CuffRecommendation RecommendPediatric(Persona persona)
        {
            // P-F (and any authored pediatric boundary) → B-9.
            if (persona.IsBoundaryPersona)
            {
                return new CuffRecommendation(
                    "B-9", CuffClass.None,
                    new List<CuffClass> { CuffClass.PediatricAdolescent, CuffClass.SmallAdult },
                    isBoundary: true, isOutOfSupportedRange: false);
            }

            switch (persona.PediatricBand)
            {
                case PediatricBand.Infant:
                    return Single("B-6", CuffClass.PediatricInfant);
                case PediatricBand.Child:
                    return Single("B-7", CuffClass.PediatricChild);
                case PediatricBand.Adolescent:
                    return Single("B-8", CuffClass.PediatricAdolescent);
                case PediatricBand.AdolescentAdultCrossover:
                    // B-8 crossover: judgment between adolescent and
                    // small adult; both acceptable, rationale required.
                    return new CuffRecommendation(
                        "B-8", CuffClass.None,
                        new List<CuffClass> { CuffClass.PediatricAdolescent, CuffClass.SmallAdult },
                        isBoundary: true, isOutOfSupportedRange: false);
                default:
                    return Single("B-7", CuffClass.PediatricChild);
            }
        }

        private static CuffRecommendation Single(string branch, CuffClass c) =>
            new CuffRecommendation(branch, c, new List<CuffClass> { c },
                isBoundary: false, isOutOfSupportedRange: false);

        private static CuffClass AdultBand(double muacCm)
        {
            if (muacCm < EdgeSmallToAdult) return CuffClass.SmallAdult;
            if (muacCm < EdgeAdultToLarge) return CuffClass.Adult;
            if (muacCm < EdgeLargeToThigh) return CuffClass.AdultLarge;
            return CuffClass.AdultThigh;
        }

        private static bool NearEdge(double muacCm, double edge) =>
            System.Math.Abs(muacCm - edge) <= BoundaryToleranceCm;

        private static readonly CuffClass[] AdultLadder =
        {
            CuffClass.SmallAdult, CuffClass.Adult, CuffClass.AdultLarge, CuffClass.AdultThigh
        };

        private static List<CuffClass> BandWithNeighbors(CuffClass band)
        {
            int i = System.Array.IndexOf(AdultLadder, band);
            var result = new List<CuffClass>();
            for (int j = i - 1; j <= i + 1; j++)
            {
                if (j >= 0 && j < AdultLadder.Length)
                    result.Add(AdultLadder[j]);
            }
            return result;
        }

        private static List<CuffClass> BoundaryPair(double muacCm)
        {
            if (NearEdge(muacCm, EdgeSmallToAdult))
                return new List<CuffClass> { CuffClass.SmallAdult, CuffClass.Adult };
            if (NearEdge(muacCm, EdgeAdultToLarge))
                return new List<CuffClass> { CuffClass.Adult, CuffClass.AdultLarge };
            return new List<CuffClass> { CuffClass.AdultLarge, CuffClass.AdultThigh };
        }

        /// <summary>True when the committed class satisfies the AHA rule
        /// for the case (CSS-SG-5 passes). On a boundary, either adjacent
        /// class is correct — the boundary itself is policed by SG-7.</summary>
        public static bool IsCommitCorrect(CuffClass committed, CuffRecommendation rec)
        {
            if (rec.IsOutOfSupportedRange) return false;
            return rec.AcceptableClasses.Contains(committed);
        }
    }
}
