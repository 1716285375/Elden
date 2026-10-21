using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace ZZ.Tests
{
    public sealed class RestResourcePersistenceTests
    {
        private static Type SaveType => AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("ZZ.CharacterSaveData"))
            .First(type => type != null);

        [TestCase(5, 2)]
        [TestCase(0, 4)]
        [TestCase(4, 0)]
        public void DepletedFlasksRetainTheirAllocationAcrossSaveReload(int health, int focus)
        {
            Type type = SaveType;
            object save = Activator.CreateInstance(type);
            type.GetProperty("CurrentHealthFlasksRemaining").SetValue(save, 0);
            type.GetProperty("CurrentFocusPointFlasksRemaining").SetValue(save, 0);
            type.GetProperty("HealthFlaskCapacity").SetValue(save, health);
            type.GetProperty("FocusPointFlaskCapacity").SetValue(save, focus);
            object loaded = JsonUtility.FromJson(JsonUtility.ToJson(save), type);
            Assert.That(type.GetProperty("HealthFlaskCapacity").GetValue(loaded), Is.EqualTo(health));
            Assert.That(type.GetProperty("FocusPointFlaskCapacity").GetValue(loaded), Is.EqualTo(focus));
            Assert.That(type.GetProperty("CurrentHealthFlasksRemaining").GetValue(loaded), Is.EqualTo(0));
        }

        [Test]
        public void LegacyRemainingChargesAreNotReducedByDefaultAllocation()
        {
            Type type = SaveType;
            object save = JsonUtility.FromJson(
                "{\"m_currentHealthFlasksRemaining\":7,\"m_currentFocusPointFlasksRemaining\":4}", type);
            Assert.That(type.GetProperty("HealthFlaskCapacity").GetValue(save), Is.EqualTo(7));
            Assert.That(type.GetProperty("FocusPointFlaskCapacity").GetValue(save), Is.EqualTo(4));
        }

        [Test]
        public void RefillUsesConfiguredAllocationInsteadOfRemainingCharges()
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("ZZ.PlayerNetworkManager")).First(candidate => candidate != null);
            var root = new GameObject("Flask refill regression");
            try
            {
                Component network = root.AddComponent(type);
                type.GetMethod("ConfigureFlaskCapacity").Invoke(network, new object[] { 0, 4 });
                type.GetMethod("RefillFlasks").Invoke(network, null);
                Assert.That(type.GetMethod("GetRemainingFlaskCount").Invoke(network, new object[] { true }), Is.EqualTo(0));
                Assert.That(type.GetMethod("GetRemainingFlaskCount").Invoke(network, new object[] { false }), Is.EqualTo(4));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
