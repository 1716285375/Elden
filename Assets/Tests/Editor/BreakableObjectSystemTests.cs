using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZZ.Tests
{
    public class BreakableObjectSystemTests
    {
        private const string k_BreakablePrefabPath =
            "Assets/Data/Prefabs/World Objects/Breakables/" +
            "Wooden Crate Breakable.prefab";
        private const string k_BrokenPrefabPath =
            "Assets/Data/Prefabs/World Objects/Breakables/" +
            "Wooden Crate Broken.prefab";
        private const string k_AreaScenePath =
            "Assets/Scenes/Levels/LV01_AbandonedMonastery/Regions/R01_MonasteryOutskirts/SCN_LV01_R01_A01_Base.unity";

        [Test]
        public static void BreakableUsesPredictedAndServerAuthoritativeState()
        {
            Type breakableType = GetRuntimeType("ZZ.BreakableObject");
            string source = ReadRuntimeSource(
                "World Objects/BreakableObject.cs");

            Assert.That(
                breakableType.BaseType?.FullName,
                Is.EqualTo("Unity.Netcode.NetworkBehaviour"));
            Assert.That(source, Does.Contain("m_isBrokenLocal"));
            Assert.That(source, Does.Contain("PlayBreakEffects();"));
            Assert.That(source, Does.Contain("BreakObjectServerRpc();"));
            Assert.That(
                source,
                Does.Contain("[ServerRpc(RequireOwnership = false)]"));
            Assert.That(source, Does.Contain("IsBroken.Value = true"));
        }

        [Test]
        public static void NetworkLifecycleSupportsLateJoinAndTransformState()
        {
            string source = ReadRuntimeSource(
                "World Objects/BreakableObject.cs");

            Assert.That(source, Does.Contain(
                "IsBroken.OnValueChanged += OnIsBrokenChanged"));
            Assert.That(source, Does.Contain(
                "NetworkPosition.OnValueChanged += OnNetworkPositionChanged"));
            Assert.That(source, Does.Contain(
                "NetworkRotation.OnValueChanged += OnNetworkRotationChanged"));
            Assert.That(source, Does.Contain(
                "OnIsBrokenChanged(IsBroken.Value, IsBroken.Value)"));
            Assert.That(source, Does.Contain("DestroyBrokenObject();"));
            Assert.That(source, Does.Contain("m_isBrokenLocal = false;"));
        }

        [Test]
        public static void TriggerRecognizesDamageAIJumpAndRoll()
        {
            string source = ReadRuntimeSource(
                "World Objects/BreakableObject.cs");

            Assert.That(source, Does.Contain(
                "GetComponentInParent<DamageCollider>()"));
            Assert.That(source, Does.Contain(
                "GetComponentInParent<AICharacterManager>()"));
            Assert.That(source, Does.Contain("player.IsJumping"));
            Assert.That(source, Does.Contain(
                "characterNetworkManager?.IsRolling.Value == true"));
        }

        [Test]
        public static void RollingStateStartsOnRollAndResetsWithActionFlags()
        {
            string networkSource = ReadRuntimeSource(
                "Character/CharacterNetworkManager.cs");
            string locomotionSource = ReadRuntimeSource(
                "Character/Player/PlayerLocomotionManager.cs");
            string characterSource = ReadRuntimeSource(
                "Character/CharacterManager.cs");

            Assert.That(networkSource, Does.Contain(
                "NetworkVariable<bool> IsRolling"));
            Assert.That(locomotionSource, Does.Contain(
                "SetRollingState(true)"));
            Assert.That(characterSource, Does.Contain(
                "SetRollingState(false)"));
        }

        [Test]
        public static void BreakableLayersAreGroundAndEnvironmentSurfaces()
        {
            Type utilityType = GetRuntimeType("ZZ.WorldUtilityManager");
            GameObject utilityObject = new GameObject("EP127 Utility Test");
            utilityObject.SetActive(false);
            try
            {
                Component utility = utilityObject.AddComponent(utilityType);
                LayerMask groundLayers = (LayerMask)utilityType.GetMethod(
                    "GetGroundLayers")?.Invoke(utility, null);
                LayerMask environmentLayers = (LayerMask)utilityType.GetMethod(
                    "GetEnvironmentLayers")?.Invoke(utility, null);

                Assert.That(LayerMask.LayerToName(16),
                    Is.EqualTo("Breakable Object"));
                Assert.That(LayerMask.LayerToName(17),
                    Is.EqualTo("Broken Object"));
                Assert.That(groundLayers.value & (1 << 16), Is.Not.Zero);
                Assert.That(groundLayers.value & (1 << 17), Is.Not.Zero);
                Assert.That(environmentLayers.value & (1 << 16), Is.Not.Zero);
                Assert.That(environmentLayers.value & (1 << 17), Is.Not.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(utilityObject);
            }
        }

        [Test]
        public static void WholePrefabHasEarlyTriggerNetworkAndSpatialAudio()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                k_BreakablePrefabPath);
            BoxCollider[] colliders = prefab?.GetComponents<BoxCollider>();
            Rigidbody rigidbody = prefab?.GetComponent<Rigidbody>();
            AudioSource audioSource = prefab?.GetComponent<AudioSource>();

            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.layer, Is.EqualTo(16));
            Assert.That(prefab.GetComponent(GetRuntimeType(
                "Unity.Netcode.NetworkObject")), Is.Not.Null);
            Assert.That(prefab.GetComponent(GetRuntimeType(
                "ZZ.BreakableObject")), Is.Not.Null);
            Assert.That(colliders, Has.Length.EqualTo(2));
            Assert.That(colliders.Count(collider => collider.isTrigger),
                Is.EqualTo(1));
            Assert.That(rigidbody?.isKinematic, Is.True);
            Assert.That(rigidbody?.constraints,
                Is.EqualTo(RigidbodyConstraints.FreezeAll));
            Assert.That(audioSource?.spatialBlend, Is.EqualTo(1f));
        }

        [Test]
        public static void BrokenPrefabContainsSevenPhysicalFragments()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                k_BrokenPrefabPath);
            Rigidbody[] rigidbodies =
                prefab?.GetComponentsInChildren<Rigidbody>(true);
            MeshCollider[] colliders =
                prefab?.GetComponentsInChildren<MeshCollider>(true);

            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.layer, Is.EqualTo(17));
            Assert.That(rigidbodies, Has.Length.EqualTo(7));
            Assert.That(colliders?.Length, Is.GreaterThanOrEqualTo(7));
            Assert.That(colliders.All(collider => collider.convex), Is.True);
        }

        [Test]
        public static void AdditiveSceneContainsInSceneBreakable()
        {
            Scene scene = SceneManager.GetSceneByPath(k_AreaScenePath);
            bool openedByTest = !scene.IsValid() || !scene.isLoaded;
            if (openedByTest)
            {
                scene = EditorSceneManager.OpenScene(
                    k_AreaScenePath,
                    OpenSceneMode.Additive);
            }

            try
            {
                GameObject breakable = scene.GetRootGameObjects()
                    .Single(root => root.name == "Breakable Wooden Crate");
                Assert.That(breakable.GetComponent(GetRuntimeType(
                    "ZZ.BreakableObject")), Is.Not.Null);
                Assert.That(breakable.GetComponent(GetRuntimeType(
                    "Unity.Netcode.NetworkObject")), Is.Not.Null);
            }
            finally
            {
                if (openedByTest)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        [Test]
        public static void NetworkDespawnUsesNonNetworkAdditiveSceneCleanup()
        {
            string source = ReadRuntimeSource(
                "World Managers/WorldSceneManager.cs");

            Assert.That(source, Does.Contain(
                "StartCoroutine(UnloadAllAdditiveScenesNonNetwork())"));
            Assert.That(source, Does.Contain(
                "SceneManager.UnloadSceneAsync(scene)"));
            Assert.That(source, Does.Not.Contain(
                "NetworkManager.SceneManager.UnloadAllAdditiveScenesNonNetwork"));
        }

        [Test]
        public static void FragmentForceMatchesEPDefaults()
        {
            string source = ReadRuntimeSource(
                "World Objects/BreakableObject.cs");

            Assert.That(source, Does.Contain(
                "k_DefaultExplosionForce = 350f"));
            Assert.That(source, Does.Contain(
                "k_DefaultExplosionRadius = 5f"));
            Assert.That(source, Does.Contain(
                "k_DefaultMinimumTorque = 250f"));
            Assert.That(source, Does.Contain(
                "k_DefaultMaximumTorque = 500f"));
            Assert.That(source, Does.Contain("AddExplosionForce("));
            Assert.That(source, Does.Contain("Random.onUnitSphere"));
        }

        private static string ReadRuntimeSource(string relativePath)
        {
            relativePath = RemapRuntimeSourcePath(relativePath);
            return File.ReadAllText($"Assets/_Game/Scripts/{relativePath}");
        }
        /// <summary>Maps a pre-refactor Script-relative path to the new layout.</summary>
        private static string RemapRuntimeSourcePath(string relativePath)
        {
            if (relativePath.StartsWith("Character/Player/Player UI/"))
                return "Characters/Player/Player UI/" + relativePath.Substring("Character/Player/Player UI/".Length);
            if (relativePath.StartsWith("Character/Player/"))
                return "Characters/Player/" + relativePath.Substring("Character/Player/".Length);
            if (relativePath.StartsWith("Character/AI/"))
                return "Characters/AI/" + relativePath.Substring("Character/AI/".Length);
            if (relativePath.StartsWith("Character/Effects/"))
                return "Characters/Common/Effects/" + relativePath.Substring("Character/Effects/".Length);
            if (relativePath.StartsWith("Character/Equipment/"))
                return "Characters/Common/Equipment/" + relativePath.Substring("Character/Equipment/".Length);
            if (relativePath.StartsWith("Character/Inventory/"))
                return "Characters/Common/Inventory/" + relativePath.Substring("Character/Inventory/".Length);
            if (relativePath.StartsWith("Character/Character UI/"))
                return "Characters/Common/Character UI/" + relativePath.Substring("Character/Character UI/".Length);
            if (relativePath.StartsWith("Character/Animation State Behaviors/"))
                return "Characters/Common/Animation State Behaviors/" + relativePath.Substring("Character/Animation State Behaviors/".Length);
            if (relativePath.StartsWith("Character/"))
                return "Characters/Common/" + relativePath.Substring("Character/".Length);
            if (relativePath.StartsWith("World Managers/AI/"))
                return "World/Managers/AI/" + relativePath.Substring("World Managers/AI/".Length);
            if (relativePath.StartsWith("World Managers/"))
                return "World/Managers/" + relativePath.Substring("World Managers/".Length);
            if (relativePath.StartsWith("World Objects/"))
                return "World/Objects/" + relativePath.Substring("World Objects/".Length);
            if (relativePath.StartsWith("Save System/"))
                return "Save/" + relativePath.Substring("Save System/".Length);
            if (relativePath.StartsWith("Menu Scene/"))
                return "UI/Frontend/" + relativePath.Substring("Menu Scene/".Length);
            if (relativePath.StartsWith("Effects/"))
                return "Combat/Effects/" + relativePath.Substring("Effects/".Length);
            if (relativePath.StartsWith("Damage/"))
                return "Combat/Damage/" + relativePath.Substring("Damage/".Length);
            if (relativePath.StartsWith("Actions/"))
                return "Combat/Actions/" + relativePath.Substring("Actions/".Length);
            if (relativePath.StartsWith("Projectiles/"))
                return "Combat/Projectiles/" + relativePath.Substring("Projectiles/".Length);
            if (relativePath.StartsWith("Spells/"))
                return "Abilities/Spells/" + relativePath.Substring("Spells/".Length);
            if (relativePath.StartsWith("Utility/"))
                return "Utilities/" + relativePath.Substring("Utility/".Length);
            return relativePath;
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
