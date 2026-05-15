using System.Collections.Generic;

namespace Encountive.Stations
{
    /// <summary>One scored rubric criterion (SDD §14.1): 0–4, mastery
    /// threshold ≥ 3. <see cref="Evidence"/> records the session facts
    /// that informed the score (SDD §14.2).</summary>
    public sealed class CriterionScore
    {
        public string Criterion { get; }
        public int Score { get; }
        public string Evidence { get; }

        public CriterionScore(string criterion, int score, string evidence)
        {
            Criterion = criterion;
            Score = score;
            Evidence = evidence;
        }

        public bool IsMastery => Score >= 3;
        public bool IsCriticalFailure => Score == 0;
    }

    /// <summary>
    /// Mutable evidence accumulated by the Cuff Size Selection state
    /// machine across a single attempt; consumed by the rubric at S5.
    /// </summary>
    public sealed class CuffEvidence
    {
        public bool Pediatric;

        public bool IdentityVerifiedBeforeContact;
        public bool ConsentObtained;

        public bool LandmarksAccurateFirstAttempt;
        public bool LandmarksAccurateEventually;

        public bool MeasurementWithinTolerance;

        public bool CuffCorrectOnFirstCommit;
        public bool Sg5Fired;

        public bool BoundaryCase;
        public bool Dp3Engaged;

        public bool RationaleProvided;
        public bool RationalePersonFirst;
        public bool Sg6Fired;

        public bool AssentObtained;
        public bool CalmAchieved;
        public bool PediatricAdvancedWithoutPrereq;
    }

    /// <summary>
    /// Cuff Size Selection rubric (SDD §14.1). Phase 1 deterministic
    /// scoring against the seven criteria; the 4 / 0 anchors are taken
    /// from the SDD table and intermediate values are a documented
    /// Phase 1 heuristic PENDING the verbatim Cuff Size Selection
    /// Blueprint v0.2 Section 9.
    /// </summary>
    public static class CuffSizeSelectionRubric
    {
        public static IReadOnlyList<CriterionScore> Score(CuffEvidence e)
        {
            var scores = new List<CriterionScore>
            {
                Identity(e),
                Landmarks(e),
                Measurement(e),
                CuffCorrectness(e),
                BoundaryRecognition(e),
                Rationale(e),
                PediatricProvisions(e)
            };
            return scores;
        }

        private static CriterionScore Identity(CuffEvidence e)
        {
            if (!e.IdentityVerifiedBeforeContact)
                return new CriterionScore("Identity verification (S1)", 0,
                    "Patient contact made without identity verification.");
            int s = e.ConsentObtained ? 4 : 3;
            return new CriterionScore("Identity verification (S1)", s,
                e.ConsentObtained
                    ? "Name and date of birth confirmed; consent obtained."
                    : "Identity verified; consent not explicitly captured.");
        }

        private static CriterionScore Landmarks(CuffEvidence e)
        {
            if (e.LandmarksAccurateFirstAttempt)
                return new CriterionScore("Landmark identification (S2)", 4,
                    "Acromion, olecranon, midpoint placed within tolerance on first attempt.");
            if (e.LandmarksAccurateEventually)
                return new CriterionScore("Landmark identification (S2)", 3,
                    "Midpoint placed within tolerance after correction.");
            return new CriterionScore("Landmark identification (S2)", 0,
                "Midpoint mis-placed beyond tolerance.");
        }

        private static CriterionScore Measurement(CuffEvidence e) =>
            e.MeasurementWithinTolerance
                ? new CriterionScore("Measurement (S3)", 4,
                    "Tape positioned at midpoint and snug; reading within tolerance.")
                : new CriterionScore("Measurement (S3)", 0,
                    "Tape mis-positioned or reading out of range.");

        private static CriterionScore CuffCorrectness(CuffEvidence e)
        {
            if (e.Sg5Fired)
                return new CriterionScore("Cuff class correctness (S4)", 0,
                    "Wrong cuff class committed (CSS-SG-5 fired).");
            return new CriterionScore("Cuff class correctness (S4)",
                e.CuffCorrectOnFirstCommit ? 4 : 3,
                e.CuffCorrectOnFirstCommit
                    ? "AHA 80%/40% rule satisfied on first commit."
                    : "Correct class committed after coaching.");
        }

        private static CriterionScore BoundaryRecognition(CuffEvidence e)
        {
            if (!e.BoundaryCase)
                return new CriterionScore("Boundary recognition (S4/S5)", 4,
                    "Not a boundary case; criterion not applicable (no penalty).");
            return e.Dp3Engaged
                ? new CriterionScore("Boundary recognition (S4/S5)", 4,
                    "Boundary detected and DP-3 engaged.")
                : new CriterionScore("Boundary recognition (S4/S5)", 0,
                    "Boundary missed; commit made without DP-3 engagement.");
        }

        private static CriterionScore Rationale(CuffEvidence e)
        {
            if (!e.RationaleProvided || e.Sg6Fired)
                return new CriterionScore("Rationale capture (S5)", 0,
                    e.Sg6Fired ? "Stigmatizing language detected."
                               : "No rationale offered.");
            return new CriterionScore("Rationale capture (S5)",
                e.RationalePersonFirst ? 4 : 3,
                e.RationalePersonFirst
                    ? "Coherent, person-first explanation referencing the AHA rule."
                    : "Rationale provided; person-first phrasing could improve.");
        }

        private static CriterionScore PediatricProvisions(CuffEvidence e)
        {
            if (!e.Pediatric)
                return new CriterionScore("Pediatric provisions (S1, S2)", 4,
                    "Adult persona; criterion not applicable (no penalty).");
            if (e.PediatricAdvancedWithoutPrereq)
                return new CriterionScore("Pediatric provisions (S1, S2)", 0,
                    "Pediatric persona advanced without assent or calm state.");
            return (e.AssentObtained && e.CalmAchieved)
                ? new CriterionScore("Pediatric provisions (S1, S2)", 4,
                    "Gate-1a and Gate-1b satisfied without coach prompting.")
                : new CriterionScore("Pediatric provisions (S1, S2)", 3,
                    "Pediatric prerequisites satisfied after coaching.");
        }
    }
}
