using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace ZZ.Tests
{
    public class LoadingScreenSystemTests
    {
        private const string k_PlayerUIPrefabPath =
            "Assets/_Game/Prefabs/World/Managers/Player UI Manager.prefab";

        [Test]
        public void PlayerUIPrefabContainsPersistentLoadingOverlay()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(
                k_PlayerUIPrefabPath);
            try
            {
                Component playerUI = FindComponent(root, "PlayerUIManager");
                Component manager = FindComponent(
                    root,
                    "PlayerUILoadingScreenManager");
                SerializedObject serializedUI = new SerializedObject(playerUI);
                SerializedObject serializedManager = new SerializedObject(manager);
                GameObject loadingScreen = serializedManager.FindProperty(
                    "m_loadingScreen").objectReferenceValue as GameObject;
                CanvasGroup canvasGroup = serializedManager.FindProperty(
                    "m_loadingScreenCanvasGroup")
                    .objectReferenceValue as CanvasGroup;

                Assert.That(
                    serializedUI.FindProperty("m_playerUILoadingScreenManager")
                        .objectReferenceValue,
                    Is.EqualTo(manager));
                Assert.That(loadingScreen, Is.Not.Null);
                Assert.That(loadingScreen.activeSelf, Is.False);
                Assert.That(canvasGroup, Is.Not.Null);
                Assert.That(canvasGroup.alpha, Is.EqualTo(1f));
                Assert.That(canvasGroup.blocksRaycasts, Is.False);
                Assert.That(loadingScreen.transform.Find("Background"), Is.Not.Null);
                Assert.That(loadingScreen.transform.Find("Loading Icon"), Is.Not.Null);
                Assert.That(
                    loadingScreen.GetComponentsInChildren<Component>(true)
                        .Any(component =>
                            component.GetType().Name ==
                                "FadeLoadingScreenIcon"),
                    Is.True);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [Test]
        public void LoadingManagerExposesImmediateActivationAndSingleFade()
        {
            Type managerType = GetRuntimeType(
                "ZZ.PlayerUILoadingScreenManager");
            string source = File.ReadAllText(
                "Assets/_Game/Scripts/UI/Gameplay/Player/" +
                "PlayerUILoadingScreenManager.cs");

            Assert.That(
                managerType.GetMethod("ActivateLoadingScreen"),
                Is.Not.Null);
            Assert.That(
                managerType.GetMethod(
                    "DeactivateLoadingScreen",
                    Type.EmptyTypes),
                Is.Not.Null);
            Assert.That(source, Does.Contain("m_loadingScreenCanvasGroup.alpha = 1f"));
            Assert.That(source, Does.Contain("IsFadingLoadingScreen"));
            Assert.That(source, Does.Contain("Time.unscaledDeltaTime"));
            Assert.That(
                source,
                Does.Contain("IsPerformingLoadingOperation == true"));
        }

        [Test]
        public void LoadingManagerOwnsSceneSubscriptionAndCoroutineCleanup()
        {
            string source = File.ReadAllText(
                "Assets/_Game/Scripts/UI/Gameplay/Player/" +
                "PlayerUILoadingScreenManager.cs");

            Assert.That(
                CountOccurrences(
                    source,
                    "SceneManager.activeSceneChanged += OnActiveSceneChanged;"),
                Is.EqualTo(1));
            Assert.That(
                CountOccurrences(
                    source,
                    "SceneManager.activeSceneChanged -= OnActiveSceneChanged;"),
                Is.EqualTo(1));
            Assert.That(source, Does.Contain("CancelFadeLoadingScreen();"));
        }

        [Test]
        public void LoadingIconUsesUnscaledLifecycleOwnedCoroutine()
        {
            string source = File.ReadAllText(
                "Assets/_Game/Scripts/UI/Gameplay/Player/" +
                "FadeLoadingScreenIcon.cs");

            Assert.That(source, Does.Contain("private void OnEnable()"));
            Assert.That(source, Does.Contain("private void OnDisable()"));
            Assert.That(source, Does.Contain("Time.unscaledDeltaTime"));
            Assert.That(source, Does.Contain("StopCoroutine"));
        }

        [Test]
        public void WorldAIManagerSeparatesThreeFixedUpdateOperations()
        {
            Type managerType = GetRuntimeType("ZZ.WorldAIManager");
            string source = File.ReadAllText(
                "Assets/_Game/Scripts/World/AI/WorldAIManager.cs");

            Assert.That(managerType.GetMethod("SpawnAllCharacters"), Is.Not.Null);
            Assert.That(managerType.GetMethod("ResetAllCharacters"), Is.Not.Null);
            Assert.That(managerType.GetMethod("DespawnAllCharacters"), Is.Not.Null);
            Assert.That(
                managerType.GetProperty("IsPerformingLoadingOperation"),
                Is.Not.Null);
            Assert.That(
                CountOccurrences(source, "new WaitForFixedUpdate()"),
                Is.EqualTo(3));

            string resetRoutine = GetSourceMethod(
                source,
                "private IEnumerator ResetAllCharactersRoutine()",
                "private IEnumerator DespawnAllCharactersRoutine()");
            Assert.That(resetRoutine, Does.Contain("ResetCharacter()"));
            Assert.That(resetRoutine, Does.Not.Contain("AttemptToSpawnCharacter"));
            Assert.That(resetRoutine, Does.Not.Contain("Despawn("));
        }

        [Test]
        public void SpawnerReusesCachedCharacterAndPreservesDefeatedBosses()
        {
            Type spawnerType = GetRuntimeType("ZZ.AICharacterSpawner");
            Type aiType = GetRuntimeType("ZZ.AICharacterManager");
            string source = File.ReadAllText(
                "Assets/_Game/Scripts/World/AI/AICharacterSpawner.cs");

            Assert.That(spawnerType.GetMethod("ResetCharacter"), Is.Not.Null);
            Assert.That(aiType.GetMethod("ResetAtSpawnPoint"), Is.Not.Null);
            Assert.That(source, Does.Contain("m_instantiatedCharacter.ResetAtSpawnPoint"));
            Assert.That(source, Does.Contain("BossProgressState.Defeated"));
        }

        [Test]
        public void ReusedAIResetsHealthDeathActionsAndFloatingHPBar()
        {
            string aiSource = File.ReadAllText(
                "Assets/_Game/Scripts/Characters/AI/AICharacterManager.cs");
            Type uiType = GetRuntimeType("ZZ.CharacterUIManager");

            Assert.That(
                uiType.GetMethod("ResetCharacterHPBar"),
                Is.Not.Null);
            Assert.That(aiSource, Does.Contain("CurrentHealth.Value = Mathf.Max"));
            Assert.That(aiSource, Does.Contain("IsDead.Value = false"));
            Assert.That(aiSource, Does.Contain("ResetActionFlags()"));
            Assert.That(aiSource, Does.Contain("ResetCharacterHPBar()"));
            Assert.That(aiSource, Does.Contain("WarpToSpawnPoint"));
        }

        [Test]
        public void SceneLoadingAndFastTravelUseLoadingOverlay()
        {
            string saveSource = File.ReadAllText(
                "Assets/_Game/Scripts/World/Managers/WorldSaveGameManager.cs");
            string travelSource = File.ReadAllText(
                "Assets/_Game/Scripts/UI/Gameplay/Player/" +
                "PlayerUITeleportLocationManager.cs");

            Assert.That(saveSource, Does.Contain("ActivateLoadingScreen()"));
            Assert.That(travelSource, Does.Contain("ActivateLoadingScreen()"));
            Assert.That(travelSource, Does.Contain("DeactivateLoadingScreen()"));
        }

        [Test]
        public void SavedSceneLoadSerializesRequestsAndRetriesNetcodeBusyState()
        {
            string source = File.ReadAllText(
                "Assets/_Game/Scripts/World/Managers/WorldSaveGameManager.cs");

            Assert.That(source, Does.Contain("m_sceneLoadIsInProgress"));
            Assert.That(source, Does.Contain("TryBeginSceneLoad("));
            Assert.That(source, Does.Contain(
                "SceneEventProgressStatus.SceneEventInProgress"));
            Assert.That(source, Does.Contain(
                "SceneEventType.LoadEventCompleted"));
            Assert.That(source, Does.Contain(
                "k_SceneEventRetryTimeoutSeconds"));
            Assert.That(source, Does.Contain(
                "networkSceneManager.OnSceneEvent -= HandleSceneEvent"));
        }

        [Test]
        public void SavedSceneLoadOnlyRetriesTransientBusyStatus()
        {
            MethodInfo shouldRetry = GetRuntimeType("ZZ.WorldSaveGameManager")
                .GetMethod(
                    "ShouldRetrySceneLoad",
                    BindingFlags.NonPublic | BindingFlags.Static);

            Assert.That(shouldRetry, Is.Not.Null);
            Assert.That(
                shouldRetry.Invoke(
                    null,
                    new object[]
                    {
                        SceneEventProgressStatus.SceneEventInProgress
                    }),
                Is.True);
            Assert.That(
                shouldRetry.Invoke(
                    null,
                    new object[] { SceneEventProgressStatus.InvalidSceneName }),
                Is.False);
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

        private static string GetSourceMethod(
            string source,
            string methodStart,
            string nextMethod)
        {
            int startIndex = source.IndexOf(
                methodStart,
                StringComparison.Ordinal);
            int endIndex = source.IndexOf(
                nextMethod,
                startIndex,
                StringComparison.Ordinal);
            Assert.That(startIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(endIndex, Is.GreaterThan(startIndex));
            return source.Substring(startIndex, endIndex - startIndex);
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
