using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ZZ.Tests
{
    public class LevelUpSystemTests
    {
        private const string k_PlayerUIPrefabPath =
            "Assets/Data/Prefabs/Word Managers/Player UI Manager.prefab";

        [TestCase(10, 10, 10, 10, 10, 10, 10, 1)]
        [TestCase(11, 11, 11, 11, 10, 10, 11, 6)]
        public void CharacterLevelIsDerivedFromAllSevenAttributes(
            int vigor,
            int mind,
            int endurance,
            int strength,
            int dexterity,
            int intelligence,
            int faith,
            int expectedLevel)
        {
            Type levelUpType = GetRuntimeType("ZZ.PlayerUILevelUpManager");
            MethodInfo calculateLevel = levelUpType.GetMethod(
                "CalculateCharacterLevel",
                BindingFlags.Public | BindingFlags.Static);

            object result = calculateLevel.Invoke(
                null,
                new object[]
                {
                    vigor,
                    mind,
                    endurance,
                    strength,
                    dexterity,
                    intelligence,
                    faith
                });

            Assert.That(result, Is.EqualTo(expectedLevel));
        }

        [Test]
        public void LevelCostAccumulatesEachTransitionAndRejectsTheCap()
        {
            Type levelUpType = GetRuntimeType("ZZ.PlayerUILevelUpManager");
            GameObject gameObject = new GameObject("Level Up Cost Test");
            try
            {
                Component manager = gameObject.AddComponent(levelUpType);
                MethodInfo calculateCost = levelUpType.GetMethod(
                    "CalculateLevelCost",
                    BindingFlags.Public | BindingFlags.Instance);

                Assert.That(
                    calculateCost.Invoke(manager, new object[] { 6, 9 }),
                    Is.EqualTo(2550));
                Assert.That(
                    calculateCost.Invoke(manager, new object[] { 6, 6 }),
                    Is.Zero);
                Assert.That(
                    calculateCost.Invoke(manager, new object[] { 99, 100 }),
                    Is.EqualTo(int.MaxValue));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ProjectedValueColorUsesWhiteBlueAndRedStates()
        {
            Type levelUpType = GetRuntimeType("ZZ.PlayerUILevelUpManager");
            MethodInfo getColor = levelUpType.GetMethod(
                "GetProjectedValueColor",
                BindingFlags.Public | BindingFlags.Static);
            Color unchanged = Color.white;
            Color affordable = Color.blue;
            Color unaffordable = Color.red;

            Assert.That(
                getColor.Invoke(
                    null,
                    new object[]
                    {
                        false,
                        false,
                        unchanged,
                        affordable,
                        unaffordable
                    }),
                Is.EqualTo(unchanged));
            Assert.That(
                getColor.Invoke(
                    null,
                    new object[]
                    {
                        true,
                        true,
                        unchanged,
                        affordable,
                        unaffordable
                    }),
                Is.EqualTo(affordable));
            Assert.That(
                getColor.Invoke(
                    null,
                    new object[]
                    {
                        true,
                        false,
                        unchanged,
                        affordable,
                        unaffordable
                    }),
                Is.EqualTo(unaffordable));
        }

        [Test]
        public void RuneBalanceLoadsAndSpendsWithoutRewardAggregation()
        {
            Type statsType = GetRuntimeType("ZZ.PlayerStatsManager");
            MethodInfo setRunes = statsType.GetMethod("SetRunes");
            MethodInfo spendRunes = statsType.GetMethod("TrySpendRunes");
            PropertyInfo runes = statsType.GetProperty("Runes");
            GameObject gameObject = new GameObject("Level Up Rune Test");
            try
            {
                Component stats = gameObject.AddComponent(statsType);
                setRunes.Invoke(stats, new object[] { 5000 });

                Assert.That(
                    spendRunes.Invoke(stats, new object[] { 1000 }),
                    Is.True);
                Assert.That(runes.GetValue(stats), Is.EqualTo(4000));
                Assert.That(
                    spendRunes.Invoke(stats, new object[] { 4001 }),
                    Is.False);
                Assert.That(runes.GetValue(stats), Is.EqualTo(4000));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void RuneBalanceSurvivesJsonAndLegacyVersionNineDefaultsToZero()
        {
            CharacterSaveData source = new CharacterSaveData
            {
                Runes = 12345
            };
            CharacterSaveData restored = JsonUtility.FromJson<CharacterSaveData>(
                JsonUtility.ToJson(source));

            Assert.That(restored.Runes, Is.EqualTo(12345));
            Assert.That(
                JsonUtility.FromJson<CharacterSaveData>(
                    "{\"m_dataVersion\":9,\"m_runes\":777}").Runes,
                Is.EqualTo(777));
            source.Runes = -1;
            Assert.That(source.Runes, Is.Zero);

            InvokeSaveMigration(restored, 9, 777);
            Assert.That(restored.Runes, Is.Zero);
        }

        [Test]
        public void PlayerUIPrefabContainsSharedMenusAndCompleteLevelUpFlow()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(
                k_PlayerUIPrefabPath);
            try
            {
                Type menuType = GetRuntimeType("ZZ.PlayerUIMenu");
                Type levelUpType = GetRuntimeType("ZZ.PlayerUILevelUpManager");
                string[] menuTypeNames =
                {
                    "ZZ.PlayerUICharacterMenuManager",
                    "ZZ.PlayerUIEquipmentManager",
                    "ZZ.PlayerUISiteOfGraceManager",
                    "ZZ.PlayerUITeleportLocationManager",
                    "ZZ.PlayerUILevelUpManager"
                };

                foreach (string menuTypeName in menuTypeNames)
                {
                    Type concreteType = GetRuntimeType(menuTypeName);
                    Component menu = root.GetComponent(concreteType);
                    SerializedObject serializedMenu = new SerializedObject(menu);

                    Assert.That(concreteType.IsSubclassOf(menuType), Is.True);
                    Assert.That(
                        serializedMenu.FindProperty("m_menuWindow")
                            .objectReferenceValue,
                        Is.Not.Null,
                        menuTypeName);
                }

                Component levelUp = root.GetComponent(levelUpType);
                SerializedObject serializedLevelUp = new SerializedObject(levelUp);
                SerializedProperty sliders = serializedLevelUp.FindProperty(
                    "m_attributeSliders");
                SerializedProperty currentTexts = serializedLevelUp.FindProperty(
                    "m_currentAttributeTexts");
                SerializedProperty projectedTexts = serializedLevelUp.FindProperty(
                    "m_projectedAttributeTexts");
                Transform levelWindow = FindTransform(root.transform, "Level Up Menu");
                Transform levelButton = FindTransform(root.transform, "Level Up Button");

                Assert.That(levelWindow, Is.Not.Null);
                Assert.That(levelWindow.gameObject.activeSelf, Is.False);
                Assert.That(levelButton?.GetComponent<Button>(), Is.Not.Null);
                Assert.That(sliders.arraySize, Is.EqualTo(7));
                Assert.That(currentTexts.arraySize, Is.EqualTo(7));
                Assert.That(projectedTexts.arraySize, Is.EqualTo(7));
                Assert.That(
                    Enumerable.Range(0, sliders.arraySize).All(index =>
                        sliders.GetArrayElementAtIndex(index).objectReferenceValue != null),
                    Is.True);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [Test]
        public void ConfirmLoadAndPersistentInteractionUseSaveContracts()
        {
            string levelSource = File.ReadAllText(
                "Assets/_Game/Scripts/Characters/Player/Player UI/PlayerUILevelUpManager.cs");
            string playerSource = File.ReadAllText(
                "Assets/_Game/Scripts/Characters/Player/PlayerManager.cs");
            string gateSource = File.ReadAllText(
                "Assets/_Game/Scripts/World/Managers/DungeonOneWayGate.cs");

            Assert.That(levelSource, Does.Contain("TrySpendRunes(m_totalLevelUpCost)"));
            Assert.That(levelSource, Does.Contain("saveGameManager.SaveGame();"));
            Assert.That(playerSource, Does.Contain("PlayerStatsManager.SetRunes("));
            Assert.That(playerSource, Does.Not.Contain("AddRunes(currentData.Runes)"));
            Assert.That(gateSource, Does.Contain("SaveGameAfterInteraction(player);"));
        }

        private static void InvokeSaveMigration(
            CharacterSaveData data,
            int dataVersion,
            int runes)
        {
            Type saveDataType = typeof(CharacterSaveData);
            saveDataType.GetField(
                    "m_dataVersion",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(data, dataVersion);
            saveDataType.GetField(
                    "m_runes",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(data, runes);
            saveDataType.GetMethod(
                    "MigrateToLatestVersion",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(data, null);
        }

        private static Type GetRuntimeType(string fullName)
        {
            Type type = Type.GetType($"{fullName}, Assembly-CSharp");

            Assert.That(type, Is.Not.Null, fullName);
            return type;
        }

        private static Transform FindTransform(Transform root, string objectName)
        {
            if (root.name == objectName)
            {
                return root;
            }

            foreach (Transform child in root)
            {
                Transform match = FindTransform(child, objectName);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }
    }
}
