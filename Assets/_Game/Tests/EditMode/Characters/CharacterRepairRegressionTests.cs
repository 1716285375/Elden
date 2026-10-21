using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ZZ.Tests
{
    public sealed class CharacterRepairRegressionTests
    {
        private const string k_Controller = "Assets/_Game/Art/Characters/Shared/Humanoid/AnimationControllers/Runtime/Humanoid Runtime.controller";
        private const string k_Undead = "Assets/_Game/Prefabs/Characters/AI/Undead AI.prefab";

        [Test]
        public void EveryEquippableWeaponKeepsTheGameplayStateMachine()
        {
            AnimatorController expected = AssetDatabase.LoadAssetAtPath<AnimatorController>(k_Controller);
            foreach (string guid in AssetDatabase.FindAssets("t:WeaponItem", new[] { "Assets/_Game/Data/Items/Weapons" }))
            {
                UnityEngine.Object weapon = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                var controller = new SerializedObject(weapon).FindProperty("m_weaponAnimator")
                    .objectReferenceValue as AnimatorOverrideController;
                Assert.That(controller, Is.Not.Null, weapon.name);
                Assert.That(controller.runtimeAnimatorController, Is.EqualTo(expected),
                    weapon.name + " must preserve gameplay layers and event states when equipped.");
            }
        }

        [Test]
        public void BossIdentityIsConfiguredBeforeNetworkSpawn()
        {
            GameObject root = PrefabUtility.LoadPrefabContents("Assets/_Game/Prefabs/Characters/AI/Fallen Watcher Boss.prefab");
            try
            {
                Component boss = root.GetComponent("BossCharacterManager");
                boss.GetType().GetMethod("ConfigureEncounterIdentity").Invoke(boss, new object[] { 1103, "Catacombs Warden" });
                Assert.That(boss.GetType().GetProperty("BossID").GetValue(boss), Is.EqualTo(1103));
                Assert.That(boss.GetType().GetProperty("BossName").GetValue(boss), Is.EqualTo("Catacombs Warden"));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [TestCase("Action Override", "Dead_01", null)]
        [TestCase("Action Override", "Bow_Draw", "DrawProjectile")]
        [TestCase("Action Override", "Bow_Fire", "ReleaseArrow")]
        [TestCase("Action Override", "Bow_Aim", null)]
        [TestCase("Upper Body Override", "Drink 01", "SuccessfullyUseQuickSlotItem")]
        [TestCase("Upper Body Override", "Drink 02", "SuccessfullyUseQuickSlotItem")]
        public void GameplayStatesContainBodyMotionAndSingleGameplayEvent(string layerName, string stateName, string eventName)
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(k_Controller);
            var clip = controller.layers.Single(layer => layer.name == layerName).stateMachine.states
                .Single(state => state.state.name == stateName).state.motion as AnimationClip;
            Assert.That(clip, Is.Not.Null);
            Assert.That(clip.humanMotion, Is.True, stateName + " must animate the body, not a UI or bow object.");
            Assert.That(AnimationUtility.GetCurveBindings(clip).Length, Is.GreaterThan(50));
            AnimationEvent[] events = AnimationUtility.GetAnimationEvents(clip);
            if (eventName != null)
            {
                Assert.That(events.Count(item => item.functionName == eventName), Is.EqualTo(1));
            }
            Assert.That(events.All(item => item.time >= 0f && item.time <= clip.length), Is.True);
            Assert.That(events.Any(item => item.functionName == "FireProjectile"), Is.False,
                "Imported fire events must not duplicate the runtime ReleaseArrow event.");
        }

        [Test]
        public void ZeroHealthBeforeStatsInitializationDoesNotPlayDeath()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(k_Undead);
            Component network = root.GetComponent("AICharacterNetworkManager");
            try
            {
                Component character = root.GetComponent("AICharacterManager");
                Type baseNetwork = Type.GetType("ZZ.CharacterNetworkManager, Assembly-CSharp");
                baseNetwork.GetField("m_characterManager", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(network, character);
                typeof(NetworkBehaviour).GetProperty("IsSpawned").SetValue(network, true);
                baseNetwork.GetMethod("CheckHP").Invoke(network, null);
                Assert.That(character.GetType().GetProperty("IsDead").GetValue(character), Is.False);
                Type baseCharacter = Type.GetType("ZZ.CharacterManager, Assembly-CSharp");
                Assert.That(baseCharacter.GetField("m_hasPlayedDeathAnimation", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(character), Is.False);
            }
            finally
            {
                typeof(NetworkBehaviour).GetProperty("IsSpawned").SetValue(network, false);
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [Test]
        public void MeleePursuitStopsInsideAnAvailableAttackRange()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(k_Undead);
            try
            {
                Component character = root.GetComponent("AICharacterManager");
                float distance = (float)character.GetType().GetProperty("MinimumDistanceToEndPursuit",
                    BindingFlags.NonPublic | BindingFlags.Instance).GetValue(character);
                UnityEngine.Object attack = new SerializedObject(character).FindProperty("m_defaultAttackAction")
                    .objectReferenceValue;
                Assert.That(attack.GetType().GetMethod("IsInRange").Invoke(attack, new object[] { distance }), Is.True);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
