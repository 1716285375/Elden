using NUnit.Framework;

namespace ZZ.Combat.Tests
{
    [TestFixture]
    public sealed class WeaponAttackRatingTests
    {
        [Test]
        public void MeetsRequirements_WithExactAttributes_Passes()
        {
            Assert.IsTrue(WeaponAttackRating.MeetsRequirements(10, 10, 10, 10, false, 10, 10, 10, 10));
        }

        [Test]
        public void MeetsRequirements_BelowStrength_Fails()
        {
            Assert.IsFalse(WeaponAttackRating.MeetsRequirements(9, 10, 10, 10, false, 10, 10, 10, 10));
        }

        [Test]
        public void MeetsRequirements_TwoHandingMultipliesStrength()
        {
            // 10 * 1.5 == 15, which covers a 15 Strength requirement.
            Assert.IsTrue(WeaponAttackRating.MeetsRequirements(10, 10, 10, 10, true, 15, 10, 10, 10));
        }

        [Test]
        public void MeetsRequirements_NeverMultipliesDexterity()
        {
            Assert.IsFalse(WeaponAttackRating.MeetsRequirements(10, 10, 10, 10, true, 10, 15, 10, 10));
        }

        [Test]
        public void AttributeBonus_IsZeroWithoutACoefficient()
        {
            Assert.AreEqual(0f, WeaponAttackRating.AttributeBonus(99, 0f));
        }

        [Test]
        public void AttributeBonus_ClampsLevelToNinetyNine()
        {
            Assert.AreEqual(
                WeaponAttackRating.AttributeBonus(99, 1f),
                WeaponAttackRating.AttributeBonus(500, 1f));
        }

        [Test]
        public void AttributeBonus_SaturatesAfterTheFirstSoftCap()
        {
            float atCap = WeaponAttackRating.AttributeBonus(40, 1f);
            float aboveCap = WeaponAttackRating.AttributeBonus(50, 1f);
            Assert.Greater(aboveCap, atCap);
            Assert.Less(aboveCap - atCap, 0.15f);
        }

        [Test]
        public void ResolveChannel_SumsEveryAttributeContribution()
        {
            var channel = new DamageChannel(100f, 0.5f, 0.5f, 0.5f, 0.5f);
            float expected =
                WeaponAttackRating.AttributeBonus(20, 0.5f) +
                WeaponAttackRating.AttributeBonus(30, 0.5f) +
                WeaponAttackRating.AttributeBonus(40, 0.5f) +
                WeaponAttackRating.AttributeBonus(50, 0.5f);
            Assert.AreEqual(expected, WeaponAttackRating.ResolveChannel(channel, 20, 30, 40, 50),
                0.0001f);
        }

        [Test]
        public void ResolveChannel_IgnoresAttributesWithoutScaling()
        {
            // Fire on one weapon can be STR/FAI while Lightning on another is DEX only.
            var fire = new DamageChannel(100f, 0.2f, 0f, 0f, 0.6f);
            var lightning = new DamageChannel(100f, 0f, 0.7f, 0f, 0.2f);
            Assert.AreEqual(
                WeaponAttackRating.AttributeBonus(10, 0.2f) +
                WeaponAttackRating.AttributeBonus(60, 0.6f),
                WeaponAttackRating.ResolveChannel(fire, 10, 10, 10, 60), 0.0001f);
            Assert.AreEqual(
                WeaponAttackRating.AttributeBonus(10, 0.7f) +
                WeaponAttackRating.AttributeBonus(60, 0.2f),
                WeaponAttackRating.ResolveChannel(lightning, 10, 10, 10, 60), 0.0001f);
        }
    }
}
