using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ZZ.Tests
{
    public class StanceBreakingSystemTests
    {
        private const string k_UndeadPrefabPath =
            "Assets/_Game/Prefabs/Characters/AI/Undead AI.prefab";
        private const string k_StanceBreakClipPath =
            "Assets/Art/Animations/Characters/Humanoid/Actions/" +
            "core_main_stance_broken_f_01.anim";

        [Test]
        public void SharedPoiseClassifierIdentifiesColossalDamage()
        {
            Type utilityType = GetRuntimeType("ZZ.WorldUtilityManager");
            MethodInfo classifier = utilityType.GetMethod(
                "GetDamageIntensityBasedOnPoiseDamage",
                BindingFlags.Public | BindingFlags.Static);

            Assert.That(classifier, Is.Not.Null);
            Assert.That(classifier.Invoke(null, new object[] { 0f }).ToString(),
                Is.EqualTo("Ping"));
            Assert.That(classifier.Invoke(null, new object[] { 70f }).ToString(),
                Is.EqualTo("Heavy"));
            Assert.That(classifier.Invoke(null, new object[] { 120f }).ToString(),
                Is.EqualTo("Colossal"));
        }

        [Test]
        public void StanceRecoveryWaitsForDelayAndClampsToMaximum()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(k_UndeadPrefabPath);
            try
            {
                Component combatManager = root.GetComponent(
                    "AICharacterCombatManager");
                Assert.That(combatManager, Is.Not.Null);
                Type managerType = combatManager.GetType();
                FieldInfo currentStance = GetPrivateField(
                    managerType,
                    "m_currentStance");
                FieldInfo regenerationTimer = GetPrivateField(
                    managerType,
                    "m_stanceRegenerationTimer");
                MethodInfo regenerateStance = managerType.GetMethod(
                    "RegenerateStance",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.That(regenerateStance, Is.Not.Null);
                currentStance.SetValue(combatManager, 70);
                regenerationTimer.SetValue(combatManager, 0.25f);
                regenerateStance.Invoke(combatManager, new object[] { 0.25f });
                Assert.That(currentStance.GetValue(combatManager), Is.EqualTo(70));

                regenerateStance.Invoke(combatManager, new object[] { 1f });
                Assert.That(currentStance.GetValue(combatManager), Is.EqualTo(80));
                regenerateStance.Invoke(combatManager, new object[] { 1f });
                Assert.That(currentStance.GetValue(combatManager), Is.EqualTo(80));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [Test]
        public void StanceBreakClipOpensRiposteWindowAfterSoundFeedback()
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                k_StanceBreakClipPath);
            Assert.That(clip, Is.Not.Null);
            AnimationEvent[] events = AnimationUtility.GetAnimationEvents(clip);
            AnimationEvent soundEvent = Array.Find(events, animationEvent =>
                animationEvent.functionName == "PlayStanceBrokenSoundEffect");
            AnimationEvent riposteEvent = Array.Find(events, animationEvent =>
                animationEvent.functionName == "EnableIsRipostable");

            Assert.That(soundEvent, Is.Not.Null);
            Assert.That(riposteEvent, Is.Not.Null);
            Assert.That(soundEvent.time, Is.GreaterThan(0f));
            Assert.That(riposteEvent.time, Is.GreaterThan(soundEvent.time));
        }

        [Test]
        public void InstantAnimationAndCriticalStateContractsAreAvailable()
        {
            Type animatorType = GetRuntimeType("ZZ.CharacterAnimatorManager");
            Type networkType = GetRuntimeType("ZZ.CharacterNetworkManager");
            Assert.That(
                animatorType.GetMethod("PlayTargetActionAnimationInstantly"),
                Is.Not.Null);
            Assert.That(networkType.GetField("IsRipostable"), Is.Not.Null);
            Assert.That(
                networkType.GetField("IsBeingCriticallyDamaged"),
                Is.Not.Null);
        }

        private static FieldInfo GetPrivateField(Type type, string fieldName)
        {
            FieldInfo field = type.GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Could not resolve {fieldName}.");
            return field;
        }

        private static Type GetRuntimeType(string fullName)
        {
            Type type = Type.GetType($"{fullName}, Assembly-CSharp");
            Assert.That(type, Is.Not.Null, $"Could not resolve {fullName}.");
            return type;
        }
    }
}
