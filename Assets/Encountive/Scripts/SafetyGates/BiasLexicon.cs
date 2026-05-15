using System.Collections.Generic;
using System.Linq;

namespace Encountive.SafetyGates
{
    /// <summary>
    /// Phase 1 deterministic stand-in for the bias-detection model at
    /// SG-6 (SDD §13.3, §14.4). Phase 1 ships no AI surface, so
    /// stigmatizing-language detection is a configurable lexicon match
    /// (case-insensitive, word-boundary). Thresholds favor coaching over
    /// false positives (SDD §14.4): a flag is raised only on an exact
    /// flagged-term hit, never on fuzzy similarity.
    ///
    /// Replaced by the evaluated bias-detection model in a later phase;
    /// the SG-6 wiring and the re-capture flow are identical, so only
    /// this class changes.
    /// </summary>
    public sealed class BiasLexicon
    {
        private readonly HashSet<string> _flagged;

        /// <summary>Minimal default set of condition-first / stigmatizing
        /// terms with person-first alternatives taught at SG-6. Authored
        /// content; expanded via the constructor for cohort tuning.</summary>
        public static readonly IReadOnlyList<string> DefaultFlaggedTerms = new[]
        {
            "diabetic",
            "hypertensive",
            "the obese",
            "obese patient",
            "noncompliant",
            "non-compliant",
            "drug abuser",
            "addict",
            "frequent flyer"
        };

        public BiasLexicon(IEnumerable<string> flaggedTerms = null)
        {
            var source = flaggedTerms ?? DefaultFlaggedTerms;
            _flagged = new HashSet<string>(
                source.Select(t => t.Trim().ToLowerInvariant()));
        }

        /// <summary>Returns the flagged term that appears earliest in the
        /// rationale text, or null when the text is clean. Position-based
        /// (not set-iteration order) so the result is deterministic.</summary>
        public string FirstFlaggedTerm(string rationaleText)
        {
            if (string.IsNullOrWhiteSpace(rationaleText)) return null;

            string normalized = " " + Normalize(rationaleText) + " ";
            string earliest = null;
            int earliestIndex = int.MaxValue;
            foreach (string term in _flagged)
            {
                int idx = normalized.IndexOf(" " + term + " ", System.StringComparison.Ordinal);
                if (idx >= 0 && idx < earliestIndex)
                {
                    earliestIndex = idx;
                    earliest = term;
                }
            }
            return earliest;
        }

        public bool IsStigmatizing(string rationaleText) =>
            FirstFlaggedTerm(rationaleText) != null;

        private static string Normalize(string text)
        {
            var chars = text.ToLowerInvariant()
                .Select(c => char.IsLetterOrDigit(c) || c == '-' ? c : ' ');
            return new string(chars.ToArray());
        }
    }
}
