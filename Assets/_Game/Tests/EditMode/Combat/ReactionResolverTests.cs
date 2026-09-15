using NUnit.Framework;
using UnityEngine;

namespace ZZ.Combat.Tests
{
    [TestFixture]
    public sealed class ReactionResolverTests
    {
        [Test]
        public void ResolveReaction_ReportsDeathFirst()
        {
            Assert.AreEqual(ReactionType.Death,
                ReactionResolver.ResolveReaction(ReactionProfile.Default, 100f, true, true));
        }

        [Test]
        public void ResolveReaction_UsesTheAuthoredPoiseBreakReaction()
        {
            Assert.AreEqual(ReactionType.MediumStagger,
                ReactionResolver.ResolveReaction(ReactionProfile.Default, 10f, true, false));
        }

        [Test]
        public void ResolveReaction_ScalesWithPoiseDamage()
        {
            // ReactionProfile.Default contributes 10 reaction power before the hit's poise damage.
            ReactionProfile profile = ReactionProfile.Default;
            Assert.AreEqual(ReactionType.Ping,
                ReactionResolver.ResolveReaction(profile, 0f, false, false));
            Assert.AreEqual(ReactionType.LightStagger,
                ReactionResolver.ResolveReaction(profile, 10f, false, false));
            Assert.AreEqual(ReactionType.MediumStagger,
                ReactionResolver.ResolveReaction(profile, 30f, false, false));
            Assert.AreEqual(ReactionType.HeavyStagger,
                ReactionResolver.ResolveReaction(profile, 60f, false, false));
        }

        [Test]
        public void ResolveReaction_LaunchRequiresTheLaunchFlag()
        {
            Assert.AreEqual(ReactionType.HeavyStagger,
                ReactionResolver.ResolveReaction(ReactionProfile.Default, 200f, false, false));
        }

        [Test]
        public void GetDamageDirection_ClassifiesEachSide()
        {
            Assert.AreEqual(DamageDirection.Back, ReactionResolver.GetDamageDirection(0f));
            Assert.AreEqual(DamageDirection.Left, ReactionResolver.GetDamageDirection(90f));
            Assert.AreEqual(DamageDirection.Right, ReactionResolver.GetDamageDirection(-90f));
            Assert.AreEqual(DamageDirection.Front, ReactionResolver.GetDamageDirection(180f));
        }

        [Test]
        public void CalculateHitAngle_IsZeroForMissingTransforms()
        {
            Assert.AreEqual(0f, ReactionResolver.CalculateHitAngle(null, null));
        }
    }
}
