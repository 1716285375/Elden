using System.Linq;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZZ.Editor
{
    /// <summary>Builds the EP175-177 ranger action, Animator, prefab variant, and registration.</summary>
    public static class AIRangerCombatSystemSetup
    {
        private const string k_SourcePrefabPath =
            "Assets/_Game/Prefabs/Characters/AI/Undead AI.prefab";
        private const string k_RangerPrefabDirectory =
            "Assets/_Game/Prefabs/Characters/AI/Ranger";
        private const string k_RangerPrefabPath =
            k_RangerPrefabDirectory + "/Ranger AI.prefab";
        private const string k_RangerActionDirectory =
            "Assets/_Game/Data/Actions/AI/Ranger";
        private const string k_RangerActionPath =
            k_RangerActionDirectory + "/Ranger_Attack_01.asset";
        private const string k_BowControllerPath =
            "Assets/_Game/Art/Characters/Shared/Humanoid/Animations/Combat/Bow/" +
            "Bow.overrideController";
        private const string k_ArcherControllerPath =
            "Assets/_Game/Art/Characters/Shared/Humanoid/Animations/Combat/Bow/" +
            "Archer Animator.overrideController";
        private const string k_BowDrawClipPath =
            "Assets/_Game/Art/Characters/Shared/Humanoid/Animations/Combat/Bow/" +
            "Bow_Draw.anim";
        private const string k_BowFireClipPath =
            "Assets/_Game/Art/Characters/Shared/Humanoid/Animations/Combat/Bow/" +
            "Bow_Fire.anim";
        private const string k_BowItemPath =
            "Assets/_Game/Data/Items/Weapons/Ranged Weapons/Longbow.asset";
        private const string k_ProjectilePath =
            "Assets/_Game/Data/Items/Projectiles/Standard Arrow.asset";
        private const string k_BowPrefabPath =
            "Assets/_Game/Prefabs/Equipment/Weapons/Longbow.prefab";
        private const string k_NetworkPrefabsPath =
            "Assets/_Game/Settings/Networking/DefaultNetworkPrefabs.asset";

        [InitializeOnLoadMethod]
        private static void BuildMissingRangerAssets()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(k_RangerPrefabPath) !=
                    null &&
                AssetDatabase.LoadAssetAtPath<AICharacterAttackAction>(
                    k_RangerActionPath) != null)
            {
                return;
            }

            EditorApplication.delayCall += ConfigureRangerCombatSystem;
        }

        [MenuItem("Tools/ZZ/AI/Configure Ranger Combat System")]
        public static void ConfigureRangerCombatSystem()
        {
            EnsureAssetFolder(k_RangerPrefabDirectory);
            EnsureAssetFolder(k_RangerActionDirectory);
            ConfigureAnimationEvents();
            RuntimeAnimatorController archerController =
                CreateArcherController();
            AICharacterAttackAction rangerAttack = CreateRangerAttack();
            GameObject rangerPrefab = CreateRangerPrefab(
                rangerAttack,
                archerController);
            RegisterNetworkPrefab(rangerPrefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                "[EP175-177] Ranger AI combat system configured and registered.");
        }

        private static void ConfigureAnimationEvents()
        {
            AnimationClip drawClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                k_BowDrawClipPath);
            AnimationClip fireClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                k_BowFireClipPath);
            if (drawClip == null || fireClip == null)
            {
                throw new System.InvalidOperationException(
                    "Ranger setup requires the existing bow draw and fire clips.");
            }

            SetSingleAnimationEvent(drawClip, "DrawProjectile", 0.2f);
            SetSingleAnimationEvent(fireClip, "ReleaseArrow", 0.05f);
        }

        private static void SetSingleAnimationEvent(
            AnimationClip clip,
            string functionName,
            float eventTime)
        {
            AnimationEvent[] events = AnimationUtility.GetAnimationEvents(clip)
                .Where(animationEvent =>
                    animationEvent.functionName != functionName)
                .Append(new AnimationEvent
                {
                    functionName = functionName,
                    time = Mathf.Clamp(eventTime, 0f, clip.length),
                    messageOptions = SendMessageOptions.RequireReceiver
                })
                .OrderBy(animationEvent => animationEvent.time)
                .ToArray();
            AnimationUtility.SetAnimationEvents(clip, events);
            EditorUtility.SetDirty(clip);
        }

        private static RuntimeAnimatorController CreateArcherController()
        {
            AnimatorOverrideController existing =
                AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(
                    k_ArcherControllerPath);
            if (existing != null)
            {
                return existing;
            }

            if (!AssetDatabase.CopyAsset(
                    k_BowControllerPath,
                    k_ArcherControllerPath))
            {
                throw new System.InvalidOperationException(
                    "Could not create the Archer Animator override.");
            }

            return AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(
                k_ArcherControllerPath);
        }

        private static AICharacterAttackAction CreateRangerAttack()
        {
            AICharacterAttackAction attack =
                AssetDatabase.LoadAssetAtPath<AICharacterAttackAction>(
                    k_RangerActionPath);
            if (attack == null)
            {
                attack = ScriptableObject.CreateInstance<AICharacterAttackAction>();
                AssetDatabase.CreateAsset(attack, k_RangerActionPath);
            }

            SerializedObject serializedAttack = new(attack);
            SetBool(serializedAttack, "m_isParryable", false);
            SetBool(serializedAttack, "m_useCharacterActionAnimation", true);
            SetEnum(
                serializedAttack,
                "m_characterActionAnimation",
                (int)CharacterActionAnimation.BowDraw);
            SetFloat(serializedAttack, "m_minimumRange", 1f);
            SetFloat(serializedAttack, "m_maximumRange", 20f);
            SetFloat(serializedAttack, "m_selectionWeight", 1f);
            SetFloat(serializedAttack, "m_recoveryTime", 2f);
            serializedAttack.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(attack);
            return attack;
        }

        private static GameObject CreateRangerPrefab(
            AICharacterAttackAction rangerAttack,
            RuntimeAnimatorController archerController)
        {
            GameObject sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                k_SourcePrefabPath);
            if (sourcePrefab == null)
            {
                throw new System.InvalidOperationException(
                    $"Missing ranger source prefab: {k_SourcePrefabPath}");
            }

            Scene previewScene = EditorSceneManager.NewPreviewScene();
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(
                sourcePrefab,
                previewScene);
            try
            {
                instance.name = "Ranger AI";
                AICharacterManager sourceManager = instance
                    .GetComponents<AICharacterManager>()
                    .Single(manager => manager.GetType() ==
                        typeof(AICharacterManager));
                AICharacterCombatManager sourceCombat = instance
                    .GetComponents<AICharacterCombatManager>()
                    .Single(combat => combat.GetType() ==
                        typeof(AICharacterCombatManager));
                AIRangerManager ranger = instance.AddComponent<AIRangerManager>();
                AIRangerCombatManager rangerCombat =
                    instance.GetComponent<AIRangerCombatManager>();
                AIRangerEquipmentManager rangerEquipment =
                    instance.GetComponent<AIRangerEquipmentManager>();
                EditorUtility.CopySerializedManagedFieldsOnly(
                    sourceManager,
                    ranger);
                EditorUtility.CopySerializedManagedFieldsOnly(
                    sourceCombat,
                    rangerCombat);
                Object.DestroyImmediate(sourceCombat);
                Object.DestroyImmediate(sourceManager);

                ConfigureRangerManager(ranger, rangerCombat, rangerAttack);
                ConfigureRangerPresentation(
                    instance,
                    rangerEquipment,
                    archerController);
                return PrefabUtility.SaveAsPrefabAsset(
                    instance,
                    k_RangerPrefabPath);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        private static void ConfigureRangerManager(
            AIRangerManager ranger,
            AIRangerCombatManager rangerCombat,
            AICharacterAttackAction rangerAttack)
        {
            SerializedObject serializedRanger = new(ranger);
            SetFloat(serializedRanger, "m_detectionRadius", 24f);
            SetFloat(serializedRanger, "m_loseTargetRadius", 35f);
            SetObject(serializedRanger, "m_defaultAttackAction", rangerAttack);
            SetObject(serializedRanger, "m_aiCombatManager", rangerCombat);
            SetBool(serializedRanger, "m_willCircleTarget", false);
            SetEnum(
                serializedRanger,
                "m_rangerPursuitMode",
                (int)PursuitMode.Run);
            SetEnum(
                serializedRanger,
                "m_rangerCombatMode",
                (int)PursuitMode.None);
            SerializedProperty attacks = serializedRanger.FindProperty(
                "m_combatStanceAttacks");
            attacks.arraySize = 1;
            attacks.GetArrayElementAtIndex(0).objectReferenceValue = rangerAttack;
            serializedRanger.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureRangerPresentation(
            GameObject rangerRoot,
            AIRangerEquipmentManager rangerEquipment,
            RuntimeAnimatorController archerController)
        {
            Animator characterAnimator = rangerRoot.GetComponentInChildren<Animator>(
                true);
            if (characterAnimator == null || !characterAnimator.isHuman)
            {
                throw new System.InvalidOperationException(
                    "Ranger source requires a humanoid Animator.");
            }

            characterAnimator.runtimeAnimatorController = archerController;
            Transform leftHand = characterAnimator.GetBoneTransform(
                HumanBodyBones.LeftHand);
            Transform rightHand = characterAnimator.GetBoneTransform(
                HumanBodyBones.RightHand);
            if (leftHand == null || rightHand == null)
            {
                throw new System.InvalidOperationException(
                    "Ranger source is missing humanoid hand bones.");
            }

            GameObject bowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                k_BowPrefabPath);
            GameObject bowObject = (GameObject)PrefabUtility.InstantiatePrefab(
                bowPrefab,
                leftHand);
            bowObject.name = "Ranger Bow";
            bowObject.transform.SetLocalPositionAndRotation(
                new Vector3(0.03f, 0.02f, 0.02f),
                Quaternion.Euler(0f, 90f, 90f));

            GameObject drawSlotObject = new("Arrow Instantiation Slot");
            drawSlotObject.transform.SetParent(rightHand, false);
            drawSlotObject.transform.SetLocalPositionAndRotation(
                new Vector3(0f, 0.015f, 0.12f),
                Quaternion.Euler(90f, 0f, 0f));

            SerializedObject serializedEquipment = new(rangerEquipment);
            SetObject(
                serializedEquipment,
                "m_bow",
                AssetDatabase.LoadAssetAtPath<RangedWeaponItem>(k_BowItemPath));
            SetObject(serializedEquipment, "m_bowObject", bowObject);
            SetObject(
                serializedEquipment,
                "m_bowAnimator",
                bowObject.GetComponentInChildren<Animator>(true));
            SetObject(
                serializedEquipment,
                "m_projectile",
                AssetDatabase.LoadAssetAtPath<RangedProjectileItem>(
                    k_ProjectilePath));
            SetObject(
                serializedEquipment,
                "m_drawHand",
                drawSlotObject.transform);
            serializedEquipment.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RegisterNetworkPrefab(GameObject rangerPrefab)
        {
            NetworkPrefabsList prefabs =
                AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(
                    k_NetworkPrefabsPath);
            if (prefabs == null || rangerPrefab == null || prefabs.Contains(rangerPrefab))
            {
                return;
            }

            prefabs.Add(new NetworkPrefab
            {
                Override = NetworkPrefabOverride.None,
                Prefab = rangerPrefab
            });
            EditorUtility.SetDirty(prefabs);
        }

        private static void SetObject(
            SerializedObject serializedObject,
            string propertyName,
            Object value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.objectReferenceValue = value;
            }
        }

        private static void SetFloat(
            SerializedObject serializedObject,
            string propertyName,
            float value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.floatValue = value;
            }
        }

        private static void SetBool(
            SerializedObject serializedObject,
            string propertyName,
            bool value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.boolValue = value;
            }
        }

        private static void SetEnum(
            SerializedObject serializedObject,
            string propertyName,
            int enumValue)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.enumValueIndex = enumValue;
            }
        }

        private static void EnsureAssetFolder(string folderPath)
        {
            string normalizedPath = folderPath.Replace('\\', '/');
            string currentPath = "Assets";
            foreach (string segment in normalizedPath
                         .Substring("Assets/".Length)
                         .Split('/'))
            {
                string childPath = currentPath + "/" + segment;
                if (!AssetDatabase.IsValidFolder(childPath))
                {
                    AssetDatabase.CreateFolder(currentPath, segment);
                }

                currentPath = childPath;
            }
        }
    }
}
