using Encountive.Domain;
using Encountive.SafetyGates;
using NUnit.Framework;

namespace Encountive.SafetyGates.Tests
{
    /// <summary>
    /// Covers every adult branch B-1..B-5 and pediatric branch B-6..B-9,
    /// the AHA 80%/40% commit check, and every MUAC partition-edge
    /// off-by-one (SDD §16.1).
    /// </summary>
    public sealed class CuffSizeRulesTests
    {
        private static Persona Adult(bool boundary = false) =>
            new Persona("adultX", "Adult X", PopulationClass.Adult,
                isBoundaryPersona: boundary);

        private static Persona Ped(PediatricBand band, bool boundary = false) =>
            new Persona("pedX", "Ped X", PopulationClass.Pediatric, band,
                isBoundaryPersona: boundary);

        [TestCase(22.0, CuffClass.SmallAdult, "B-1")]
        [TestCase(25.0, CuffClass.SmallAdult, "B-1")]
        [TestCase(30.0, CuffClass.Adult, "B-2")]
        [TestCase(40.0, CuffClass.AdultLarge, "B-3")]
        [TestCase(50.0, CuffClass.AdultThigh, "B-4")]
        [TestCase(52.0, CuffClass.AdultThigh, "B-4")]
        public void AdultBands_ResolveToExpectedClass(double muac, CuffClass expected, string branch)
        {
            CuffRecommendation rec = CuffSizeRules.Recommend(Adult(), muac);

            Assert.IsFalse(rec.IsBoundary);
            Assert.IsFalse(rec.IsOutOfSupportedRange);
            Assert.AreEqual(expected, rec.PrimaryClass);
            Assert.AreEqual(branch, rec.BranchId);
        }

        [TestCase(26.5)] // small ↔ adult edge
        [TestCase(34.5)] // adult ↔ large edge
        [TestCase(44.5)] // large ↔ thigh edge
        [TestCase(27.4)] // within tolerance of an edge
        public void NearPartitionEdge_IsBoundary_B5(double muac)
        {
            CuffRecommendation rec = CuffSizeRules.Recommend(Adult(), muac);

            Assert.IsTrue(rec.IsBoundary);
            Assert.AreEqual("B-5", rec.BranchId);
            Assert.AreEqual(CuffClass.None, rec.PrimaryClass);
            Assert.AreEqual(2, rec.AcceptableClasses.Count);
        }

        [Test]
        public void BoundaryPersona_AdultC_IsAlwaysBoundary()
        {
            // MUAC sits squarely inside the Adult band, but persona C
            // forces boundary handling.
            CuffRecommendation rec = CuffSizeRules.Recommend(Adult(boundary: true), 30.0);

            Assert.IsTrue(rec.IsBoundary);
            Assert.AreEqual("B-5", rec.BranchId);
        }

        [TestCase(21.9)]
        [TestCase(52.1)]
        [TestCase(0.0)]
        public void MuacOutsideSupportedDomain_IsOutOfRange(double muac)
        {
            CuffRecommendation rec = CuffSizeRules.Recommend(Adult(), muac);

            Assert.IsTrue(rec.IsOutOfSupportedRange);
            Assert.AreEqual("OUT_OF_RANGE", rec.BranchId);
            Assert.IsFalse(CuffSizeRules.IsCommitCorrect(CuffClass.Adult, rec));
        }

        [TestCase(PediatricBand.Infant, CuffClass.PediatricInfant, "B-6")]
        [TestCase(PediatricBand.Child, CuffClass.PediatricChild, "B-7")]
        [TestCase(PediatricBand.Adolescent, CuffClass.PediatricAdolescent, "B-8")]
        [TestCase(PediatricBand.None, CuffClass.PediatricChild, "B-7")]
        public void PediatricBands_ResolveToExpectedClass(
            PediatricBand band, CuffClass expected, string branch)
        {
            CuffRecommendation rec = CuffSizeRules.Recommend(Ped(band), 0.0);

            Assert.IsFalse(rec.IsBoundary);
            Assert.AreEqual(expected, rec.PrimaryClass);
            Assert.AreEqual(branch, rec.BranchId);
        }

        [Test]
        public void PediatricCrossover_IsBoundary_B8()
        {
            CuffRecommendation rec =
                CuffSizeRules.Recommend(Ped(PediatricBand.AdolescentAdultCrossover), 0.0);

            Assert.IsTrue(rec.IsBoundary);
            Assert.AreEqual("B-8", rec.BranchId);
            CollectionAssert.AreEquivalent(
                new[] { CuffClass.PediatricAdolescent, CuffClass.SmallAdult },
                rec.AcceptableClasses);
        }

        [Test]
        public void PediatricBoundaryPersona_PF_IsBoundary_B9()
        {
            CuffRecommendation rec =
                CuffSizeRules.Recommend(Ped(PediatricBand.Child, boundary: true), 0.0);

            Assert.IsTrue(rec.IsBoundary);
            Assert.AreEqual("B-9", rec.BranchId);
        }

        [Test]
        public void IsCommitCorrect_AcceptsBandMatch_RejectsMismatch()
        {
            CuffRecommendation rec = CuffSizeRules.Recommend(Adult(), 30.0);

            Assert.IsTrue(CuffSizeRules.IsCommitCorrect(CuffClass.Adult, rec));
            Assert.IsFalse(CuffSizeRules.IsCommitCorrect(CuffClass.AdultLarge, rec));
        }

        [Test]
        public void IsCommitCorrect_OnBoundary_AcceptsEitherAdjacentClass()
        {
            CuffRecommendation rec = CuffSizeRules.Recommend(Adult(), 34.5);

            Assert.IsTrue(CuffSizeRules.IsCommitCorrect(CuffClass.Adult, rec));
            Assert.IsTrue(CuffSizeRules.IsCommitCorrect(CuffClass.AdultLarge, rec));
            Assert.IsFalse(CuffSizeRules.IsCommitCorrect(CuffClass.SmallAdult, rec));
        }
    }
}
