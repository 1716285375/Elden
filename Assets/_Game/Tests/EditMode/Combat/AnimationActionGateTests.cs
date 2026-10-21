using NUnit.Framework;

namespace ZZ.Tests
{
    public class AnimationActionGateTests
    {
        [Test]
        public void OutgoingEmptyCannotCompleteAnUnobservedAttack()
        {
            var gate = new AnimationActionGate();
            gate.Request(100);
            gate.Observe(50, 0);
            Assert.That(gate.CanComplete, Is.False);
            gate.Observe(50, 100);
            Assert.That(gate.CanComplete, Is.True);
        }

        [Test]
        public void ANewComboMustBeObservedIndependentlyOfThePreviousSwing()
        {
            var gate = new AnimationActionGate();
            gate.Request(100);
            gate.Observe(100, 0);
            gate.Request(200);
            gate.Observe(100, 50);
            Assert.That(gate.CanComplete, Is.False);
            gate.Observe(200, 0);
            Assert.That(gate.CanComplete, Is.True);
        }

        [Test]
        public void InterruptionReleasesThePresentationGate()
        {
            var gate = new AnimationActionGate();
            gate.Request(100);
            gate.Cancel();
            Assert.That(gate.CanComplete, Is.True);
        }
    }
}
