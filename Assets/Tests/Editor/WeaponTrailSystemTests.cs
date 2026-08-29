using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ZZ.Tests
{
    public class WeaponTrailSystemTests
    {
        private const string k_BroadswordPrefabPath =
            "Assets/Data/Prefabs/Weapons/Melee Weapons/Broadsword.prefab";
        private const string k_StraightSwordPrefabPath =
            "Assets/Data/Prefabs/Weapons/Melee Weapons/Straight Sword.prefab";
        private const string k_WeaponManagerPath =
            "Assets/_Game/Scripts/Items/WeaponManager.cs";
        private const string k_PlayerEquipmentManagerPath =
            "Assets/_Game/Scripts/Characters/Common/Equipment/PlayerEquipmentManager.cs";
        private const string k_PlayerCombatManagerPath =
            "Assets/_Game/Scripts/Characters/Player/PlayerCombatManager.cs";

        [Test]
        public void WeaponManagerExposesNullSafeUnifiedTrailToggle()
        {
            string source = File.ReadAllText(k_WeaponManagerPath);

            Assert.That(source, Does.Contain("ToggleWeaponTrail(bool status)"));
            Assert.That(source, Does.Contain("if (m_particleWeaponTrail == null)"));
            Assert.That(source, Does.Contain("m_rendererWeaponTrail.emitting = status"));
            Assert.That(source, Does.Contain("ParticleSystemStopBehavior.StopEmitting"));
        }

        [TestCase(k_BroadswordPrefabPath)]
        [TestCase(k_StraightSwordPrefabPath)]
        public void SwordPrefabsUseAuthoredParticleTrails(string prefabPath)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                prefabPath);
            Type weaponManagerType = GetRuntimeType("ZZ.WeaponManager");
            Component weaponManager = prefab?.GetComponent(weaponManagerType);
            ParticleSystem particles = GetWeaponTrailParticles(
                weaponManagerType,
                weaponManager);

            Assert.That(weaponManager, Is.Not.Null);
            Assert.That(particles, Is.Not.Null);
            Assert.That(particles.main.playOnAwake, Is.False);
            Assert.That(particles.main.simulationSpace,
                Is.EqualTo(ParticleSystemSimulationSpace.World));
            Assert.That(particles.emission.rateOverTime.constant,
                Is.EqualTo(40f).Within(0.001f));
            Assert.That(particles.shape.shapeType,
                Is.EqualTo(ParticleSystemShapeType.SingleSidedEdge));
            Assert.That(particles.trails.enabled, Is.True);
            Assert.That(particles.trails.ratio,
                Is.EqualTo(1f).Within(0.001f));
            Assert.That(particles.trails.minVertexDistance,
                Is.EqualTo(0.01f).Within(0.001f));
            Assert.That(particles.GetComponent<ParticleSystemRenderer>().renderMode,
                Is.EqualTo(ParticleSystemRenderMode.None));
            Assert.That(prefab.GetComponentsInChildren<ParticleSystem>(true),
                Has.Exactly(1).Matches<ParticleSystem>(
                    candidate => candidate.name.Contains("Weapon Trail")));
        }

        [Test]
        public void DamageWindowTogglesTrailAndAllowsNaturalDissipation()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                k_BroadswordPrefabPath);
            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                Type weaponManagerType = GetRuntimeType("ZZ.WeaponManager");
                Component weaponManager = instance.GetComponent(weaponManagerType);
                MethodInfo openDamageCollider = weaponManagerType.GetMethod(
                    "OpenDamageCollider",
                    BindingFlags.Public | BindingFlags.Instance);
                MethodInfo closeDamageCollider = weaponManagerType.GetMethod(
                    "CloseDamageCollider",
                    BindingFlags.Public | BindingFlags.Instance);
                PropertyInfo isWeaponTrailEmitting =
                    weaponManagerType.GetProperty(
                        "IsWeaponTrailEmitting",
                        BindingFlags.Public | BindingFlags.Instance);

                openDamageCollider.Invoke(weaponManager, null);
                Assert.That(
                    (bool)isWeaponTrailEmitting.GetValue(weaponManager),
                    Is.True);

                closeDamageCollider.Invoke(weaponManager, null);
                Assert.That(
                    (bool)isWeaponTrailEmitting.GetValue(weaponManager),
                    Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void UnconfiguredWeaponTrailToggleDoesNotThrow()
        {
            GameObject weaponObject = new("Unconfigured Weapon");
            try
            {
                Type weaponManagerType = GetRuntimeType("ZZ.WeaponManager");
                Component weaponManager = weaponObject.AddComponent(
                    weaponManagerType);
                MethodInfo toggleWeaponTrail = weaponManagerType.GetMethod(
                    "ToggleWeaponTrail",
                    BindingFlags.Public | BindingFlags.Instance);
                Assert.DoesNotThrow(() =>
                    toggleWeaponTrail.Invoke(weaponManager, new object[] { true }));
                Assert.DoesNotThrow(() =>
                    toggleWeaponTrail.Invoke(weaponManager, new object[] { false }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(weaponObject);
            }
        }

        [Test]
        public void RemotePresentationStartsBeforeOwnerDamageGuard()
        {
            string source = File.ReadAllText(k_PlayerEquipmentManagerPath);
            int methodStart = source.IndexOf(
                "private void OpenDamageCollider(WeaponManager weaponManager)",
                StringComparison.Ordinal);
            int trailToggle = source.IndexOf(
                "weaponManager?.ToggleWeaponTrail(true)",
                methodStart,
                StringComparison.Ordinal);
            int ownerGuard = source.IndexOf(
                "!m_player.IsOwner",
                methodStart,
                StringComparison.Ordinal);

            Assert.That(methodStart, Is.GreaterThanOrEqualTo(0));
            Assert.That(trailToggle, Is.GreaterThan(methodStart));
            Assert.That(ownerGuard, Is.GreaterThan(trailToggle));
        }

        [Test]
        public void CloseAllDamageCollidersClosesEveryPlayerWeaponTrail()
        {
            string source = File.ReadAllText(k_PlayerCombatManagerPath);
            int methodStart = source.IndexOf(
                "public override void CloseAllDamageColliders()",
                StringComparison.Ordinal);
            int methodEnd = source.IndexOf(
                "public override bool AttemptRiposte",
                methodStart,
                StringComparison.Ordinal);
            string methodSource = source.Substring(
                methodStart,
                methodEnd - methodStart);

            Assert.That(methodSource,
                Does.Contain("CurrentRightHandWeaponManager"));
            Assert.That(methodSource,
                Does.Contain("CurrentLeftHandWeaponManager"));
            Assert.That(methodSource,
                Does.Contain("CurrentTwoHandWeaponManager"));
            Assert.That(methodSource,
                Does.Contain("CloseDamageCollider()"));
        }

        /// <summary>Runs the focused EP156 tests without entering Play Mode.</summary>
        public static void RunAllFocusedTests()
        {
            WeaponTrailSystemTests tests = new();
            tests.WeaponManagerExposesNullSafeUnifiedTrailToggle();
            tests.SwordPrefabsUseAuthoredParticleTrails(
                k_BroadswordPrefabPath);
            tests.SwordPrefabsUseAuthoredParticleTrails(
                k_StraightSwordPrefabPath);
            tests.DamageWindowTogglesTrailAndAllowsNaturalDissipation();
            tests.UnconfiguredWeaponTrailToggleDoesNotThrow();
            tests.RemotePresentationStartsBeforeOwnerDamageGuard();
            tests.CloseAllDamageCollidersClosesEveryPlayerWeaponTrail();
            Debug.Log(
                "[WeaponTrailSystemTests] 7 EP156 focused tests passed.");
        }

        private static ParticleSystem GetWeaponTrailParticles(
            Type weaponManagerType,
            Component weaponManager)
        {
            PropertyInfo property = weaponManagerType.GetProperty(
                "WeaponTrailParticles",
                BindingFlags.Public | BindingFlags.Instance);
            return property?.GetValue(weaponManager) as ParticleSystem;
        }

        private static Type GetRuntimeType(string fullName)
        {
            foreach (System.Reflection.Assembly assembly in
                     AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName);
                if (type != null)
                {
                    return type;
                }
            }

            throw new InvalidOperationException(
                $"Runtime type {fullName} could not be found.");
        }
    }
}
