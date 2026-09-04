using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ZZ.Tests
{
    public class FrostbiteSystemTests
    {
        private const string k_TakeFrostPath =
            "Assets/_Game/Resources/Effects/Take Frost Buildup Effect.asset";
        private const string k_DegradeFrostPath =
            "Assets/_Game/Resources/Effects/Degrade Frost Buildup Effect.asset";
        private const string k_StaminaModifierPath =
            "Assets/_Game/Resources/Effects/Frostbite Stamina Regeneration Modifier.asset";
        private const string k_FrostbiteEffectPath =
            "Assets/_Game/Resources/Effects/Frostbite Effect.asset";
        private const string k_FrostbiteVFXPath =
            "Assets/_Game/Resources/Effects/Frostbite VFX.prefab";
        private const string k_FrozenMaterialPath =
            "Assets/_Game/Resources/Effects/Frozen Material.mat";
        private const string k_PlayerUIPrefabPath =
            "Assets/_Game/Prefabs/World/Managers/Player UI Manager.prefab";

        [Test]
        public void FrostUsesIndependentBuildupChannel()
        {
            Type buildupType = GetRuntimeType("ZZ.Buildup");
            Assert.That(Convert.ToInt32(Enum.Parse(buildupType, "Poison")),
                Is.EqualTo(0));
            Assert.That(Convert.ToInt32(Enum.Parse(buildupType, "Bleed")),
                Is.EqualTo(1));
            Assert.That(Convert.ToInt32(Enum.Parse(buildupType, "Frost")),
                Is.EqualTo(2));
        }

        [Test]
        public void StaminaRegenerationAppliesSignedPercentageModifier()
        {
            MethodInfo calculate = GetRuntimeType("ZZ.CharacterStatsManager")
                .GetMethod(
                    "CalculateStaminaRegenerationAmount",
                    BindingFlags.Public | BindingFlags.Static);
            Assert.That(calculate, Is.Not.Null);
            Assert.That((float)calculate.Invoke(null, new object[] { 2f, -80f }),
                Is.EqualTo(0.4f).Within(0.001f));
            Assert.That((float)calculate.Invoke(null, new object[] { 2f, 50f }),
                Is.EqualTo(3f).Within(0.001f));
        }

        [Test]
        public void FrostbiteDealsTenPercentOfMaximumHealthOnce()
        {
            MethodInfo calculate = GetRuntimeType("ZZ.FrostbiteEffect")
                .GetMethod(
                    "CalculatePercentageDamage",
                    BindingFlags.Public | BindingFlags.Static);
            Assert.That(calculate, Is.Not.Null);
            Assert.That((float)calculate.Invoke(null, new object[] { 500f, 10f }),
                Is.EqualTo(50f).Within(0.001f));
            Assert.That((float)calculate.Invoke(null, new object[] { -100f, 10f }),
                Is.Zero);
        }

        [Test]
        public void AuthoredEffectsUseStableIDsAndReferences()
        {
            UnityEngine.Object takeFrost = LoadRequiredAsset(k_TakeFrostPath);
            UnityEngine.Object degradeFrost = LoadRequiredAsset(
                k_DegradeFrostPath);
            UnityEngine.Object staminaModifier = LoadRequiredAsset(
                k_StaminaModifierPath);
            UnityEngine.Object frostbite = LoadRequiredAsset(
                k_FrostbiteEffectPath);

            Assert.That(GetProperty<int>(takeFrost, "InstantEffectId"),
                Is.EqualTo(5));
            Assert.That(GetProperty(takeFrost, "BuildupType").ToString(),
                Is.EqualTo("Frost"));
            Assert.That(GetProperty(takeFrost, "DegradeBuildupEffect"),
                Is.SameAs(degradeFrost));
            Assert.That(GetProperty<int>(degradeFrost, "TimedEffectID"),
                Is.EqualTo(3));
            Assert.That(GetProperty<int>(staminaModifier, "TimedEffectID"),
                Is.EqualTo(4));
            Assert.That(GetProperty<float>(staminaModifier, "ModifierPercentage"),
                Is.EqualTo(-80f).Within(0.001f));
            Assert.That(GetProperty<int>(frostbite, "TimedEffectID"),
                Is.EqualTo(5));
            Assert.That(GetProperty<float>(frostbite, "HPPercentageDamage"),
                Is.EqualTo(10f).Within(0.001f));
            Assert.That(GetProperty(
                    frostbite,
                    "StaminaRegenerationModifierEffect"),
                Is.SameAs(staminaModifier));
        }

        [Test]
        public void TimedFrostbiteRuntimeCopiesStartUninitialized()
        {
            UnityEngine.Object frostbite = LoadRequiredAsset(
                k_FrostbiteEffectPath);
            UnityEngine.Object staminaModifier = LoadRequiredAsset(
                k_StaminaModifierPath);
            UnityEngine.Object frostbiteRuntime = InvokeRuntimeCopy(frostbite);
            UnityEngine.Object modifierRuntime = InvokeRuntimeCopy(staminaModifier);
            try
            {
                Assert.That(frostbiteRuntime, Is.Not.SameAs(frostbite));
                Assert.That(GetProperty<bool>(
                    frostbiteRuntime,
                    "EffectHasBeenInitialized"), Is.False);
                Assert.That(GetProperty<bool>(
                    modifierRuntime,
                    "EffectHasBeenInitialized"), Is.False);
                Assert.That(GetProperty<float>(
                    frostbiteRuntime,
                    "TimeRemainingOnEffect"),
                    Is.EqualTo(60f).Within(0.001f));
                Assert.That(GetProperty<float>(
                    modifierRuntime,
                    "TimeRemainingOnEffect"),
                    Is.EqualTo(60f).Within(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(frostbiteRuntime);
                UnityEngine.Object.DestroyImmediate(modifierRuntime);
            }
        }

        [Test]
        public void FrostbitePresentationAssetsAreBlueAndLoadable()
        {
            GameObject vfx = (GameObject)LoadRequiredAsset(k_FrostbiteVFXPath);
            Material frozenMaterial = (Material)LoadRequiredAsset(
                k_FrozenMaterialPath);
            ParticleSystem particles = vfx.GetComponentInChildren<ParticleSystem>(
                true);

            Assert.That(particles, Is.Not.Null);
            Color frostColor = particles.main.startColor.colorMax;
            Assert.That(frostColor.b, Is.GreaterThan(frostColor.r));
            Assert.That(Resources.Load<GameObject>("Effects/Frostbite VFX"),
                Is.SameAs(vfx));
            Assert.That(Resources.Load<Material>("Effects/Frozen Material"),
                Is.SameAs(frozenMaterial));
        }

        [Test]
        public void PlayerUIPrefabContainsHiddenFrostBuildupBar()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(
                k_PlayerUIPrefabPath);
            try
            {
                Type barType = GetRuntimeType("ZZ.UIBuildupBar");
                Component[] bars = root.GetComponentsInChildren(barType, true);
                Component frostBar = bars.Single(bar =>
                    GetProperty(bar, "BuildupType").ToString() == "Frost");
                Component hud = root.GetComponentInChildren(
                    GetRuntimeType("ZZ.PlayerUIHUDManager"),
                    true);
                SerializedObject serializedHUD = new(hud);
                SerializedProperty buildupBars = serializedHUD.FindProperty(
                    "m_buildupBars");

                Assert.That(bars.Length, Is.EqualTo(3));
                Assert.That(frostBar.gameObject.activeSelf, Is.False);
                Assert.That(frostBar.transform.parent.name,
                    Is.EqualTo("Popup Organizer"));
                Assert.That(buildupBars.arraySize, Is.EqualTo(3));
                Assert.That(
                    buildupBars.GetArrayElementAtIndex(2).objectReferenceValue,
                    Is.SameAs(frostBar));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [Test]
        public void SourcesPreserveOwnerAuthorityAndFrozenStateRestoration()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string networkSource = ReadSource(
                projectRoot,
                "Assets/_Game/Scripts/Characters/Common/CharacterNetworkManager.cs");
            string characterSource = ReadSource(
                projectRoot,
                "Assets/_Game/Scripts/Characters/Common/CharacterManager.cs");
            string effectsSource = ReadSource(
                projectRoot,
                "Assets/_Game/Scripts/Characters/Common/Effects/CharacterEffectsManager.cs");
            string damageSource = ReadSource(
                projectRoot,
                "Assets/_Game/Scripts/Combat/Damage/DamageCollider.cs");
            string hudSource = ReadSource(
                projectRoot,
                "Assets/_Game/Scripts/UI/Gameplay/Player/PlayerUIHUDManager.cs");

            Assert.That(networkSource,
                Does.Contain("IsFrostbitten.OnValueChanged +="));
            Assert.That(networkSource, Does.Contain("IsFrozen.OnValueChanged +="));
            Assert.That(networkSource,
                Does.Contain("IsFrostbitten.OnValueChanged -="));
            Assert.That(networkSource, Does.Contain("IsFrozen.OnValueChanged -="));
            Assert.That(characterSource, Does.Contain("m_animator.speed = 0f"));
            Assert.That(characterSource, Does.Contain("SetCanRun(false)"));
            Assert.That(characterSource,
                Does.Contain("state.OriginalMaterials"));
            Assert.That(characterSource,
                Does.Contain("GetComponentsInChildren<Behaviour>(true)"));
            Assert.That(effectsSource, Does.Contain("TriggerFrostbite"));
            Assert.That(damageSource, Does.Contain("m_frostBuildup"));
            Assert.That(hudSource,
                Does.Contain("FrostBuildup.OnValueChanged"));
        }

        public static void RunAllFocusedTests()
        {
            FrostbiteSystemTests tests = new();
            tests.FrostUsesIndependentBuildupChannel();
            tests.StaminaRegenerationAppliesSignedPercentageModifier();
            tests.FrostbiteDealsTenPercentOfMaximumHealthOnce();
            tests.AuthoredEffectsUseStableIDsAndReferences();
            tests.TimedFrostbiteRuntimeCopiesStartUninitialized();
            tests.FrostbitePresentationAssetsAreBlueAndLoadable();
            tests.PlayerUIPrefabContainsHiddenFrostBuildupBar();
            tests.SourcesPreserveOwnerAuthorityAndFrozenStateRestoration();
            Debug.Log("[FrostbiteSystemTests] 8 focused tests passed.");
        }

        private static UnityEngine.Object LoadRequiredAsset(string assetPath)
        {
            UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            Assert.That(asset, Is.Not.Null, $"Missing asset: {assetPath}");
            return asset;
        }

        private static UnityEngine.Object InvokeRuntimeCopy(
            UnityEngine.Object template)
        {
            MethodInfo createRuntime = template.GetType().GetMethod(
                "CreateRuntimeInstance",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(createRuntime, Is.Not.Null);
            return (UnityEngine.Object)createRuntime.Invoke(template, null);
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

        private static string ReadSource(
            string projectRoot,
            string relativePath)
        {
            return File.ReadAllText(Path.Combine(projectRoot, relativePath));
        }
    }
}
