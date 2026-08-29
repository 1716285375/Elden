using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ZZ.Tests
{
    public class StatusEffectsSystemTests
    {
        private const string k_TakePoisonPath =
            "Assets/_Game/Resources/Effects/Take Poison Buildup Effect.asset";
        private const string k_TakeBleedPath =
            "Assets/_Game/Resources/Effects/Take Bleed Buildup Effect.asset";
        private const string k_DegradePoisonPath =
            "Assets/_Game/Resources/Effects/Degrade Poison Buildup Effect.asset";
        private const string k_DegradeBleedPath =
            "Assets/_Game/Resources/Effects/Degrade Bleed Buildup Effect.asset";
        private const string k_BuildupBarPrefabPath =
            "Assets/_Game/Prefabs/UI/Buildup Bar.prefab";
        private const string k_PlayerUIPrefabPath =
            "Assets/_Game/Prefabs/World/Managers/Player UI Manager.prefab";

        [Test]
        public void VitalityCalculatesSharedBuildupCapacity()
        {
            Type statsType = GetRuntimeType("ZZ.CharacterStatsManager");
            MethodInfo calculate = statsType.GetMethod(
                "CalculateBuildupCapacityBasedOnVitalityLevel",
                BindingFlags.Public | BindingFlags.Static);

            Assert.That(calculate, Is.Not.Null);
            Assert.That((float)calculate.Invoke(null, new object[] { 10 }),
                Is.EqualTo(32.5f).Within(0.001f));
            Assert.That((float)calculate.Invoke(null, new object[] { -2 }),
                Is.Zero);
        }

        [Test]
        public void DecayStopsOnlyAtZeroOrCapacity()
        {
            Type effectType = GetRuntimeType("ZZ.BuildupEffect");
            MethodInfo shouldStop = effectType.GetMethod(
                "ShouldStopDegrading",
                BindingFlags.Public | BindingFlags.Static);

            Assert.That(shouldStop.Invoke(null, new object[] { 0f, 100f }),
                Is.True);
            Assert.That(shouldStop.Invoke(null, new object[] { 100f, 100f }),
                Is.True);
            Assert.That(shouldStop.Invoke(null, new object[] { 25f, 100f }),
                Is.False);
        }

        [Test]
        public void AuthoredTakeEffectsReferenceMatchingDecayAssets()
        {
            UnityEngine.Object takePoison = LoadRequiredAsset(k_TakePoisonPath);
            UnityEngine.Object takeBleed = LoadRequiredAsset(k_TakeBleedPath);
            UnityEngine.Object degradePoison = LoadRequiredAsset(k_DegradePoisonPath);
            UnityEngine.Object degradeBleed = LoadRequiredAsset(k_DegradeBleedPath);

            Assert.That(GetProperty(takePoison, "BuildupType").ToString(),
                Is.EqualTo("Poison"));
            Assert.That(GetProperty(takeBleed, "BuildupType").ToString(),
                Is.EqualTo("Bleed"));
            Assert.That(GetProperty(takePoison, "DegradeBuildupEffect"),
                Is.SameAs(degradePoison));
            Assert.That(GetProperty(takeBleed, "DegradeBuildupEffect"),
                Is.SameAs(degradeBleed));
            Assert.That(GetProperty<int>(degradePoison, "TimedEffectID"),
                Is.Not.EqualTo(GetProperty<int>(degradeBleed, "TimedEffectID")));
        }

        [Test]
        public void TimedRuntimeCopyOwnsIndependentDuration()
        {
            UnityEngine.Object template = LoadRequiredAsset(k_DegradePoisonPath);
            MethodInfo createRuntime = template.GetType().GetMethod(
                "CreateRuntimeInstance",
                BindingFlags.Public | BindingFlags.Instance);
            UnityEngine.Object runtime =
                (UnityEngine.Object)createRuntime.Invoke(template, null);
            try
            {
                float defaultDuration = GetProperty<float>(
                    runtime,
                    "DefaultTimeLengthOnEffect");
                runtime.GetType().GetMethod("AdvanceTime")
                    .Invoke(runtime, new object[] { 5f });

                Assert.That(runtime, Is.Not.SameAs(template));
                Assert.That(runtime.hideFlags & HideFlags.DontSave, Is.Not.Zero);
                Assert.That(GetProperty<float>(runtime, "TimeRemainingOnEffect"),
                    Is.EqualTo(defaultDuration - 5f).Within(0.001f));
                Assert.That(GetProperty<float>(template, "TimeRemainingOnEffect"),
                    Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(runtime);
            }
        }

        [Test]
        public void DuplicateTimedEffectRefreshesInsteadOfStacking()
        {
            Type managerType = GetRuntimeType("ZZ.CharacterEffectsManager");
            GameObject character = new("Timed Effect Test Character");
            Component manager = character.AddComponent(managerType);
            UnityEngine.Object template = LoadRequiredAsset(k_DegradePoisonPath);
            MethodInfo addEffect = managerType.GetMethod(
                "AddTimedEffect",
                BindingFlags.Public | BindingFlags.Instance);
            try
            {
                object first = addEffect.Invoke(manager, new object[] { template });
                first.GetType().GetMethod("AdvanceTime")
                    .Invoke(first, new object[] { 5f });
                object refreshed = addEffect.Invoke(
                    manager,
                    new object[] { template });
                int effectCount = ((IEnumerable)GetProperty(
                        manager,
                        "TimedEffects"))
                    .Cast<object>()
                    .Count();

                Assert.That(refreshed, Is.SameAs(first));
                Assert.That(effectCount, Is.EqualTo(1));
                Assert.That(GetProperty<float>(refreshed, "TimeRemainingOnEffect"),
                    Is.EqualTo(GetProperty<float>(
                        refreshed,
                        "DefaultTimeLengthOnEffect")).Within(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(character);
            }
        }

        [Test]
        public void ReusableBuildupBarStartsHiddenAndTogglesWithValue()
        {
            GameObject template = AssetDatabase.LoadAssetAtPath<GameObject>(
                k_BuildupBarPrefabPath);
            Assert.That(template, Is.Not.Null);
            Assert.That(template.activeSelf, Is.False);
            Assert.That(template.GetComponent<Slider>(), Is.Not.Null);

            GameObject instance = UnityEngine.Object.Instantiate(template);
            try
            {
                Component bar = instance.GetComponent(
                    GetRuntimeType("ZZ.UIBuildupBar"));
                MethodInfo setAmount = bar.GetType().GetMethod(
                    "SetBuildupAmount",
                    BindingFlags.Public | BindingFlags.Instance);
                setAmount.Invoke(bar, new object[] { 20f });
                Assert.That(instance.activeSelf, Is.True);
                Assert.That(instance.GetComponent<Slider>().value,
                    Is.EqualTo(20f).Within(0.001f));
                setAmount.Invoke(bar, new object[] { 0f });
                Assert.That(instance.activeSelf, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void PlayerUIPrefabContainsPoisonAndBleedBarsInPopupOrganizer()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(
                k_PlayerUIPrefabPath);
            try
            {
                Type barType = GetRuntimeType("ZZ.UIBuildupBar");
                Component[] bars = root.GetComponentsInChildren(barType, true);
                Assert.That(bars.Length, Is.GreaterThanOrEqualTo(2));
                Assert.That(bars.All(bar => !bar.gameObject.activeSelf), Is.True);
                Assert.That(bars.All(bar =>
                    bar.transform.parent.name == "Popup Organizer"), Is.True);
                string[] buildupTypes = bars.Select(bar =>
                        GetProperty(bar, "BuildupType").ToString())
                    .ToArray();
                Assert.That(buildupTypes, Does.Contain("Poison"));
                Assert.That(buildupTypes, Does.Contain("Bleed"));

                Component hud = root.GetComponentInChildren(
                    GetRuntimeType("ZZ.PlayerUIHUDManager"),
                    true);
                SerializedObject serializedHUD = new(hud);
                Assert.That(serializedHUD.FindProperty("m_buildupBars").arraySize,
                    Is.GreaterThanOrEqualTo(2));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [Test]
        public void SourcesPreserveOwnerTickAndNetworkUIBoundaries()
        {
            string root = Directory.GetParent(Application.dataPath).FullName;
            string effectsSource = File.ReadAllText(Path.Combine(
                root,
                "Assets/_Game/Scripts/Characters/Common/Effects/CharacterEffectsManager.cs"));
            string playerEffectsSource = File.ReadAllText(Path.Combine(
                root,
                "Assets/_Game/Scripts/Characters/Common/Effects/PlayerEffectsManager.cs"));
            string hudSource = File.ReadAllText(Path.Combine(
                root,
                "Assets/_Game/Scripts/Characters/Player/Player UI/PlayerUIHUDManager.cs"));
            string damageSource = File.ReadAllText(Path.Combine(
                root,
                "Assets/_Game/Scripts/Combat/Damage/DamageCollider.cs"));
            string takeBuildupSource = File.ReadAllText(Path.Combine(
                root,
                "Assets/_Game/Scripts/Combat/Effects/Instant Effects/TakeBuildupEffect.cs"));

            Assert.That(effectsSource, Does.Contain("!m_character.IsOwner"));
            Assert.That(effectsSource, Does.Contain("effect.TimedEffectID"));
            Assert.That(effectsSource, Does.Contain("activeEffect.RefreshDuration()"));
            Assert.That(playerEffectsSource, Does.Contain("base.Update()"));
            Assert.That(hudSource, Does.Contain("PoisonBuildup.OnValueChanged"));
            Assert.That(hudSource, Does.Contain("BleedBuildup.OnValueChanged"));
            Assert.That(damageSource, Does.Contain("ProcessBuildupEffects"));
            Assert.That(takeBuildupSource,
                Does.Contain("RemoveTimedEffect(decayEffect.TimedEffectID)"));
        }

        public static void RunAllFocusedTests()
        {
            StatusEffectsSystemTests tests = new();
            tests.VitalityCalculatesSharedBuildupCapacity();
            tests.DecayStopsOnlyAtZeroOrCapacity();
            tests.AuthoredTakeEffectsReferenceMatchingDecayAssets();
            tests.TimedRuntimeCopyOwnsIndependentDuration();
            tests.DuplicateTimedEffectRefreshesInsteadOfStacking();
            tests.ReusableBuildupBarStartsHiddenAndTogglesWithValue();
            tests.PlayerUIPrefabContainsPoisonAndBleedBarsInPopupOrganizer();
            tests.SourcesPreserveOwnerTickAndNetworkUIBoundaries();
            Debug.Log("[StatusEffectsSystemTests] 8 focused tests passed.");
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
