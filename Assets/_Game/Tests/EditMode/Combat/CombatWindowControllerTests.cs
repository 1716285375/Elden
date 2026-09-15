using NUnit.Framework;

namespace ZZ.Combat.Tests
{
    [TestFixture]
    public sealed class CombatWindowControllerTests
    {
        [Test]
        public void IsOpen_WithNoWindows_ReturnsFalse()
        {
            var windows = new CombatWindowController();
            Assert.IsFalse(windows.IsOpen(CombatWindowType.Hit));
        }

        [Test]
        public void IsOpen_NeverMatchesNone()
        {
            var windows = new CombatWindowController();
            windows.Set(CombatWindowType.Hit, true);
            Assert.IsFalse(windows.IsOpen(CombatWindowType.None));
        }

        [Test]
        public void Set_KeepsOtherWindowsIndependent()
        {
            var windows = new CombatWindowController();
            windows.Set(CombatWindowType.Hit, true);
            windows.Set(CombatWindowType.HyperArmor, true);
            windows.Set(CombatWindowType.Hit, false);
            Assert.IsFalse(windows.IsOpen(CombatWindowType.Hit));
            Assert.IsTrue(windows.IsOpen(CombatWindowType.HyperArmor));
        }

        [Test]
        public void Clear_ClosesEveryWindow()
        {
            var windows = new CombatWindowController();
            windows.Set(CombatWindowType.Hit, true);
            windows.Set(CombatWindowType.Combo, true);
            windows.Clear();
            Assert.AreEqual(CombatWindowType.None, windows.OpenWindows);
        }

        [Test]
        public void Rotation_CanOpenWhileHitStaysClosed()
        {
            // Windows must never be derived from one another.
            var windows = new CombatWindowController();
            windows.Set(CombatWindowType.Rotation, true);
            Assert.IsTrue(windows.IsOpen(CombatWindowType.Rotation));
            Assert.IsFalse(windows.IsOpen(CombatWindowType.Hit));
        }
    }
}
