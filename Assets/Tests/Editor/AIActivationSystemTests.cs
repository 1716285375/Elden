using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ZZ.Tests
{
    public class AIActivationSystemTests
    {
        private const string k_PlayerPrefabPath =
            "Assets/Data/Prefabs/Player.prefab";
        private const string k_AICharacterPrefabPath =
            "Assets/Data/Prefabs/Characters/AI/Undead AI.prefab";
        private const string k_BeaconPrefabPath =
            "Assets/Data/Prefabs/Characters/AI/AI Activation Beacon.prefab";
        private const string k_WorldAIManagerPrefabPath =
            "Assets/Data/Prefabs/Word Managers/World AI Manager.prefab";

        [Test]
        public void NetworkStateUsesServerWriteAndMirroredSubscriptions()
        {
            string source = ReadRuntimeSource(
                "Character/AI/AICharacterNetworkManager.cs");

            Assert.That(source, Does.Contain("NetworkVariable<bool> IsActive"));
            Assert.That(source, Does.Contain("NetworkVariableWritePermission.Server"));
            Assert.That(source, Does.Contain(
                "IsActive.OnValueChanged += OnActiveStateChanged"));
            Assert.That(source, Does.Contain(
                "IsActive.OnValueChanged -= OnActiveStateChanged"));
            Assert.That(source, Does.Contain("gameObject.SetActive(isActive)"));
            Assert.That(source, Does.Contain("!IsSpawned || !IsServer"));
        }

        [Test]
        public void ActivationListDeduplicatesAndPrunesDestroyedPlayers()
        {
            string source = ReadRuntimeSource(
                "Character/AI/AICharacterCombatManager.cs");

            Assert.That(source, Does.Contain(
                "List<PlayerManager> m_playersWithinActivationRange"));
            Assert.That(source, Does.Contain(
                "m_playersWithinActivationRange.Contains(player)"));
            Assert.That(source, Does.Contain(
                "PruneMissingPlayersWithinActivationRange();"));
            Assert.That(source, Does.Contain(
                "m_playersWithinActivationRange.RemoveAt(index)"));
        }

        [Test]
        public void ActivationListRuntimeBehaviorHandlesDuplicatesAndDestroyedPlayers()
        {
            GameObject aiPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                k_AICharacterPrefabPath);
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                k_PlayerPrefabPath);
            GameObject ai = PrefabUtility.InstantiatePrefab(aiPrefab) as GameObject;
            GameObject playerOne =
                PrefabUtility.InstantiatePrefab(playerPrefab) as GameObject;
            GameObject playerTwo =
                PrefabUtility.InstantiatePrefab(playerPrefab) as GameObject;
            try
            {
                Type combatType = GetRuntimeType("ZZ.AICharacterCombatManager");
                Type playerType = GetRuntimeType("ZZ.PlayerManager");
                Component combat = ai.GetComponent(combatType);
                Component firstPlayer = playerOne.GetComponent(playerType);
                Component secondPlayer = playerTwo.GetComponent(playerType);
                object[] firstPlayerArgument = { firstPlayer };

                Assert.That(combatType
                    .GetMethod("AddPlayerToPlayersWithinRange")
                    .Invoke(combat, firstPlayerArgument), Is.True);
                Assert.That(combatType
                    .GetMethod("AddPlayerToPlayersWithinRange")
                    .Invoke(combat, firstPlayerArgument), Is.False);
                Assert.That(combatType
                    .GetMethod("AddPlayerToPlayersWithinRange")
                    .Invoke(combat, new object[] { secondPlayer }), Is.True);
                Assert.That(GetActivationCount(combatType, combat), Is.EqualTo(2));

                UnityEngine.Object.DestroyImmediate(playerTwo);
                playerTwo = null;
                Assert.That(combatType
                    .GetMethod("RemovePlayerFromPlayersWithinRange")
                    .Invoke(combat, firstPlayerArgument), Is.True);
                Assert.That(GetActivationCount(combatType, combat), Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(ai);
                UnityEngine.Object.DestroyImmediate(playerOne);
                if (playerTwo != null)
                {
                    UnityEngine.Object.DestroyImmediate(playerTwo);
                }
            }
        }

        [Test]
        public void DeactivationClearsCombatAndResetReturnsToIdle()
        {
            string managerSource = ReadRuntimeSource(
                "Character/AI/AICharacterManager.cs");
            string spawnerSource = ReadRuntimeSource(
                "World Managers/AI/AICharacterSpawner.cs");

            Assert.That(managerSource, Does.Contain("ClearTarget();"));
            Assert.That(managerSource, Does.Contain(
                "m_stateMachine?.ChangeState(AICharacterStateId.Idle)"));
            Assert.That(managerSource, Does.Contain(
                "ClearPlayersWithinActivationRange"));
            Assert.That(managerSource, Does.Contain("SetActiveState(false)"));
            Assert.That(spawnerSource, Does.Contain(
                "character.InitializeAsInactive();"));
        }

        [Test]
        public void BeaconDetectorPrefabUsesServerOnlySixtyFiveMeterTrigger()
        {
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                k_PlayerPrefabPath);
            Transform detector = playerPrefab?.transform.Find("Beacon Detector");
            SphereCollider sphereCollider = detector?.GetComponent<SphereCollider>();
            Rigidbody rigidbody = detector?.GetComponent<Rigidbody>();
            Type detectorType = GetRuntimeType("ZZ.BeaconDetector");
            Component detectorComponent = detector?.GetComponent(detectorType);
            object assignedPlayer = detectorType.GetProperty("Player")
                .GetValue(detectorComponent);

            Assert.That(detector, Is.Not.Null);
            Assert.That(detector.gameObject.layer, Is.EqualTo(
                LayerMask.NameToLayer("BeaconDetector")));
            Assert.That(sphereCollider?.isTrigger, Is.True);
            Assert.That(sphereCollider?.radius, Is.EqualTo(65f).Within(0.01f));
            Assert.That(rigidbody?.isKinematic, Is.True);
            Assert.That(rigidbody?.useGravity, Is.False);
            Assert.That(assignedPlayer, Is.EqualTo(
                playerPrefab.GetComponent(GetRuntimeType("ZZ.PlayerManager"))));
        }

        [Test]
        public void SharedBeaconPrefabIsLightweightAndAssignedToWorldManager()
        {
            Type beaconType = GetRuntimeType("ZZ.AIActivationBeacon");
            Type managerType = GetRuntimeType("ZZ.WorldAIManager");
            GameObject beaconPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                k_BeaconPrefabPath);
            GameObject managerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                k_WorldAIManagerPrefabPath);
            SphereCollider sphereCollider =
                beaconPrefab?.GetComponent<SphereCollider>();
            Rigidbody rigidbody = beaconPrefab?.GetComponent<Rigidbody>();
            Component manager = managerPrefab?.GetComponent(managerType);
            object assignedBeacon = managerType
                .GetProperty("AIActivationBeaconPrefab")
                .GetValue(manager);

            Assert.That(beaconPrefab?.GetComponent(beaconType), Is.Not.Null);
            Assert.That(
                beaconPrefab?.GetComponents<Component>().Any(component =>
                    component != null &&
                    component.GetType().FullName == "Unity.Netcode.NetworkObject"),
                Is.False);
            Assert.That(sphereCollider?.isTrigger, Is.True);
            Assert.That(rigidbody?.isKinematic, Is.True);
            Assert.That(assignedBeacon, Is.Not.Null);
        }

        [Test]
        public void CollisionMatrixOnlyKeepsRequiredActivationPairs()
        {
            int beaconLayer = LayerMask.NameToLayer("Beacon");
            int detectorLayer = LayerMask.NameToLayer("BeaconDetector");
            int damageableLayer = LayerMask.NameToLayer("Damageable Character");

            Assert.That(beaconLayer, Is.GreaterThanOrEqualTo(0));
            Assert.That(detectorLayer, Is.GreaterThanOrEqualTo(0));
            for (int layer = 0; layer < 32; layer++)
            {
                Assert.That(
                    Physics.GetIgnoreLayerCollision(beaconLayer, layer),
                    Is.EqualTo(layer != detectorLayer));
                Assert.That(
                    Physics.GetIgnoreLayerCollision(detectorLayer, layer),
                    Is.EqualTo(layer != beaconLayer && layer != damageableLayer));
            }
        }

        private static string ReadRuntimeSource(string relativePath)
        {
            return File.ReadAllText($"Assets/_Game/Scripts/{relativePath}");
        }

        private static int GetActivationCount(Type combatType, Component combat)
        {
            return (int)combatType
                .GetProperty("PlayersWithinActivationRangeCount")
                .GetValue(combat);
        }

        private static Type GetRuntimeType(string fullName)
        {
            Type type = Type.GetType($"{fullName}, Assembly-CSharp");
            Assert.That(type, Is.Not.Null, fullName);
            return type;
        }
    }
}
