using NUnit.Framework;

namespace ZZ.Combat.Tests
{
    [TestFixture]
    public sealed class ActionWindowTests
    {
        [Test]
        public void Contains_MatchesTheHalfOpenRange()
        {
            var window = new ActionWindow(0.2f, 0.4f);
            Assert.IsFalse(window.Contains(0.19f));
            Assert.IsTrue(window.Contains(0.2f));
            Assert.IsTrue(window.Contains(0.39f));
            Assert.IsFalse(window.Contains(0.4f));
        }

        [Test]
        public void IsOpen_IsFalseForAnEmptyWindow()
        {
            Assert.IsFalse(ActionWindow.None.IsOpen);
            Assert.IsFalse(new ActionWindow(0.5f, 0.5f).IsOpen);
        }

        [Test]
        public void Contains_IgnoresNonFiniteTime()
        {
            var window = new ActionWindow(0f, 1f);
            Assert.IsFalse(window.Contains(float.NaN));
            Assert.IsFalse(window.Contains(float.PositiveInfinity));
        }

        [Test]
        public void Dodge_InvulnerabilityCanStartBeforeTheHitWindow()
        {
            // Hyper armor and invulnerability are authored independently of the hit window.
            var hyperArmor = new ActionWindow(0.25f, 0.6f);
            var hit = new ActionWindow(0.4f, 0.52f);
            Assert.IsTrue(hyperArmor.Contains(0.3f));
            Assert.IsFalse(hit.Contains(0.3f));
        }
    }
}
