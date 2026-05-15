using Encountive.SafetyGates;
using NUnit.Framework;

namespace Encountive.SafetyGates.Tests
{
    public sealed class BiasLexiconTests
    {
        private readonly BiasLexicon _lexicon = new BiasLexicon();

        [TestCase("The diabetic in room 2.", "diabetic")]
        [TestCase("She is hypertensive and noncompliant.", "hypertensive")]
        [TestCase("That obese patient again.", "obese patient")]
        [TestCase("A non-compliant attitude.", "non-compliant")]
        public void FlagsStigmatizingTerms(string text, string expectedTerm)
        {
            Assert.AreEqual(expectedTerm, _lexicon.FirstFlaggedTerm(text));
            Assert.IsTrue(_lexicon.IsStigmatizing(text));
        }

        [TestCase("This person with diabetes needs an Adult Large cuff.")]
        [TestCase("The patient's mid-arm circumference is 38 cm.")]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase(null)]
        public void DoesNotFlagPersonFirstOrEmptyText(string text)
        {
            Assert.IsNull(_lexicon.FirstFlaggedTerm(text));
            Assert.IsFalse(_lexicon.IsStigmatizing(text));
        }

        [Test]
        public void SubstringInsideWord_IsNotFlagged()
        {
            // "addict" must not match inside "addictive" / unrelated words;
            // word-boundary matching only.
            Assert.IsFalse(_lexicon.IsStigmatizing("an addictive design pattern"));
        }

        [Test]
        public void CustomLexicon_OverridesDefault()
        {
            var custom = new BiasLexicon(new[] { "widget" });

            Assert.IsTrue(custom.IsStigmatizing("the widget is here"));
            Assert.IsFalse(custom.IsStigmatizing("the diabetic is here"));
        }
    }
}
