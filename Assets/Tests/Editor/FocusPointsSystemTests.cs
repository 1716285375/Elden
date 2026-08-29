using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ZZ.Tests
{
    public class FocusPointsSystemTests
    {
        private const string k_FireballPath =
            "Assets/_Game/Data/Items/Spells/Fireball.asset";
        private const string k_PlayerPrefabPath =
            "Assets/_Game/Prefabs/Characters/Player/Player.prefab";
        private const string k_UIManagerPrefabPath =
            "Assets/_Game/Prefabs/World/Managers/Player UI Manager.prefab";

        [Test]
        public void MindTenProducesOneHundredFocusPoints()
        {
            Type statsType = Type.GetType(
                "ZZ.CharacterStatsManager, Assembly-CSharp");
            MethodInfo calculateMethod = statsType?.GetMethod(
                "CalculateFocusPointsBasedOnMindLevel",
                BindingFlags.Public | BindingFlags.Static);

            Assert.That(calculateMethod, Is.Not.Null);
            Assert.That(
                calculateMethod.Invoke(null, new object[] { 10 }),
                Is.EqualTo(100f));
            Assert.That(
                calculateMethod.Invoke(null, new object[] { -4 }),
                Is.EqualTo(0f));
        }

        [Test]
        public void PlayerReplicatesOwnerWrittenMindAndFocusResources()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(k_PlayerPrefabPath);
            try
            {
                Component network = root.GetComponents<Component>()
                    .Single(component =>
                        component.GetType().Name == "PlayerNetworkManager");
                AssertOwnerWrittenNetworkVariable(network, "Mind");
                AssertOwnerWrittenNetworkVariable(network, "CurrentFocusPoints");
                AssertOwnerWrittenNetworkVariable(network, "MaxFocusPoints");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [Test]
        public void FireballUsesAuthoredNormalAndFullChargeResourceCosts()
        {
            UnityEngine.Object fireball = AssetDatabase.LoadMainAssetAtPath(
                k_FireballPath);
            SerializedObject serializedFireball = new SerializedObject(fireball);
            MethodInfo focusCostMethod = fireball.GetType().GetMethod(
                "GetFocusPointsCost");
            MethodInfo staminaCostMethod = fireball.GetType().GetMethod(
                "GetStaminaCost");

            Assert.That(
                serializedFireball.FindProperty("m_focusPointsCost").intValue,
                Is.EqualTo(25));
            Assert.That(
                serializedFireball.FindProperty("m_staminaCost").floatValue,
                Is.EqualTo(25f));
            Assert.That(
                focusCostMethod?.Invoke(fireball, new object[] { false }),
                Is.EqualTo(25));
            Assert.That(
                focusCostMethod?.Invoke(fireball, new object[] { true }),
                Is.EqualTo(35));
            Assert.That(
                staminaCostMethod?.Invoke(fireball, new object[] { true }),
                Is.EqualTo(35f));
        }

        [Test]
        public void HUDContainsBlueFocusBarAndSafeSpellIcon()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(k_UIManagerPrefabPath);
            try
            {
                Component hud = root.GetComponentsInChildren<Component>(true)
                    .Single(component =>
                        component.GetType().Name == "PlayerUIHUDManager");
                SerializedObject serializedHUD = new SerializedObject(hud);
                Component focusBar = serializedHUD
                    .FindProperty("m_focusPointsBar")
                    .objectReferenceValue as Component;
                Component spellSlot = serializedHUD
                    .FindProperty("m_spellQuickSlot")
                    .objectReferenceValue as Component;
                SerializedObject serializedSpellSlot = new SerializedObject(spellSlot);
                Image icon = serializedSpellSlot
                    .FindProperty("m_iconImage")
                    .objectReferenceValue as Image;
                Slider focusSlider = focusBar?.GetComponent<Slider>();
                Image fillImage = focusSlider?.fillRect?.GetComponent<Image>();

                Assert.That(focusBar, Is.Not.Null);
                Assert.That(focusBar.gameObject.name, Is.EqualTo("Focus Point Bar"));
                Assert.That(focusSlider.maxValue, Is.EqualTo(100f));
                Assert.That(fillImage, Is.Not.Null);
                Assert.That(fillImage.color.b, Is.GreaterThan(fillImage.color.r));
                Assert.That(icon, Is.Not.Null);
                Assert.That(icon.preserveAspect, Is.True);
                Assert.That(icon.raycastTarget, Is.False);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void AssertOwnerWrittenNetworkVariable(
            Component network,
            string propertyName)
        {
            object networkVariable = network.GetType()
                .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)
                ?.GetValue(network);
            networkVariable ??= network.GetType()
                .GetField(propertyName, BindingFlags.Instance | BindingFlags.Public)
                ?.GetValue(network);
            object writePermission = networkVariable?.GetType()
                .GetProperty("WritePerm")
                ?.GetValue(networkVariable);
            Assert.That(writePermission?.ToString(), Is.EqualTo("Owner"));
        }
    }
}
