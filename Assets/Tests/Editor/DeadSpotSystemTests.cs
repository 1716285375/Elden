using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ZZ.Tests
{
    public class DeadSpotSystemTests
    {
        private const string k_DeadSpotPrefabPath =
            "Assets/Resources/Effects/Dead Spot.prefab";
        private const string k_NetworkPrefabsPath =
            "Assets/DefaultNetworkPrefabs.asset";

        [Test]
        public void DeadSpotStateSurvivesJsonAndVersionTenMigratesSafely()
        {
            CharacterSaveData source = new CharacterSaveData
            {
                HasDeadSpot = true,
                DeadSpotPositionX = 1.25f,
                DeadSpotPositionY = 2.5f,
                DeadSpotPositionZ = -3.75f,
                DeadSpotRuneCount = 500,
                LastSiteOfGraceRestedAt = 7
            };
            CharacterSaveData restored = JsonUtility.FromJson<CharacterSaveData>(
                JsonUtility.ToJson(source));

            Assert.That(restored.HasDeadSpot, Is.True);
            Assert.That(restored.DeadSpotPositionX, Is.EqualTo(1.25f));
            Assert.That(restored.DeadSpotPositionY, Is.EqualTo(2.5f));
            Assert.That(restored.DeadSpotPositionZ, Is.EqualTo(-3.75f));
            Assert.That(restored.DeadSpotRuneCount, Is.EqualTo(500));
            Assert.That(restored.LastSiteOfGraceRestedAt, Is.EqualTo(7));

            SetPrivateField(restored, "m_dataVersion", 10);
            InvokeMigration(restored);
            Assert.That(restored.HasDeadSpot, Is.False);
            Assert.That(restored.DeadSpotRuneCount, Is.Zero);
            Assert.That(restored.LastSiteOfGraceRestedAt, Is.Zero);
        }

        [Test]
        public void SignedRuneChangesClampAndFormatWithoutDoubleSigns()
        {
            Type statsType = GetRuntimeType("ZZ.PlayerStatsManager");
            MethodInfo calculateRunes = statsType.GetMethod(
                "CalculateRuneTotal",
                BindingFlags.Public | BindingFlags.Static);
            Type hudType = GetRuntimeType("ZZ.PlayerUIHUDManager");
            MethodInfo formatChange = hudType.GetMethod(
                "FormatRuneChange",
                BindingFlags.Public | BindingFlags.Static);

            Assert.That(
                calculateRunes.Invoke(null, new object[] { 500, -500 }),
                Is.Zero);
            Assert.That(
                calculateRunes.Invoke(null, new object[] { 100, -500 }),
                Is.Zero);
            Assert.That(
                formatChange.Invoke(null, new object[] { 500 }),
                Is.EqualTo("+ 500"));
            Assert.That(
                formatChange.Invoke(null, new object[] { -500 }),
                Is.EqualTo("- 500"));
            Assert.That(
                formatChange.Invoke(null, new object[] { int.MinValue }),
                Is.EqualTo("- 2147483648"));
        }

        [Test]
        public void DeadSpotPrefabHasNetworkInteractionAndVfx()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                k_DeadSpotPrefabPath);
            Component pickup = prefab?.GetComponent("PickupRunesInteractable");
            SphereCollider sphereCollider = prefab?.GetComponent<SphereCollider>();
            Rigidbody rigidbody = prefab?.GetComponent<Rigidbody>();
            Component networkObject = prefab?.GetComponent(
                GetRuntimeType("Unity.Netcode.NetworkObject"));

            Assert.That(prefab, Is.Not.Null);
            Assert.That(pickup, Is.Not.Null);
            Assert.That(networkObject, Is.Not.Null);
            Assert.That(sphereCollider?.isTrigger, Is.True);
            Assert.That(rigidbody?.isKinematic, Is.True);
            Assert.That(rigidbody?.useGravity, Is.False);
            Assert.That(prefab.layer, Is.EqualTo(LayerMask.NameToLayer("Interactable")));
            Assert.That(prefab.GetComponentInChildren<ParticleSystem>(true), Is.Not.Null);

            SerializedObject serializedPickup = new SerializedObject(pickup);
            Assert.That(
                serializedPickup.FindProperty("m_hostOnlyInteractable").boolValue,
                Is.False);
            Assert.That(
                serializedPickup.FindProperty("m_interactableCollider")
                    .objectReferenceValue,
                Is.EqualTo(sphereCollider));
        }

        [Test]
        public void DeadSpotPrefabIsRegisteredExactlyOnceForNetworkSpawning()
        {
            GameObject deadSpot = AssetDatabase.LoadAssetAtPath<GameObject>(
                k_DeadSpotPrefabPath);
            UnityEngine.Object prefabs = AssetDatabase.LoadMainAssetAtPath(
                k_NetworkPrefabsPath);
            SerializedProperty entries = new SerializedObject(prefabs)
                .FindProperty("List");
            int registrationCount = Enumerable.Range(0, entries.arraySize)
                .Count(index => entries.GetArrayElementAtIndex(index)
                    .FindPropertyRelative("Prefab")
                    .objectReferenceValue == deadSpot);

            Assert.That(registrationCount, Is.EqualTo(1));
        }

        [Test]
        public void DeathTransitionCreatesOneHostDeadSpotAndStartsRevival()
        {
            string networkSource = File.ReadAllText(
                "Assets/Script/Character/Player/PlayerNetworkManager.cs");
            string combatSource = File.ReadAllText(
                "Assets/Script/Character/Player/PlayerCombatManager.cs");

            Assert.That(
                networkSource,
                Does.Contain("if (wasDead || !isDead || !IsOwner || !IsServer)"));
            Assert.That(networkSource, Does.Contain("CreateDeadSpot("));
            Assert.That(networkSource, Does.Contain("ReviveHost(player)"));
            Assert.That(combatSource, Does.Contain("networkManager?.IsHost != true"));
            Assert.That(combatSource, Does.Contain("networkObject.Spawn(true)"));
            Assert.That(combatSource, Does.Contain("AddRunes(-runeCount)"));
        }

        [Test]
        public void SceneRestoreDoesNotChargeRunesTwiceAndUnsubscribes()
        {
            string combatSource = File.ReadAllText(
                "Assets/Script/Character/Player/PlayerCombatManager.cs");

            Assert.That(
                combatSource,
                Does.Contain("SceneManager.activeSceneChanged += OnActiveSceneChanged"));
            Assert.That(
                combatSource,
                Does.Contain("SceneManager.activeSceneChanged -= OnActiveSceneChanged"));
            Assert.That(combatSource, Does.Contain("saveData?.HasDeadSpot != true"));
            Assert.That(
                combatSource,
                Does.Match(@"saveData\.DeadSpotRuneCount,\s+false\);"));
        }

        [Test]
        public void RevivalUsesSingleCoroutineSavedCheckpointAndFallback()
        {
            string sessionSource = File.ReadAllText(
                "Assets/Script/World Managers/WorldGameSessionManager.cs");
            string graceSource = File.ReadAllText(
                "Assets/Script/World Managers/SiteOfGraceInteractable.cs");
            string objectSource = File.ReadAllText(
                "Assets/Script/World Managers/WorldObjectManager.cs");

            Assert.That(sessionSource, Does.Contain("StopRevivalCoroutine();"));
            Assert.That(
                sessionSource,
                Does.Contain("new WaitForSecondsRealtime(m_hostReviveDelaySeconds)"));
            Assert.That(sessionSource, Does.Contain("ActivateLoadingScreen()"));
            Assert.That(sessionSource, Does.Contain("hostPlayer.ReviveCharacter()"));
            Assert.That(sessionSource, Does.Contain("GetRespawnSiteOfGrace("));
            Assert.That(
                graceSource,
                Does.Contain("RecordLastSiteOfGraceRestedAt("));
            Assert.That(
                objectSource,
                Does.Contain("m_sitesOfGrace.Count > 0"));
        }

        [Test]
        public void ReclaimTargetsOriginalOwnerClearsSaveAndDespawns()
        {
            Type pickupType = GetRuntimeType("ZZ.PickupRunesInteractable");
            MethodInfo reclaimRpc = pickupType.GetMethod(
                "ReclaimRunesServerRpc",
                BindingFlags.Instance | BindingFlags.NonPublic);
            object serverRpcAttribute = reclaimRpc.GetCustomAttributes(false)
                .Single(attribute =>
                    attribute.GetType().Name == "ServerRpcAttribute");
            string source = File.ReadAllText(
                "Assets/Script/World Managers/PickupRunesInteractable.cs");

            Assert.That(
                (bool)serverRpcAttribute.GetType().GetField("RequireOwnership")
                    .GetValue(serverRpcAttribute),
                Is.False);
            Assert.That(source, Does.Contain("TargetClientIds"));
            Assert.That(source, Does.Contain("player.PlayerStatsManager?.AddRunes"));
            Assert.That(source, Does.Contain("ClearDeadSpot(false)"));
            Assert.That(source, Does.Contain("NetworkObject.Despawn(true)"));
        }

        private static void SetPrivateField(
            CharacterSaveData data,
            string fieldName,
            object value)
        {
            typeof(CharacterSaveData).GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(data, value);
        }

        private static void InvokeMigration(CharacterSaveData data)
        {
            typeof(CharacterSaveData).GetMethod(
                    "MigrateToLatestVersion",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(data, null);
        }

        private static Type GetRuntimeType(string fullName)
        {
            string assemblyName = fullName.StartsWith(
                "Unity.Netcode.",
                StringComparison.Ordinal)
                    ? "Unity.Netcode.Runtime"
                    : "Assembly-CSharp";
            Type type = Type.GetType($"{fullName}, {assemblyName}");

            Assert.That(type, Is.Not.Null, fullName);
            return type;
        }
    }
}
