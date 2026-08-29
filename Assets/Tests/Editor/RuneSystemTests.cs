using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ZZ.Tests
{
    public class RuneSystemTests
    {
        private const string k_PlayerPrefabPath =
            "Assets/Data/Prefabs/Player.prefab";
        private const string k_UndeadPrefabPath =
            "Assets/Data/Prefabs/Characters/AI/Undead AI.prefab";
        private const string k_BossPrefabPath =
            "Assets/Data/Prefabs/Characters/AI/Fallen Watcher Boss.prefab";
        private const string k_PlayerUIManagerPrefabPath =
            "Assets/Data/Prefabs/Word Managers/Player UI Manager.prefab";

        [Test]
        public void PlayerRunesRemainPrivatePlainDataAndAddSafely()
        {
            Type statsType = GetRuntimeType("ZZ.PlayerStatsManager");
            FieldInfo runeField = statsType.GetField(
                "m_runes",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo calculate = statsType.GetMethod(
                "CalculateRuneTotal",
                BindingFlags.Public | BindingFlags.Static);
            MethodInfo addRunes = statsType.GetMethod(
                "AddRunes",
                BindingFlags.Public | BindingFlags.Instance);
            PropertyInfo runes = statsType.GetProperty(
                "Runes",
                BindingFlags.Public | BindingFlags.Instance);

            Assert.That(runeField, Is.Not.Null);
            Assert.That(runeField.FieldType, Is.EqualTo(typeof(int)));
            Assert.That(calculate.Invoke(null, new object[] { 100, 50 }), Is.EqualTo(150));
            Assert.That(
                calculate.Invoke(null, new object[] { int.MaxValue, 50 }),
                Is.EqualTo(int.MaxValue));
            Assert.That(calculate.Invoke(null, new object[] { 100, -50 }), Is.EqualTo(50));

            GameObject gameObject = new GameObject("Rune Stats Test");
            try
            {
                Component stats = gameObject.AddComponent(statsType);
                addRunes.Invoke(stats, new object[] { 50 });
                addRunes.Invoke(stats, new object[] { -10 });
                Assert.That(runes.GetValue(stats), Is.EqualTo(40));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void PendingRuneRewardsAggregateAndResetThroughHudContract()
        {
            Type hudType = GetRuntimeType("ZZ.PlayerUIHUDManager");
            MethodInfo calculate = hudType.GetMethod(
                "CalculatePendingRuneTotal",
                BindingFlags.Public | BindingFlags.Static);
            string source = File.ReadAllText(
                "Assets/_Game/Scripts/Characters/Player/Player UI/PlayerUIHUDManager.cs");

            Assert.That(calculate.Invoke(null, new object[] { 0, 50 }), Is.EqualTo(50));
            Assert.That(calculate.Invoke(null, new object[] { 100, 50 }), Is.EqualTo(150));
            Assert.That(source, Does.Contain("StopRuneMergeCoroutine();"));
            Assert.That(source, Does.Contain("WaitForSecondsRealtime(k_RuneMergeDelay)"));
            Assert.That(source, Does.Contain("m_pendingRunesToAdd = 0;"));
        }

        [Test]
        public void FactionsRejectSameTeamRuneRewards()
        {
            Type groupType = GetRuntimeType("ZZ.CharacterGroup");
            Type combatType = GetRuntimeType("ZZ.AICharacterCombatManager");
            MethodInfo canAward = combatType.GetMethod(
                "CanAwardRunes",
                BindingFlags.Public | BindingFlags.Static);
            object teamOne = Enum.Parse(groupType, "TeamOne");
            object teamTwo = Enum.Parse(groupType, "TeamTwo");

            Assert.That(canAward.Invoke(null, new[] { teamTwo, teamOne }), Is.True);
            Assert.That(canAward.Invoke(null, new[] { teamTwo, teamTwo }), Is.False);
        }

        [Test]
        public void CharacterPrefabsUseAuthoredFactionsAndDifferentRewards()
        {
            AssertCharacterPrefab(k_PlayerPrefabPath, "TeamOne", 0);
            AssertCharacterPrefab(k_UndeadPrefabPath, "TeamTwo", 50);
            AssertCharacterPrefab(k_BossPrefabPath, "TeamTwo", 5000);
        }

        [Test]
        public void PlayerUiPrefabContainsBottomRightRuneHudWithBoundTexts()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(
                k_PlayerUIManagerPrefabPath);

            try
            {
                Transform hud = root.transform.Find("Player UI/HUD");
                Transform runesHUD = hud?.Find("Runes HUD");
                Component hudManager = hud?.GetComponent(
                    GetRuntimeType("ZZ.PlayerUIHUDManager"));
                Component countText = runesHUD?.Find("Runes Count Text")
                    ?.GetComponent(GetTextType());
                Component pendingText = runesHUD?.Find("Runes To Add Text")
                    ?.GetComponent(GetTextType());
                SerializedObject serializedHUD = new SerializedObject(hudManager);

                Assert.That(runesHUD, Is.Not.Null);
                Assert.That(((RectTransform)runesHUD).anchorMin, Is.EqualTo(
                    new Vector2(1f, 0f)));
                Assert.That(countText, Is.Not.Null);
                Assert.That(pendingText, Is.Not.Null);
                Assert.That(pendingText.gameObject.activeSelf, Is.False);
                Assert.That(
                    serializedHUD.FindProperty("m_runesCountText")
                        .objectReferenceValue,
                    Is.EqualTo(countText));
                Assert.That(
                    serializedHUD.FindProperty("m_runesToAddText")
                        .objectReferenceValue,
                    Is.EqualTo(pendingText));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [Test]
        public void DeathRewardRunsOnlyOnFalseToTrueAndTargetsTheKillerOwner()
        {
            string source = File.ReadAllText(
                "Assets/_Game/Scripts/Characters/AI/AICharacterNetworkManager.cs");

            Assert.That(source, Does.Contain("if (!wasDead && IsServer)"));
            Assert.That(source, Does.Contain("TargetClientIds"));
            Assert.That(source, Does.Contain("player.OwnerClientId"));
            Assert.That(source, Does.Contain("AwardRunesClientRpc"));
        }

        [Test]
        public void LocalPlayerCacheBindsAndClearsAtOwnershipBoundaries()
        {
            string managerSource = File.ReadAllText(
                "Assets/_Game/Scripts/Characters/Player/Player UI/PlayerUIManager.cs");
            string playerSource = File.ReadAllText(
                "Assets/_Game/Scripts/Characters/Player/PlayerManager.cs");

            Assert.That(managerSource, Does.Contain("public PlayerManager LocalPlayer"));
            Assert.That(playerSource, Does.Contain("BindLocalPlayer(this)"));
            Assert.That(playerSource, Does.Contain("UnbindLocalPlayer(this)"));
        }

        private static void AssertCharacterPrefab(
            string prefabPath,
            string expectedGroup,
            int expectedReward)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);

            try
            {
                Component character = root.GetComponent(
                    GetRuntimeType("ZZ.CharacterManager"));
                Component stats = root.GetComponent(
                    GetRuntimeType("ZZ.CharacterStatsManager"));
                object group = character.GetType().GetProperty("CharacterGroup")
                    .GetValue(character);
                object reward = stats.GetType().GetProperty("RunesDroppedOnDeath")
                    .GetValue(stats);

                Assert.That(group.ToString(), Is.EqualTo(expectedGroup));
                Assert.That(reward, Is.EqualTo(expectedReward));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Type GetRuntimeType(string fullName)
        {
            Type type = Type.GetType(fullName + ", Assembly-CSharp");

            Assert.That(type, Is.Not.Null, fullName);
            return type;
        }

        private static Type GetTextType()
        {
            Type type = Type.GetType("TMPro.TMP_Text, Unity.TextMeshPro");

            Assert.That(type, Is.Not.Null, "TMPro.TMP_Text");
            return type;
        }
    }
}
