using System;
using System.Collections.Generic;
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
    public class SiteOfGraceTravelSystemTests
    {
        private const string k_PlayerUIPrefabPath =
            "Assets/_Game/Prefabs/World/Managers/Player UI Manager.prefab";
        private const string k_WorldScenePath =
            "Assets/_Game/Scenes/Levels/LV01_AbandonedMonastery/SCN_LV01_AbandonedMonastery.unity";

        [Test]
        public void WorldObjectManagerExposesSiteRegistryByStableID()
        {
            Type managerType = GetRuntimeType("ZZ.WorldObjectManager");

            Assert.That(managerType.GetProperty("SitesOfGrace"), Is.Not.Null);
            Assert.That(
                managerType.GetMethod("RegisterSiteOfGrace"),
                Is.Not.Null);
            Assert.That(
                managerType.GetMethod("UnregisterSiteOfGrace"),
                Is.Not.Null);
            Assert.That(
                managerType.GetMethod("GetSiteOfGraceByID"),
                Is.Not.Null);
        }

        [Test]
        public void WorldSceneSetHasUniqueSitesWithSeparateTeleportPoints()
        {
            Scene masterScene = SceneManager.GetSceneByPath(k_WorldScenePath);
            bool shouldCloseMaster = !masterScene.IsValid() || !masterScene.isLoaded;
            if (shouldCloseMaster)
            {
                masterScene = EditorSceneManager.OpenScene(
                    k_WorldScenePath,
                    OpenSceneMode.Additive);
            }

            try
            {
                Component[] masterComponents = masterScene.GetRootGameObjects()
                    .SelectMany(root =>
                        root.GetComponentsInChildren<Component>(true))
                    .ToArray();
                Assert.That(
                    masterComponents.Count(component =>
                        component.GetType().Name == "WorldObjectManager"),
                    Is.EqualTo(1));

                List<int> siteIDs = new List<int>();
                string[] spawnerScenePaths = AssetDatabase.FindAssets(
                        "t:Scene",
                        new[]
                        {
                            "Assets/_Game/Scenes/Levels/LV01_AbandonedMonastery/Regions"
                        })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Where(path => path.EndsWith(
                        "_Spawners.unity",
                        StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                foreach (string spawnerScenePath in spawnerScenePaths)
                {
                    Scene spawnerScene = EditorSceneManager.OpenScene(
                        spawnerScenePath,
                        OpenSceneMode.Additive);
                    try
                    {
                        Component[] sites = spawnerScene.GetRootGameObjects()
                            .SelectMany(root =>
                                root.GetComponentsInChildren<Component>(true))
                            .Where(component =>
                                component.GetType().Name == "SiteOfGraceInteractable")
                            .ToArray();
                        foreach (Component site in sites)
                        {
                            siteIDs.Add(GetProperty<int>(site, "SiteOfGraceID"));
                            Transform teleport = GetProperty<Transform>(
                                site,
                                "TeleportTransform");
                            Assert.That(teleport, Is.Not.Null);
                            Assert.That(teleport, Is.Not.EqualTo(site.transform));
                            Assert.That(teleport.IsChildOf(site.transform), Is.True);
                        }
                    }
                    finally
                    {
                        EditorSceneManager.CloseScene(spawnerScene, true);
                    }
                }

                Assert.That(siteIDs, Is.Not.Empty);
                Assert.That(siteIDs, Has.All.GreaterThan(0));
                Assert.That(siteIDs.Distinct().Count(), Is.EqualTo(siteIDs.Count));
            }
            finally
            {
                if (shouldCloseMaster)
                {
                    EditorSceneManager.CloseScene(masterScene, true);
                }
            }
        }

        [Test]
        public void PlayerUIPrefabContainsIndependentGraceAndTravelMenus()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(
                k_PlayerUIPrefabPath);
            try
            {
                Component playerUI = FindComponent(root, "PlayerUIManager");
                Component siteManager = FindComponent(
                    root,
                    "PlayerUISiteOfGraceManager");
                Component teleportManager = FindComponent(
                    root,
                    "PlayerUITeleportLocationManager");
                SerializedObject serializedUI = new SerializedObject(playerUI);
                SerializedObject serializedSite = new SerializedObject(siteManager);
                SerializedObject serializedTeleport = new SerializedObject(
                    teleportManager);
                GameObject siteMenu = serializedSite.FindProperty(
                    "m_menuWindow").objectReferenceValue as GameObject;
                GameObject teleportMenu = serializedTeleport.FindProperty(
                    "m_menuWindow").objectReferenceValue as GameObject;

                Assert.That(
                    serializedUI.FindProperty("m_playerUISiteOfGraceManager")
                        .objectReferenceValue,
                    Is.EqualTo(siteManager));
                Assert.That(
                    serializedUI.FindProperty(
                        "m_playerUITeleportLocationManager")
                        .objectReferenceValue,
                    Is.EqualTo(teleportManager));
                Assert.That(siteMenu, Is.Not.Null);
                Assert.That(teleportMenu, Is.Not.Null);
                Assert.That(siteMenu.activeSelf, Is.False);
                Assert.That(teleportMenu.activeSelf, Is.False);
                Assert.That(
                    siteMenu.GetComponent(GetRuntimeType("ZZ.PlayerUIToggleHUD")),
                    Is.Not.Null);
                Assert.That(
                    teleportMenu.GetComponent(
                        GetRuntimeType("ZZ.PlayerUIToggleHUD")),
                    Is.Not.Null);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [Test]
        public void TravelButtonsMapOneToOneToPositiveSiteIDs()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(
                k_PlayerUIPrefabPath);
            try
            {
                Component teleportManager = FindComponent(
                    root,
                    "PlayerUITeleportLocationManager");
                SerializedObject serializedManager = new SerializedObject(
                    teleportManager);
                SerializedProperty buttons = serializedManager.FindProperty(
                    "m_teleportLocationButtons");
                SerializedProperty siteIDs = serializedManager.FindProperty(
                    "m_siteOfGraceIDs");
                List<int> ids = new List<int>();

                Assert.That(buttons.arraySize, Is.GreaterThan(0));
                Assert.That(buttons.arraySize, Is.EqualTo(siteIDs.arraySize));
                for (int index = 0; index < buttons.arraySize; index++)
                {
                    Component button = buttons.GetArrayElementAtIndex(index)
                        .objectReferenceValue as Component;
                    int siteID = siteIDs.GetArrayElementAtIndex(index).intValue;
                    ids.Add(siteID);
                    Assert.That(button, Is.Not.Null);
                    Assert.That(siteID, Is.GreaterThan(0));
                    Assert.That(
                        GetPersistentMethodNames(button),
                        Does.Contain("TeleportToSiteOfGrace"));
                }

                Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Count));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [TestCase(0, false)]
        [TestCase(1, true)]
        [TestCase(2, false)]
        [TestCase(4, false)]
        public void FastTravelIsDisabledOutsideSinglePlayer(
            int playerCount,
            bool expected)
        {
            MethodInfo method = GetRuntimeType(
                "ZZ.PlayerUITeleportLocationManager").GetMethod(
                "IsFastTravelAllowed",
                BindingFlags.Public | BindingFlags.Static);

            Assert.That(
                method.Invoke(null, new object[] { playerCount }),
                Is.EqualTo(expected));
        }

        [Test]
        public void RestCompletionOpensGraceMenuAndTravelClosesModalInput()
        {
            string siteSource = File.ReadAllText(
                "Assets/_Game/Scripts/World/Interactions/SiteOfGraceInteractable.cs");
            string uiSource = File.ReadAllText(
                "Assets/_Game/Scripts/UI/Gameplay/Player/PlayerUIManager.cs");

            Assert.That(siteSource, Does.Contain("OpenSiteOfGraceMenu()"));
            Assert.That(siteSource, Does.Contain("TeleportLocalPlayer()"));
            Assert.That(uiSource, Does.Contain("CloseSiteOfGraceMenu()"));
            Assert.That(uiSource, Does.Contain("CloseTeleportLocationMenu()"));
            Assert.That(
                uiSource,
                Does.Contain("IsTeleportLocationMenuOpen == true"));
        }

        [Test]
        public void WeaponSwitchingCancelsTwoHandingBeforeSelection()
        {
            string inventorySource = File.ReadAllText(
                "Assets/_Game/Scripts/Characters/Common/Inventory/PlayerInventoryManager.cs");
            Type networkType = GetRuntimeType("ZZ.PlayerNetworkManager");

            Assert.That(networkType.GetMethod("CancelTwoHanding"), Is.Not.Null);
            Assert.That(
                CountOccurrences(
                    inventorySource,
                    "m_player.PlayerNetworkManager.CancelTwoHanding();"),
                Is.EqualTo(2));
        }

        private static Type GetRuntimeType(string fullName)
        {
            Type type = Type.GetType($"{fullName}, Assembly-CSharp");
            Assert.That(type, Is.Not.Null, fullName);
            return type;
        }

        private static Component FindComponent(
            GameObject root,
            string typeName)
        {
            Component component = root.GetComponentsInChildren<Component>(true)
                .Single(candidate => candidate.GetType().Name == typeName);
            Assert.That(component, Is.Not.Null, typeName);
            return component;
        }

        private static T GetProperty<T>(object target, string propertyName)
        {
            return (T)target.GetType().GetProperty(propertyName)?.GetValue(target);
        }

        private static string[] GetPersistentMethodNames(Component button)
        {
            object onClick = button.GetType().GetProperty("onClick")
                ?.GetValue(button);
            int count = (int)onClick.GetType()
                .GetMethod("GetPersistentEventCount")
                ?.Invoke(onClick, null);
            string[] methodNames = new string[count];
            for (int index = 0; index < count; index++)
            {
                methodNames[index] = (string)onClick.GetType()
                    .GetMethod("GetPersistentMethodName")
                    ?.Invoke(onClick, new object[] { index });
            }

            return methodNames;
        }

        private static int CountOccurrences(string source, string value)
        {
            int count = 0;
            int index = 0;
            while ((index = source.IndexOf(
                       value,
                       index,
                       StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += value.Length;
            }

            return count;
        }
    }
}
