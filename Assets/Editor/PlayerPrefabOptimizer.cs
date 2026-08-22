using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace ZZ.Editor
{
    /// <summary>
    /// Rebuilds the data Player prefab directly on top of the rigged art Player prefab,
    /// removing the redundant low-poly placeholder root and nested visual embedding.
    /// </summary>
    public static class PlayerPrefabOptimizer
    {
        private const string k_ArtPrefabPath =
            "Assets/Art/Models/Rigged/Characters/Humanoid/Player/Player.prefab";
        private const string k_DataPrefabPath = "Assets/Data/Prefabs/Player.prefab";
        private const string k_BackupPath = "Assets/Data/Prefabs/Player_Backup_Old.prefab";
        private const string k_PlayerLayerName = "Player";

        [MenuItem("Tools/Elden/Optimize Player Prefab")]
        public static void OptimizePlayerPrefab()
        {
            if (!AssetDatabase.CopyAsset(k_DataPrefabPath, k_BackupPath))
            {
                Debug.LogError("PlayerPrefabOptimizer: could not back up the old Player prefab.");
                return;
            }

            GameObject artRoot = PrefabUtility.LoadPrefabContents(k_ArtPrefabPath);
            GameObject oldRoot = PrefabUtility.LoadPrefabContents(k_DataPrefabPath);
            try
            {
                MigrateGameplayComponents(artRoot, oldRoot);
                AddWeaponSlotsAndGroundCheck(artRoot, oldRoot);
                PrefabUtility.SaveAsPrefabAsset(artRoot, k_DataPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(artRoot);
                PrefabUtility.UnloadPrefabContents(oldRoot);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"PlayerPrefabOptimizer: optimized {k_DataPrefabPath}. Backup at {k_BackupPath}.");
        }

        [MenuItem("Tools/Elden/Relink Player Prefab References")]
        public static void RelinkPlayerPrefabReferences()
        {
            GameObject playerRoot = PrefabUtility.LoadPrefabContents(k_DataPrefabPath);
            try
            {
                PlayerLocomotionManager locomotionManager =
                    playerRoot.GetComponent<PlayerLocomotionManager>();
                Transform groundCheck = playerRoot.transform.Find("Ground Check Point");
                if (locomotionManager != null && groundCheck != null)
                {
                    SetObjectReference(locomotionManager, "m_groundCheckPoint", groundCheck);
                    EditorUtility.SetDirty(locomotionManager);
                }

                PrefabUtility.SaveAsPrefabAsset(playerRoot, k_DataPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(playerRoot);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("PlayerPrefabOptimizer: relinked the ground check reference.");
        }

        private static void MigrateGameplayComponents(GameObject artRoot, GameObject oldRoot)
        {
            artRoot.layer = oldRoot.layer;
            artRoot.name = oldRoot.name;

            GameObject artAnimatorObject = FindAnimatorObject(artRoot);
            GameObject oldAnimatorObject = FindAnimatorObject(oldRoot);

            // Components living on the Animator's GameObject (animation events fire there).
            CopyComponent<AudioSource>(oldAnimatorObject, artAnimatorObject);
            CopyComponent<PlayerAnimatorManager>(oldAnimatorObject, artAnimatorObject);
            CopyComponent<PlayerSoundFXManager>(oldAnimatorObject, artAnimatorObject);
            PreserveAnimatorController(oldAnimatorObject, artAnimatorObject);

            // Components living on the character root.
            CopyComponent<NetworkObject>(oldRoot, artRoot);
            CopyComponent<Rigidbody>(oldRoot, artRoot);
            CopyComponent<CharacterController>(oldRoot, artRoot);
            CopyComponent<PlayerManager>(oldRoot, artRoot);
            CopyComponent<PlayerEffectsManager>(oldRoot, artRoot);

            // PlayerManager auto-adds its required components; restore their authored values.
            CopyComponent<PlayerLocomotionManager>(oldRoot, artRoot);
            CopyComponent<PlayerNetworkManager>(oldRoot, artRoot);
            CopyComponent<PlayerStatsManager>(oldRoot, artRoot);
            CopyComponent<PlayerInventoryManager>(oldRoot, artRoot);
            CopyComponent<PlayerEquipmentManager>(oldRoot, artRoot);
            CopyComponent<PlayerCombatManager>(oldRoot, artRoot);
        }

        private static void AddWeaponSlotsAndGroundCheck(GameObject artRoot, GameObject oldRoot)
        {
            WeaponModelInstantiationSlot[] oldSlots =
                oldRoot.GetComponentsInChildren<WeaponModelInstantiationSlot>(true);
            foreach (WeaponModelInstantiationSlot oldSlot in oldSlots)
            {
                Transform newParent = ResolveSlotParent(artRoot, oldSlot.transform);
                GameObject newSlot = new GameObject(oldSlot.name);
                Transform newSlotTransform = newSlot.transform;
                newSlotTransform.SetParent(newParent, false);
                newSlotTransform.localPosition = oldSlot.transform.localPosition;
                newSlotTransform.localRotation = oldSlot.transform.localRotation;
                newSlotTransform.localScale = oldSlot.transform.localScale;
                WeaponModelInstantiationSlot newSlotComponent =
                    newSlot.AddComponent<WeaponModelInstantiationSlot>();
                EditorUtility.CopySerialized(oldSlot, newSlotComponent);
            }

            Transform oldGroundCheck = oldRoot.transform.Find("Ground Check Point");
            if (oldGroundCheck == null)
            {
                return;
            }

            GameObject newGroundCheck = new GameObject("Ground Check Point");
            Transform newGroundCheckTransform = newGroundCheck.transform;
            newGroundCheckTransform.SetParent(artRoot.transform, false);
            newGroundCheckTransform.localPosition = oldGroundCheck.localPosition;
            newGroundCheckTransform.localRotation = oldGroundCheck.localRotation;
            newGroundCheck.layer = oldGroundCheck.gameObject.layer;
        }

        private static Transform ResolveSlotParent(GameObject artRoot, Transform oldSlot)
        {
            string parentBoneName = oldSlot.parent != null ? oldSlot.parent.name : string.Empty;
            if (!string.IsNullOrEmpty(parentBoneName))
            {
                Transform matchingBone = FindTransformByName(artRoot.transform, parentBoneName);
                if (matchingBone != null)
                {
                    return matchingBone;
                }

                Debug.LogWarning(
                    $"PlayerPrefabOptimizer: bone '{parentBoneName}' not found; " +
                    $"parenting '{oldSlot.name}' to the prefab root.");
            }

            return artRoot.transform;
        }

        private static GameObject FindAnimatorObject(GameObject root)
        {
            Animator animator = root.GetComponentInChildren<Animator>(true);
            return animator != null ? animator.gameObject : root;
        }

        private static void PreserveAnimatorController(
            GameObject oldAnimatorObject,
            GameObject artAnimatorObject)
        {
            Animator oldAnimator = oldAnimatorObject.GetComponent<Animator>();
            Animator artAnimator = artAnimatorObject.GetComponent<Animator>();
            if (oldAnimator == null ||
                artAnimator == null ||
                oldAnimator.runtimeAnimatorController == null)
            {
                return;
            }

            // The art prefab's default controller lacks the gameplay Action Override layer.
            artAnimator.runtimeAnimatorController = oldAnimator.runtimeAnimatorController;
            EditorUtility.SetDirty(artAnimator);
        }

        private static Transform FindTransformByName(Transform root, string name)
        {
            if (root.name == name)
            {
                return root;
            }

            foreach (Transform child in root)
            {
                Transform match = FindTransformByName(child, name);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static void CopyComponent<T>(GameObject source, GameObject target)
            where T : Component
        {
            if (source == null || target == null)
            {
                return;
            }

            T sourceComponent = source.GetComponent<T>();
            if (sourceComponent == null)
            {
                return;
            }

            T targetComponent = target.GetComponent<T>();
            if (targetComponent == null)
            {
                targetComponent = target.AddComponent<T>();
            }

            EditorUtility.CopySerialized(sourceComponent, targetComponent);
        }

        private static void SetObjectReference(
            Component component,
            string propertyName,
            UnityEngine.Object value)
        {
            SerializedObject serializedObject = new SerializedObject(component);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogWarning($"PlayerPrefabOptimizer: missing property {propertyName}.");
                return;
            }

            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
