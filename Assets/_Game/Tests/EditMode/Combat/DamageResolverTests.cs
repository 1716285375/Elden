using NUnit.Framework;
using UnityEngine;

namespace ZZ.Combat.Tests
{
    [TestFixture]
    public sealed class DamageResolverTests
    {
        [Test]
        public void ResolveChannel_WithNoAttackPower_DealsNothing()
        {
            Assert.AreEqual(0f, DamageResolver.ResolveChannel(0f, 100f, 0f));
        }

        [Test]
        public void ResolveChannel_WithNegativeAttackPower_ClampsToZero()
        {
            Assert.AreEqual(0f, DamageResolver.ResolveChannel(-50f, 0f, 0f));
        }

        [Test]
        public void ResolveChannel_WithNoDefenseOrAbsorption_DealsFullPower()
        {
            Assert.AreEqual(100f, DamageResolver.ResolveChannel(100f, 0f, 0f), 0.0001f);
        }

        [Test]
        public void ResolveChannel_WithEqualDefense_HalvesThePower()
        {
            // power^2 / (power + defense) == 10000 / 200 == 50
            Assert.AreEqual(50f, DamageResolver.ResolveChannel(100f, 100f, 0f), 0.0001f);
        }

        [Test]
        public void ResolveChannel_AppliesAbsorptionAfterDefense()
        {
            Assert.AreEqual(25f, DamageResolver.ResolveChannel(100f, 100f, 50f), 0.0001f);
        }

        [Test]
        public void ResolveChannel_ClampsAbsorptionAboveOneHundred()
        {
            Assert.AreEqual(0f, DamageResolver.ResolveChannel(100f, 0f, 500f), 0.0001f);
        }

        [Test]
        public void ResolveChannel_ClampsNegativeAbsorption()
        {
            Assert.AreEqual(100f, DamageResolver.ResolveChannel(100f, 0f, -80f), 0.0001f);
        }

        [Test]
        public void ResolveChannel_ScalesMonotonicallyWithPower()
        {
            float weak = DamageResolver.ResolveChannel(50f, 80f, 10f);
            float strong = DamageResolver.ResolveChannel(150f, 80f, 10f);
            Assert.Greater(strong, weak);
        }
    }
}
