using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ZZ.Tests
{
    public class PoisonStatusSystemTests
    {
        private const string k_PoisonedEffectPath =
            "Assets/_Game/Resources/Effects/Poisoned Effect.asset";
        private const string k_PoisonedVFXPath =
            "Assets/_Game/Resources/Effects/Poisoned VFX.prefab";
        private const string k_StatusWarningPath =
            "Assets/_Game/Prefabs/UI/Status Effect Warning.prefab";
        private const string k_PlayerUIPrefabPath =
            "Assets/_Game/Prefabs/World/Managers/Player UI Manager.prefab";

        [Test]
        public void PoisonedEffectUsesExpectedDurationDamageAndCatalogID()
        {
            UnityEngine.Object effect = LoadRequiredAsset(k_PoisonedEffectPath);

            Assert.That(GetProperty<int>(effect, "TimedEffectID"), Is.EqualTo(2));
            Assert.That(GetProperty<float>(effect, "DefaultTimeLengthOnEffect"),
                Is.EqualTo(120f).Within(0.001f));
            Assert.That(GetProperty<float>(effect, "PoisonDamage"),
                Is.EqualTo(10f).Within(0.001f));
        }

        [Test]
        public void PoisonDamageNeverProducesNegativeHealth()
        {
            Type effectType = GetRuntimeType("ZZ.PoisonedEffect");
            MethodInfo calculate = effectType.GetMethod(
                "CalculateRemainingHealth",
                BindingFlags.Public | BindingFlags.Static);

            Assert.That(calculate, Is.Not.Null);
            Assert.That((float)calculate.Invoke(null, new object[] { 100f, 10f }),
                Is.EqualTo(90f).Within(0.001f));
            Assert.That((float)calculate.Invoke(null, new object[] { 5f, 10f }),
                Is.Zero);
            Assert.That((float)calculate.Invoke(null, new object[] { 20f, -5f }),
                Is.EqualTo(20f).Within(0.001f));
        }

        [Test]
        public void PoisonedVFXContainsLoopingParticlePresentation()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                k_PoisonedVFXPath);
            Assert.That(prefab, Is.Not.Null);
            ParticleSystem particles = prefab.GetComponent<ParticleSystem>();
            Assert.That(particles, Is.Not.Null);
            Assert.That(particles.main.loop, Is.True);
            Assert.That(particles.main.playOnAwake, Is.True);
        }

        [Test]
        public void StatusWarningPrefabIsReusableAndStartsHidden()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                k_StatusWarningPath);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.activeSelf, Is.False);
            Assert.That(prefab.GetComponent(
                GetRuntimeType("ZZ.UIStatusEffectWarning")), Is.Not.Null);
            Assert.That(prefab.GetComponent<CanvasGroup>(), Is.Not.Null);

            Type warningType = GetRuntimeType("ZZ.UIStatusEffectWarning");
            MethodInfo getText = warningType.GetMethod(
                "GetDisplayText",
                BindingFlags.Public | BindingFlags.Static);
            Type buildupType = GetRuntimeType("ZZ.Buildup");
            object poison = Enum.Parse(buildupType, "Poison");
            object bleed = Enum.Parse(buildupType, "Bleed");
            Assert.That(getText.Invoke(null, new[] { poison }),
                Is.EqualTo("POISONED"));
            Assert.That(getText.Invoke(null, new[] { bleed }),
                Is.EqualTo("BLOOD LOSS"));
        }

        [Test]
        public void PlayerUIPrefabBindsWarningToPopupOrganizer()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(
                k_PlayerUIPrefabPath);
            try
            {
                Component popupManager = root.GetComponentInChildren(
                    GetRuntimeType("ZZ.PlayerUIPopUpManager"),
                    true);
                Assert.That(popupManager, Is.Not.Null);
                SerializedObject serializedPopup = new(popupManager);
                UnityEngine.Object organizer = serializedPopup.FindProperty(
                    "m_popupOrganizer").objectReferenceValue;
                UnityEngine.Object warningPrefab = serializedPopup.FindProperty(
                    "m_statusEffectWarningPrefab").objectReferenceValue;

                Assert.That(organizer, Is.Not.Null);
                Assert.That(organizer.name, Is.EqualTo("Popup Organizer"));
                Assert.That(warningPrefab, Is.SameAs(
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        k_StatusWarningPath).GetComponent(
                            GetRuntimeType("ZZ.UIStatusEffectWarning"))));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [Test]
        public void SourcesPreserveOwnerAuthorityLateJoinAndCriticalDeath()
        {
            string root = Directory.GetParent(Application.dataPath).FullName;
            string networkSource = ReadSource(
                root,
                "Assets/_Game/Scripts/Characters/Common/CharacterNetworkManager.cs");
            string effectsSource = ReadSource(
                root,
                "Assets/_Game/Scripts/Characters/Common/Effects/CharacterEffectsManager.cs");
            string poisonSource = ReadSource(
                root,
                "Assets/_Game/Scripts/Combat/Effects/TimedEffects/PoisonedEffect.cs");

            Assert.That(networkSource, Does.Contain(
                "NetworkVariable<bool> IsPoisoned"));
            Assert.That(networkSource, Does.Contain(
                "IsPoisoned.OnValueChanged += OnIsPoisonedChanged"));
            Assert.That(networkSource, Does.Contain(
                "IsPoisoned.OnValueChanged -= OnIsPoisonedChanged"));
            Assert.That(networkSource, Does.Contain(
                "OnIsPoisonedChanged(false, IsPoisoned.Value)"));
            Assert.That(networkSource, Does.Contain(
                "player.IsOwner"));
            Assert.That(effectsSource, Does.Contain("!m_character.IsOwner"));
            Assert.That(networkSource, Does.Contain("IsBeingCriticallyDamaged"),
                "Poison death must rely on the existing critical-safe death path.");
            Assert.That(poisonSource, Does.Contain("character.IsOwner"));
        }

        [Test]
        public void PoisonTriggerClearsBuildupAndSkipsDecayWhileActive()
        {
            string root = Directory.GetParent(Application.dataPath).FullName;
            string effectsSource = ReadSource(
                root,
                "Assets/_Game/Scripts/Characters/Common/Effects/CharacterEffectsManager.cs");
            string takeBuildupSource = ReadSource(
                root,
                "Assets/_Game/Scripts/Combat/Effects/InstantEffects/TakeBuildupEffect.cs");

            Assert.That(effectsSource, Does.Contain(
                "networkManager.TrySetBuildup(Buildup.Poison, 0f)"));
            Assert.That(effectsSource, Does.Contain(
                "networkManager.TrySetPoisoned(true)"));
            Assert.That(effectsSource, Does.Contain(
                "networkManager.IsPoisoned.Value"));
            Assert.That(takeBuildupSource, Does.Contain(
                "networkManager?.IsPoisoned.Value == true"));
        }

        public static void RunAllFocusedTests()
        {
            PoisonStatusSystemTests tests = new();
            tests.PoisonedEffectUsesExpectedDurationDamageAndCatalogID();
            tests.PoisonDamageNeverProducesNegativeHealth();
            tests.PoisonedVFXContainsLoopingParticlePresentation();
            tests.StatusWarningPrefabIsReusableAndStartsHidden();
            tests.PlayerUIPrefabBindsWarningToPopupOrganizer();
            tests.SourcesPreserveOwnerAuthorityLateJoinAndCriticalDeath();
            tests.PoisonTriggerClearsBuildupAndSkipsDecayWhileActive();
            Debug.Log("[PoisonStatusSystemTests] 7 focused tests passed.");
        }

        private static string ReadSource(string root, string relativePath)
        {
            return File.ReadAllText(Path.Combine(root, relativePath));
        }

        private static UnityEngine.Object LoadRequiredAsset(string assetPath)
        {
            UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            Assert.That(asset, Is.Not.Null, $"Missing asset: {assetPath}");
            return asset;
        }

        private static object GetProperty(object target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null, propertyName);
            return property.GetValue(target);
        }

        private static T GetProperty<T>(object target, string propertyName)
        {
            return (T)GetProperty(target, propertyName);
        }

        private static Type GetRuntimeType(string fullName)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(candidate => candidate != null);
            Assert.That(type, Is.Not.Null, fullName);
            return type;
        }
    }
}
