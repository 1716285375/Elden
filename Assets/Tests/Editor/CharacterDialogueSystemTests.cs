using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ZZ.Tests
{
    public class CharacterDialogueSystemTests
    {
        private const string k_StageZeroPath =
            "Assets/Data/Dialogue/Nameless Knight/" +
            "Nameless Knight Stage 00.asset";
        private const string k_StageFivePath =
            "Assets/Data/Dialogue/Nameless Knight/" +
            "Nameless Knight Stage 05.asset";
        private const string k_DialogueInteractablePath =
            "Assets/Data/Prefabs/World Objects/Dialogue/" +
            "Dialogue Interactable.prefab";
        private const string k_NamelessKnightPath =
            "Assets/Data/Prefabs/Characters/AI/Nameless Knight NPC.prefab";
        private const string k_WorldAIManagerPath =
            "Assets/Data/Prefabs/Word Managers/World AI Manager.prefab";
        private const string k_WorldSaveManagerPath =
            "Assets/Data/Prefabs/Word Managers/World Save Game Manager.prefab";
        private const string k_PlayerUIManagerPath =
            "Assets/Data/Prefabs/Word Managers/Player UI Manager.prefab";
        private const string k_WorldScenePath =
            "Assets/Scenes/Levels/LV01_AbandonedMonastery/SCN_LV01_AbandonedMonastery.unity";
        private const string k_NetworkPrefabsPath =
            "Assets/_Game/Settings/Networking/DefaultNetworkPrefabs.asset";

        [Test]
        public static void RuntimeCopyOwnsIndependentDialogueProgress()
        {
            Type dialogueType = GetRuntimeType("ZZ.CharacterDialogue");
            UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath(
                k_StageZeroPath,
                dialogueType);
            UnityEngine.Object firstCopy = Invoke<UnityEngine.Object>(
                asset,
                "CreateRuntimeCopy");
            UnityEngine.Object secondCopy = Invoke<UnityEngine.Object>(
                asset,
                "CreateRuntimeCopy");
            try
            {
                dialogueType.GetMethod("AdvanceDialogue")?.Invoke(firstCopy, null);

                Assert.That(GetPropertyValue<int>(firstCopy, "DialogueIndex"),
                    Is.EqualTo(1));
                Assert.That(GetPropertyValue<int>(secondCopy, "DialogueIndex"),
                    Is.Zero);
                Assert.That(GetPropertyValue<int>(asset, "DialogueIndex"),
                    Is.Zero);
                Assert.That(firstCopy.hideFlags, Is.EqualTo(HideFlags.DontSave));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(firstCopy);
                UnityEngine.Object.DestroyImmediate(secondCopy);
            }
        }

        [Test]
        public static void AuthoredDialogueStagesAreCompleteAndOrdered()
        {
            Type dialogueType = GetRuntimeType("ZZ.CharacterDialogue");
            UnityEngine.Object stageZero = AssetDatabase.LoadAssetAtPath(
                k_StageZeroPath,
                dialogueType);
            UnityEngine.Object stageFive = AssetDatabase.LoadAssetAtPath(
                k_StageFivePath,
                dialogueType);

            Assert.That(Invoke<bool>(stageZero, "ValidateDialogueData", false),
                Is.True);
            Assert.That(GetPropertyValue<int>(stageZero, "RequiredStageID"),
                Is.Zero);
            Assert.That(GetPropertyValue<bool>(stageZero, "SetStageAfterDialogue"),
                Is.True);
            Assert.That(GetPropertyValue<int>(stageZero, "StageIDToSet"),
                Is.EqualTo(5));
            Assert.That(GetPropertyValue<int>(stageZero, "CoreLineCount"),
                Is.EqualTo(2));
            Assert.That(Invoke<bool>(stageFive, "ValidateDialogueData", false),
                Is.True);
            Assert.That(GetPropertyValue<int>(stageFive, "RequiredStageID"),
                Is.EqualTo(5));
            Assert.That(GetPropertyValue<bool>(stageFive, "SetStageAfterDialogue"),
                Is.False);
        }

        [Test]
        public static void DialogueStageSurvivesSaveJsonRoundTrip()
        {
            CharacterSaveData source = new CharacterSaveData
            {
                NamelessKnightStageID = 5
            };

            string json = JsonUtility.ToJson(source);
            CharacterSaveData restored =
                JsonUtility.FromJson<CharacterSaveData>(json);

            Assert.That(restored.NamelessKnightStageID, Is.EqualTo(5));
            Assert.That(json, Does.Contain("m_namelessKnightStageID"));
        }

        [Test]
        public static void PlaybackSequenceAdvancesOnlyAfterEachCoreLine()
        {
            string source = ReadRuntimeSource(
                "Character/AI/AICharacterSoundFXManager.cs");
            int greetingIndex = source.IndexOf(
                "DialoguePlaybackSection.Greeting",
                StringComparison.Ordinal);
            int coreIndex = source.IndexOf(
                "while (m_playbackSection == DialoguePlaybackSection.Core",
                StringComparison.Ordinal);
            int farewellIndex = source.IndexOf(
                "DialoguePlaybackSection.Farewell",
                coreIndex,
                StringComparison.Ordinal);
            int playLineIndex = source.IndexOf(
                "yield return PlayDialogueLine(dialogueLine, dialogueClip);",
                coreIndex,
                StringComparison.Ordinal);
            int advanceIndex = source.IndexOf(
                "m_currentDialogue.AdvanceDialogue();",
                playLineIndex,
                StringComparison.Ordinal);

            Assert.That(greetingIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(coreIndex, Is.GreaterThan(greetingIndex));
            Assert.That(farewellIndex, Is.GreaterThan(coreIndex));
            Assert.That(advanceIndex, Is.GreaterThan(playLineIndex));
        }

        [Test]
        public static void SkipCancelAndEndHaveDistinctSemantics()
        {
            string source = ReadRuntimeSource(
                "Character/AI/AICharacterSoundFXManager.cs");
            string cancelBody = SliceMethod(
                source,
                "public void CancelCurrentDialogueEvent()",
                "public void OnCurrentDialogueEventEnded()");
            string endBody = SliceMethod(
                source,
                "public void OnCurrentDialogueEventEnded()",
                "public void RegisterDialogueInteractable(");
            string skipBody = SliceMethod(
                source,
                "private void SkipCurrentDialogueLine()",
                "private void ResolveCurrentDialogue()");

            Assert.That(cancelBody, Does.Contain("OnDialogueCanceled"));
            Assert.That(cancelBody, Does.Not.Contain("SetStageOfDialogue"));
            Assert.That(endBody, Does.Contain("OnDialogueEnded"));
            Assert.That(endBody, Does.Contain("SetStageOfDialogue"));
            Assert.That(skipBody, Does.Contain("AdvanceDialogue"));
            Assert.That(skipBody, Does.Contain("OnCurrentDialogueEventEnded"));
        }

        [Test]
        public static void DialogueInteractableIsReusableAndNetworked()
        {
            GameObject prefab = LoadRequiredAsset<GameObject>(
                k_DialogueInteractablePath);
            Component interactable = prefab.GetComponent(
                GetRuntimeType("ZZ.DialogueInteractable"));
            SphereCollider collider = prefab.GetComponent<SphereCollider>();
            Rigidbody rigidbody = prefab.GetComponent<Rigidbody>();

            Assert.That(prefab.layer,
                Is.EqualTo(LayerMask.NameToLayer("Interactable")));
            Assert.That(
                prefab.GetComponent(GetRuntimeType("Unity.Netcode.NetworkObject")),
                Is.Not.Null);
            Assert.That(interactable, Is.Not.Null);
            Assert.That(GetPropertyValue<string>(interactable, "InteractableText"),
                Is.EqualTo("Talk"));
            Assert.That(GetPropertyValue<bool>(
                    interactable,
                    "IsHostOnlyInteractable"),
                Is.False);
            Assert.That(collider?.isTrigger, Is.True);
            Assert.That(collider?.radius, Is.EqualTo(2.5f).Within(0.001f));
            Assert.That(rigidbody?.isKinematic, Is.True);
            Assert.That(rigidbody?.constraints,
                Is.EqualTo(RigidbodyConstraints.FreezeAll));
        }

        [Test]
        public static void DialoguePopupIsBottomCenteredAndContentSized()
        {
            GameObject prefab = LoadRequiredAsset<GameObject>(
                k_PlayerUIManagerPath);
            Component manager = prefab.GetComponent(
                GetRuntimeType("ZZ.PlayerUIPopUpManager"));
            SerializedObject serializedManager = new SerializedObject(manager);
            GameObject popup = serializedManager
                .FindProperty("m_dialoguePopup")
                ?.objectReferenceValue as GameObject;
            UnityEngine.Object subtitle = serializedManager
                .FindProperty("m_dialogueSubtitleText")
                ?.objectReferenceValue;
            RectTransform popupRect = popup?.GetComponent<RectTransform>();

            Assert.That(popup, Is.Not.Null);
            Assert.That(popup.activeSelf, Is.False);
            Assert.That(subtitle, Is.Not.Null);
            Assert.That(subtitle.GetType().FullName, Does.StartWith("TMPro."));
            Assert.That(popup.GetComponent<ContentSizeFitter>(), Is.Not.Null);
            Assert.That(popup.GetComponent<VerticalLayoutGroup>(), Is.Not.Null);
            Assert.That(popupRect.anchorMin.y, Is.Zero.Within(0.001f));
            Assert.That(popupRect.anchorMax.y, Is.Zero.Within(0.001f));
            Assert.That(popupRect.anchoredPosition.y, Is.GreaterThanOrEqualTo(40f));
        }

        [Test]
        public static void DialogueNPCIsPassiveUntilExplicitlyTargeted()
        {
            GameObject prefab = LoadRequiredAsset<GameObject>(
                k_NamelessKnightPath);
            Component character = prefab.GetComponent(
                GetRuntimeType("ZZ.AICharacterManager"));
            Component soundFX = prefab.GetComponentInChildren(
                GetRuntimeType("ZZ.AICharacterSoundFXManager"),
                true);
            string aiSource = ReadRuntimeSource(
                "Character/AI/AICharacterManager.cs");

            Assert.That(character, Is.Not.Null);
            Assert.That(GetPropertyValue<bool>(character, "AutoAcquireTargets"),
                Is.False);
            Assert.That(soundFX, Is.Not.Null);
            Assert.That(
                Convert.ToInt32(GetPropertyValue<object>(
                    soundFX,
                    "CharacterDialogueID")),
                Is.EqualTo(1));
            Assert.That(aiSource, Does.Contain("if (!m_autoAcquireTargets)"));
            Assert.That(aiSource, Does.Contain("public bool SetTarget"));
        }

        [Test]
        public static void ServerSpawnsAndNetworkParentsDialogueTrigger()
        {
            string source = ReadRuntimeSource(
                "World Managers/AI/WorldAIManager.cs");
            GameObject managerPrefab = LoadRequiredAsset<GameObject>(
                k_WorldAIManagerPath);
            Component manager = managerPrefab.GetComponent(
                GetRuntimeType("ZZ.WorldAIManager"));
            Transform spawner = managerPrefab.transform.Find(
                "Nameless Knight Dialogue NPC Spawner");

            Assert.That(GetPropertyValue<object>(
                    manager,
                    "DialogueInteractablePrefab"),
                Is.Not.Null);
            Assert.That(spawner, Is.Not.Null);
            Assert.That(spawner.GetComponent(
                    GetRuntimeType("ZZ.AICharacterSpawner")),
                Is.Not.Null);
            Assert.That(source, Does.Contain("dialogueNetworkObject.Spawn(true)"));
            Assert.That(source, Does.Contain(
                "TrySetParent(aiNetworkObject, false)"));
        }

        [Test]
        public static void WorldSceneContainsConfiguredAIManager()
        {
            Scene scene = SceneManager.GetSceneByPath(k_WorldScenePath);
            bool openedByTest = !scene.IsValid() || !scene.isLoaded;
            if (openedByTest)
            {
                scene = EditorSceneManager.OpenScene(
                    k_WorldScenePath,
                    OpenSceneMode.Additive);
            }

            try
            {
                Type managerType = GetRuntimeType("ZZ.WorldAIManager");
                Component[] managers = scene.GetRootGameObjects()
                    .Select(root => root.GetComponent(managerType))
                    .Where(manager => manager != null)
                    .ToArray();

                Assert.That(managers, Has.Length.EqualTo(1));
                Assert.That(GetPropertyValue<object>(
                        managers[0],
                        "DialogueInteractablePrefab"),
                    Is.Not.Null);
                Assert.That(
                    managers[0].transform.Find(
                        "Nameless Knight Dialogue NPC Spawner"),
                    Is.Not.Null);
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
        public static void DialogueNetworkPrefabsAreRegisteredExactlyOnce()
        {
            ScriptableObject prefabsList =
                LoadRequiredAsset<ScriptableObject>(k_NetworkPrefabsPath);
            SerializedProperty entries = new SerializedObject(prefabsList)
                .FindProperty("List");
            GameObject interactable = LoadRequiredAsset<GameObject>(
                k_DialogueInteractablePath);
            GameObject npc = LoadRequiredAsset<GameObject>(k_NamelessKnightPath);

            Assert.That(CountNetworkPrefab(entries, interactable), Is.EqualTo(1));
            Assert.That(CountNetworkPrefab(entries, npc), Is.EqualTo(1));
        }

        [Test]
        public static void InteractionInputRemainsAvailableForDialogueSkip()
        {
            string interactionSource = ReadRuntimeSource(
                "Character/Player/PlayerInteractionManager.cs");
            string popupSource = ReadRuntimeSource(
                "Character/Player/Player UI/PlayerUIPopUpManager.cs");

            Assert.That(interactionSource, Does.Contain("IsDialoguePopupOpen"));
            Assert.That(interactionSource, Does.Contain(
                "Interactable interactable = m_activeInteractable"));
            Assert.That(interactionSource, Does.Contain(
                "interactable.Interact(m_player)"));
            Assert.That(popupSource, Does.Contain("SendDialoguePopup"));
            Assert.That(popupSource, Does.Contain("UpdateDialogueSubtitle"));
            Assert.That(popupSource, Does.Contain("CloseDialoguePopup"));
        }

        [Test]
        public static void SaveManagerOwnsStageLookupAndRuntimeCopyCreation()
        {
            GameObject prefab = LoadRequiredAsset<GameObject>(
                k_WorldSaveManagerPath);
            Component manager = prefab.GetComponent(
                GetRuntimeType("ZZ.WorldSaveGameManager"));
            SerializedProperty dialogues = new SerializedObject(manager)
                .FindProperty("m_namelessKnightDialogues");
            string source = ReadRuntimeSource(
                "World Managers/WorldSaveGameManager.cs");

            Assert.That(dialogues?.arraySize, Is.EqualTo(2));
            Assert.That(source, Does.Contain("GetCurrentDialogue"));
            Assert.That(source, Does.Contain("CreateRuntimeCopy()"));
            Assert.That(source, Does.Contain("GetStageIDsOnLoad()"));
            Assert.That(source, Does.Contain("NamelessKnightStageID"));
        }

        private static int CountNetworkPrefab(
            SerializedProperty entries,
            GameObject prefab)
        {
            int matches = 0;
            for (int entryIndex = 0; entryIndex < entries.arraySize; entryIndex++)
            {
                if (entries.GetArrayElementAtIndex(entryIndex)
                        .FindPropertyRelative("Prefab")
                        ?.objectReferenceValue == prefab)
                {
                    matches++;
                }
            }

            return matches;
        }

        private static string SliceMethod(
            string source,
            string startMarker,
            string endMarker)
        {
            int start = source.IndexOf(startMarker, StringComparison.Ordinal);
            int end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), startMarker);
            Assert.That(end, Is.GreaterThan(start), endMarker);
            return source.Substring(start, end - start);
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

        private static T GetPropertyValue<T>(
            object target,
            string propertyName)
        {
            Assert.That(target, Is.Not.Null, propertyName);
            object value = target.GetType().GetProperty(propertyName)?.GetValue(target);
            Assert.That(value, Is.Not.Null, propertyName);
            return (T)value;
        }

        private static T Invoke<T>(
            object target,
            string methodName,
            params object[] parameters)
        {
            Assert.That(target, Is.Not.Null, methodName);
            object result = target.GetType().GetMethod(methodName)?.Invoke(
                target,
                parameters);
            Assert.That(result, Is.Not.Null, methodName);
            return (T)result;
        }

        private static T LoadRequiredAsset<T>(string assetPath)
            where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            Assert.That(asset, Is.Not.Null, assetPath);
            return asset;
        }
    }
}
