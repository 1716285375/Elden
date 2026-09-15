using NUnit.Framework;
using UnityEngine;

namespace ZZ.Combat.Tests
{
    [TestFixture]
    public sealed class CombatCommandBufferTests
    {
        [Test]
        public void Push_IgnoresTheNoneCommand()
        {
            var buffer = new CombatCommandBuffer();
            buffer.Push(CombatCommand.None);
            Assert.AreEqual(0, buffer.Count);
        }

        [Test]
        public void Push_KeepsCommandsUntilTheyAreConsumed()
        {
            var buffer = new CombatCommandBuffer();
            buffer.Push(CombatCommand.LightAttack);
            Assert.IsTrue(buffer.HasPending(CombatCommand.LightAttack));
        }

        [Test]
        public void NestedConsumptionDoesNotExecuteTheSamePressAgain()
        {
            var buffer = new CombatCommandBuffer();
            buffer.Push(CombatCommand.LightAttack);
            int executions = 0;
            bool consumed = buffer.TryConsumeFirstExecutable(command =>
            {
                executions++;
                Assert.That(buffer.TryConsumeFirstExecutable(nested =>
                {
                    executions++;
                    return true;
                }, out _), Is.False);
                return true;
            }, out _);
            Assert.That(consumed, Is.True);
            Assert.That(executions, Is.EqualTo(1));
            Assert.That(buffer.Count, Is.Zero);
        }

        [Test]
        public void ClearingDuringExecutionDoesNotReplayCancelledCommands()
        {
            var buffer = new CombatCommandBuffer();
            buffer.Push(CombatCommand.Dodge);
            buffer.Push(CombatCommand.LightAttack);
            int executions = 0;
            Assert.That(buffer.TryConsumeFirstExecutable(command =>
            {
                executions++;
                buffer.Clear();
                return false;
            }, out _), Is.False);
            Assert.That(executions, Is.EqualTo(1));
        }

        [Test]
        public void ExecutionExceptionReleasesTheConsumptionGuard()
        {
            var buffer = new CombatCommandBuffer();
            buffer.Push(CombatCommand.LightAttack);
            Assert.Throws<System.InvalidOperationException>(() =>
                buffer.TryConsumeFirstExecutable(command => throw new System.InvalidOperationException(), out _));
            Assert.That(buffer.TryConsumeFirstExecutable(command => true, out _), Is.True);
            Assert.That(buffer.Count, Is.Zero);
        }

        [Test]
        public void ClearingAndRequeuingDuringExecutionPreservesTheNewPress()
        {
            var buffer = new CombatCommandBuffer();
            buffer.Push(CombatCommand.LightAttack);
            Assert.That(buffer.TryConsumeFirstExecutable(command =>
            {
                buffer.Clear();
                buffer.Push(CombatCommand.LightAttack);
                return true;
            }, out _), Is.True);
            Assert.That(buffer.Count, Is.EqualTo(1));
        }

        [Test]
        public void TryConsume_RemovesOnlyOneInstance()
        {
            var buffer = new CombatCommandBuffer();
            buffer.Push(CombatCommand.LightAttack);
            buffer.Push(CombatCommand.LightAttack);
            Assert.IsTrue(buffer.TryConsume(CombatCommand.LightAttack));
            Assert.IsTrue(buffer.HasPending(CombatCommand.LightAttack));
        }

        [Test]
        public void TryConsume_ReturnsFalseWhenAbsent()
        {
            var buffer = new CombatCommandBuffer();
            Assert.IsFalse(buffer.TryConsume(CombatCommand.HeavyAttack));
        }

        [Test]
        public void TryPeek_PrefersDodgeOverAttacks()
        {
            var buffer = new CombatCommandBuffer();
            buffer.Push(CombatCommand.LightAttack);
            buffer.Push(CombatCommand.Dodge);
            Assert.IsTrue(buffer.TryPeek(out BufferedCommand buffered));
            Assert.AreEqual(CombatCommand.Dodge, buffered.Command);
        }

        [Test]
        public void TryPeek_PrefersHeavyOverLight()
        {
            var buffer = new CombatCommandBuffer();
            buffer.Push(CombatCommand.LightAttack);
            buffer.Push(CombatCommand.HeavyAttack);
            Assert.IsTrue(buffer.TryPeek(out BufferedCommand buffered));
            Assert.AreEqual(CombatCommand.HeavyAttack, buffered.Command);
        }

        [Test]
        public void TryConsumeHighest_RemovesTheWinningCommand()
        {
            var buffer = new CombatCommandBuffer();
            buffer.Push(CombatCommand.LightAttack);
            buffer.Push(CombatCommand.Dodge);
            Assert.IsTrue(buffer.TryConsumeHighest(out CombatCommand command));
            Assert.AreEqual(CombatCommand.Dodge, command);
            Assert.IsTrue(buffer.HasPending(CombatCommand.LightAttack));
        }

        [Test]
        public void RemoveExpired_DropsCommandsPastTheirLifetime()
        {
            var buffer = new CombatCommandBuffer { Lifetime = -1f };
            buffer.Push(CombatCommand.LightAttack);
            buffer.RemoveExpired(Time.time);
            Assert.AreEqual(0, buffer.Count);
        }

        [Test]
        public void TryConsumeFirstExecutable_SkipsIllegalHigherPriorityCommands()
        {
            // An illegal Dodge must never starve a legal LightAttack.
            var buffer = new CombatCommandBuffer();
            buffer.Push(CombatCommand.LightAttack);
            buffer.Push(CombatCommand.Dodge);
            Assert.IsTrue(buffer.TryConsumeFirstExecutable(
                command => command == CombatCommand.LightAttack, out CombatCommand consumed));
            Assert.AreEqual(CombatCommand.LightAttack, consumed);
            Assert.IsTrue(buffer.HasPending(CombatCommand.Dodge));
        }

        [Test]
        public void TryConsumeFirstExecutable_PrefersTheHighestPriorityThatCanRun()
        {
            var buffer = new CombatCommandBuffer();
            buffer.Push(CombatCommand.LightAttack);
            buffer.Push(CombatCommand.HeavyAttack);
            Assert.IsTrue(buffer.TryConsumeFirstExecutable(_ => true, out CombatCommand consumed));
            Assert.AreEqual(CombatCommand.HeavyAttack, consumed);
        }

        [Test]
        public void TryConsumeFirstExecutable_KeepsCommandsWhenNoneCanRun()
        {
            var buffer = new CombatCommandBuffer();
            buffer.Push(CombatCommand.LightAttack);
            Assert.IsFalse(buffer.TryConsumeFirstExecutable(_ => false, out CombatCommand consumed));
            Assert.AreEqual(CombatCommand.None, consumed);
            Assert.IsTrue(buffer.HasPending(CombatCommand.LightAttack));
        }

        [Test]
        public void Push_RejectsHeldIntentCommands()
        {
            // Guard is held state, not an edge command: buffering it would raise a released shield.
            var buffer = new CombatCommandBuffer();
            buffer.Push(CombatCommand.Guard);
            Assert.AreEqual(0, buffer.Count);
        }

        [Test]
        public void Clear_EmptiesTheBuffer()
        {
            var buffer = new CombatCommandBuffer();
            buffer.Push(CombatCommand.LightAttack);
            buffer.Clear();
            Assert.AreEqual(0, buffer.Count);
        }
    }
}
