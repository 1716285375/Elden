using NUnit.Framework;

namespace ZZ.Combat.Tests
{
    [TestFixture]
    public sealed class ParryRulesTests
    {
        [Test]
        public void CanParry_RejectsUnparryableAttacks()
        {
            Assert.IsFalse(ParryRules.CanParry(0, 3, 1f));
        }

        [Test]
        public void CanParry_RequiresEnoughParryStrength()
        {
            Assert.IsTrue(ParryRules.CanParry(2, 3, 1f));
            Assert.IsFalse(ParryRules.CanParry(4, 3, 1f));
        }

        [Test]
        public void CanParry_RequiresTheDefenderToFaceTheAttack()
        {
            Assert.IsFalse(ParryRules.CanParry(1, 3, 0.3f));
            Assert.IsFalse(ParryRules.CanParry(1, 3, -1f));
            Assert.IsTrue(ParryRules.CanParry(1, 3, 0.9f));
        }
    }
}
