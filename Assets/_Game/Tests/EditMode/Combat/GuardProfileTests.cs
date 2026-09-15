using NUnit.Framework;
using UnityEngine;

namespace ZZ.Combat.Tests
{
    [TestFixture]
    public sealed class GuardProfileTests
    {
        [Test]
        public void MinimumBlockingDot_HalvesTheAuthoredArc()
        {
            GuardProfile guard = ScriptableObject.CreateInstance<GuardProfile>();
            Assert.AreEqual(120f, guard.TotalGuardArcDegrees, 0.001f);
            // A 120 degree total arc accepts anything within 60 degrees of forward: cos(60) = 0.5.
            Assert.AreEqual(0.5f, guard.MinimumBlockingDot, 0.0001f);
            Assert.AreEqual(Mathf.Cos(guard.TotalGuardArcDegrees * 0.5f * Mathf.Deg2Rad),
                guard.MinimumBlockingDot, 0.0001f);
        }

        [Test]
        public void GetAbsorption_ReturnsOneValuePerChannel()
        {
            GuardProfile guard = ScriptableObject.CreateInstance<GuardProfile>();
            Assert.AreEqual(guard.PhysicalAbsorption, guard.GetAbsorption(DamageType.Physical));
            Assert.AreEqual(guard.FireAbsorption, guard.GetAbsorption(DamageType.Fire));
            Assert.AreEqual(guard.LightningAbsorption, guard.GetAbsorption(DamageType.Lightning));
        }
    }
}
