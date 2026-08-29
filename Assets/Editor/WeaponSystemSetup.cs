using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ZZ.Editor
{
    public static class WeaponSystemSetup
    {
        private const string k_PlayerPrefabPath = "Assets/Data/Prefabs/Player.prefab";
        private const string k_MainMenuScenePath = WorldScenePathLayout.MainMenuScenePath;
        private const string k_InputActionsPath = "Assets/_Game/Settings/Input/PlayerControls.inputactions";
        private const string k_ControllerPath =
            "Assets/Art/Animations/Animator Controllers/Humanoid/" +
            "Humanoid Animator Controller.controller";
        private const string k_RightSwapClipPath =
            "Assets/Art/Animations/Characters/Humanoid/Actions/core_oh_equip_R_01.anim";
        private const string k_LeftSwapClipPath =
            "Assets/Art/Animations/Characters/Humanoid/Actions/core_oh_equip_L_01.anim";
        private const string k_UpperBodyMaskPath =
            "Assets/Data/Animations/Upper Body Weapon Mask.mask";
        private const string k_ItemFolder =
            "Assets/Data/Items/Weapons/Melee Weapons";
        private const string k_WeaponPrefabFolder =
            "Assets/Data/Prefabs/Weapons/Melee Weapons";
        private const string k_WorldItemDatabasePrefabPath =
            "Assets/Data/Prefabs/Word Managers/World Item Database.prefab";
        private const string k_UnarmedAssetPath = k_ItemFolder + "/Unarmed.asset";
        private const string k_StraightSwordAssetPath =
            k_ItemFolder + "/Straight Sword.asset";
        private const string k_BroadswordAssetPath = k_ItemFolder + "/Broadsword.asset";
        private const string k_MediumShieldAssetPath =
            k_ItemFolder + "/Medium Shield.asset";
        private const string k_HeadEquipmentAssetPath =
            "Assets/Data/Items/Armor/Starter Hood.asset";
        private const string k_BodyEquipmentAssetPath =
            "Assets/Data/Items/Armor/Starter Armor.asset";
        private const string k_HandEquipmentAssetPath =
            "Assets/Data/Items/Armor/Starter Gauntlets.asset";
        private const string k_LegEquipmentAssetPath =
            "Assets/Data/Items/Armor/Starter Greaves.asset";
        private const string k_UnarmedPrefabPath =
            k_WeaponPrefabFolder + "/Unarmed.prefab";
        private const string k_StraightSwordPrefabPath =
            k_WeaponPrefabFolder + "/Straight Sword.prefab";
        private const string k_BroadswordPrefabPath =
            k_WeaponPrefabFolder + "/Broadsword.prefab";
        private const string k_StraightSwordModelPath =
            "Assets/Art/Models/Equipment/Weapons/Sword/Md_Prop_StraightSword_01.obj";
        private const string k_BroadswordModelPath =
            "Assets/Art/Models/Equipment/Weapons/Sword/Md_Prop_StraightSword_03.obj";
        private const string k_DamageLayerName = "Damage Collider";
        private const string k_UpperBodyLayerName = "Upper Body Override";
        private const string k_EmptyStateName = "Empty";
        private const string k_RightSwapStateName = "Swap_Right_Weapon_01";
        private const string k_LeftSwapStateName = "Swap_Left_Weapon_01";
        private const int k_UnarmedID = 0;
        private const int k_StraightSwordID = 1;
        private const int k_BroadswordID = 2;

        [MenuItem("Tools/Elden/Configure Weapon System")]
        public static void ConfigureWeaponSystem()
        {
            EnsureFolder(k_ItemFolder);
            EnsureFolder(k_WeaponPrefabFolder);
            EnsureFolder("Assets/Data/Animations");

            GameObject unarmedPrefab = ConfigureWeaponPrefab(
                k_UnarmedPrefabPath,
                null,
                new Vector3(0f, 0.08f, 0f),
                new Vector3(0.18f, 0.2f, 0.18f));
            GameObject straightSwordPrefab = ConfigureWeaponPrefab(
                k_StraightSwordPrefabPath,
                LoadRequiredAsset<GameObject>(k_StraightSwordModelPath),
                Vector3.zero,
                Vector3.zero);
            GameObject broadswordPrefab = ConfigureWeaponPrefab(
                k_BroadswordPrefabPath,
                LoadRequiredAsset<GameObject>(k_BroadswordModelPath),
                Vector3.zero,
                Vector3.zero);

            MeleeWeaponItem unarmed = ConfigureWeaponItem(
                k_UnarmedAssetPath,
                "Unarmed",
                "Bare hands used when no weapon is equipped.",
                k_UnarmedID,
                unarmedPrefab,
                true,
                5f,
                8f,
                5f);
            MeleeWeaponItem straightSword = ConfigureWeaponItem(
                k_StraightSwordAssetPath,
                "Straight Sword",
                "A balanced straight sword suitable for one-handed combat.",
                k_StraightSwordID,
                straightSwordPrefab,
                false,
                20f,
                20f,
                10f);
            MeleeWeaponItem broadsword = ConfigureWeaponItem(
                k_BroadswordAssetPath,
                "Broadsword",
                "A heavier sword that trades stamina for stronger physical damage.",
                k_BroadswordID,
                broadswordPrefab,
                false,
                30f,
                25f,
                15f);

            ConfigureWorldItemDatabasePrefab(unarmed, straightSword, broadsword);
            ConfigurePlayerPrefab(unarmed, straightSword, broadsword);
            ConfigureAnimator();
            ConfigureMainMenuScene();
            AssetDatabase.SaveAssets();
            ValidateWeaponSystem();
            Debug.Log(
                "[WeaponSystemSetup] Configured weapon assets, equipment slots, input, " +
                "upper-body swaps, and network catalog reconstruction.");
        }

        [MenuItem("Tools/Elden/Validate Weapon System")]
        public static void ValidateWeaponSystem()
        {
            MeleeWeaponItem unarmed = LoadRequiredAsset<MeleeWeaponItem>(k_UnarmedAssetPath);
            MeleeWeaponItem straightSword =
                LoadRequiredAsset<MeleeWeaponItem>(k_StraightSwordAssetPath);
            MeleeWeaponItem broadsword =
                LoadRequiredAsset<MeleeWeaponItem>(k_BroadswordAssetPath);

            ValidateWeaponItem(unarmed, k_UnarmedID, true, k_UnarmedPrefabPath);
            ValidateWeaponItem(
                straightSword,
                k_StraightSwordID,
                false,
                k_StraightSwordPrefabPath);
            ValidateWeaponItem(
                broadsword,
                k_BroadswordID,
                false,
                k_BroadswordPrefabPath);
            ValidateWeaponPrefab(k_UnarmedPrefabPath, false);
            ValidateWeaponPrefab(k_StraightSwordPrefabPath, true);
            ValidateWeaponPrefab(k_BroadswordPrefabPath, true);
            ValidateWorldItemDatabase(unarmed, straightSword, broadsword);
            ValidatePlayerPrefab(unarmed, straightSword, broadsword);
            ValidateAnimator();
            ValidateInputActions();
            ValidateMainMenuScene();
            Debug.Log(
                "[WeaponSystemValidation] Item IDs, quick slots, hand models, damage " +
                "colliders, input, animation, and database bootstrap are valid.");
        }

        private static GameObject ConfigureWeaponPrefab(
            string prefabPath,
            GameObject modelSource,
            Vector3 fallbackColliderCenter,
            Vector3 fallbackColliderSize)
        {
            GameObject weaponRoot = new GameObject(System.IO.Path.GetFileNameWithoutExtension(
                prefabPath));
            try
            {
                WeaponManager weaponManager = weaponRoot.AddComponent<WeaponManager>();
                GameObject pivotObject = new GameObject("Weapon Pivot");
                pivotObject.transform.SetParent(weaponRoot.transform, false);

                Bounds modelBounds = new Bounds(fallbackColliderCenter, fallbackColliderSize);
                bool hasModelBounds = false;
                if (modelSource != null)
                {
                    GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(modelSource);
                    model.name = "Weapon Mesh";
                    model.transform.SetParent(pivotObject.transform, false);
                    model.transform.localPosition = Vector3.zero;
                    model.transform.localRotation = Quaternion.identity;
                    model.transform.localScale = Vector3.one;
                    hasModelBounds = TryCalculateLocalBounds(
                        pivotObject.transform,
                        model,
                        out modelBounds);
                }

                GameObject colliderObject = new GameObject("Damage Collider");
                colliderObject.layer = LayerMask.NameToLayer(k_DamageLayerName);
                colliderObject.transform.SetParent(pivotObject.transform, false);
                BoxCollider boxCollider = colliderObject.AddComponent<BoxCollider>();
                boxCollider.isTrigger = true;
                boxCollider.enabled = false;
                boxCollider.center = modelBounds.center;
                boxCollider.size = hasModelBounds
                    ? ScaleColliderToBlade(modelBounds.size)
                    : modelBounds.size;
                MeleeWeaponDamageCollider damageCollider =
                    colliderObject.AddComponent<MeleeWeaponDamageCollider>();
                SetObjectReference(
                    weaponManager,
                    "m_meleeDamageCollider",
                    damageCollider);

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(weaponRoot, prefabPath);
                return prefab != null
                    ? prefab
                    : throw new InvalidOperationException($"Could not save {prefabPath}.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(weaponRoot);
            }
        }

        private static MeleeWeaponItem ConfigureWeaponItem(
            string assetPath,
            string itemName,
            string description,
            int itemID,
            GameObject weaponPrefab,
            bool isUnarmed,
            float physicalDamage,
            float staminaCost,
            float poiseDamage)
        {
            MeleeWeaponItem weapon = AssetDatabase.LoadAssetAtPath<MeleeWeaponItem>(assetPath);
            if (weapon == null)
            {
                weapon = ScriptableObject.CreateInstance<MeleeWeaponItem>();
                AssetDatabase.CreateAsset(weapon, assetPath);
            }

            SerializedObject serializedWeapon = new SerializedObject(weapon);
            SetString(serializedWeapon, "m_itemName", itemName);
            SetString(serializedWeapon, "m_itemDescription", description);
            SetInt(serializedWeapon, "m_itemID", itemID);
            SetObjectReference(serializedWeapon, "m_weaponModel", weaponPrefab);
            SetBool(serializedWeapon, "m_isUnarmed", isUnarmed);
            SetFloat(serializedWeapon, "m_physicalDamage", physicalDamage);
            SetFloat(serializedWeapon, "m_magicDamage", 0f);
            SetFloat(serializedWeapon, "m_fireDamage", 0f);
            SetFloat(serializedWeapon, "m_lightningDamage", 0f);
            SetFloat(serializedWeapon, "m_holyDamage", 0f);
            SetInt(serializedWeapon, "m_strengthRequirement", isUnarmed ? 0 : 10);
            SetInt(serializedWeapon, "m_dexterityRequirement", isUnarmed ? 0 : 10);
            SetInt(serializedWeapon, "m_intelligenceRequirement", 0);
            SetInt(serializedWeapon, "m_faithRequirement", 0);
            SetFloat(serializedWeapon, "m_baseStaminaCost", staminaCost);
            SetFloat(serializedWeapon, "m_basePoiseDamage", poiseDamage);
            serializedWeapon.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(weapon);
            return weapon;
        }

        private static void ConfigureWorldItemDatabasePrefab(
            WeaponItem unarmed,
            WeaponItem straightSword,
            WeaponItem broadsword)
        {
            GameObject root = new GameObject("World Item Database");
            try
            {
                WorldItemDatabase database = root.AddComponent<WorldItemDatabase>();
                WeaponItem mediumShield =
                    AssetDatabase.LoadAssetAtPath<WeaponItem>(
                        k_MediumShieldAssetPath);
                List<UnityEngine.Object> items = new List<UnityEngine.Object>
                {
                    unarmed,
                    straightSword,
                    broadsword
                };
                if (mediumShield != null)
                {
                    items.Add(mediumShield);
                }

                HeadEquipmentItem head =
                    AssetDatabase.LoadAssetAtPath<HeadEquipmentItem>(
                        k_HeadEquipmentAssetPath);
                BodyEquipmentItem body =
                    AssetDatabase.LoadAssetAtPath<BodyEquipmentItem>(
                        k_BodyEquipmentAssetPath);
                HandEquipmentItem hands =
                    AssetDatabase.LoadAssetAtPath<HandEquipmentItem>(
                        k_HandEquipmentAssetPath);
                LegEquipmentItem legs =
                    AssetDatabase.LoadAssetAtPath<LegEquipmentItem>(
                        k_LegEquipmentAssetPath);
                if (head != null && body != null && hands != null && legs != null)
                {
                    items.Add(head);
                    items.Add(body);
                    items.Add(hands);
                    items.Add(legs);
                    SetObjectArray(database, "m_headEquipment", new UnityEngine.Object[] { head });
                    SetObjectArray(database, "m_bodyEquipment", new UnityEngine.Object[] { body });
                    SetObjectArray(database, "m_handEquipment", new UnityEngine.Object[] { hands });
                    SetObjectArray(database, "m_legEquipment", new UnityEngine.Object[] { legs });
                }

                SetObjectArray(database, "m_items", items.ToArray());
                if (PrefabUtility.SaveAsPrefabAsset(root, k_WorldItemDatabasePrefabPath) == null)
                {
                    throw new InvalidOperationException(
                        $"Could not save {k_WorldItemDatabasePrefabPath}.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ConfigurePlayerPrefab(
            WeaponItem unarmed,
            WeaponItem straightSword,
            WeaponItem broadsword)
        {
            GameObject playerRoot = PrefabUtility.LoadPrefabContents(k_PlayerPrefabPath);
            try
            {
                PlayerInventoryManager inventory =
                    GetOrAddComponent<PlayerInventoryManager>(playerRoot);
                GetOrAddComponent<PlayerEquipmentManager>(playerRoot);
                SetObjectReference(inventory, "m_unarmedWeapon", unarmed);
                SetObjectArray(
                    inventory,
                    "m_weaponsInRightHandSlots",
                    new UnityEngine.Object[] { straightSword, broadsword, unarmed });
                WeaponItem mediumShield =
                    AssetDatabase.LoadAssetAtPath<WeaponItem>(
                        k_MediumShieldAssetPath);
                SetObjectArray(
                    inventory,
                    "m_weaponsInLeftHandSlots",
                    mediumShield != null
                        ? new UnityEngine.Object[]
                        {
                            mediumShield,
                            broadsword,
                            unarmed
                        }
                        : new UnityEngine.Object[]
                        {
                            broadsword,
                            unarmed,
                            unarmed
                        });

                Animator animator = playerRoot.GetComponentInChildren<Animator>(true);
                if (animator == null || !animator.isHuman)
                {
                    throw new InvalidOperationException(
                        "Player prefab requires a Humanoid Animator for weapon slots.");
                }

                ConfigureWeaponSlot(
                    animator.GetBoneTransform(HumanBodyBones.RightHand),
                    "Right Hand Weapon Slot",
                    WeaponModelSlot.RightHandSlot);
                ConfigureWeaponSlot(
                    animator.GetBoneTransform(HumanBodyBones.LeftHand),
                    "Left Hand Weapon Slot",
                    WeaponModelSlot.LeftHandSlot);
                if (mediumShield != null)
                {
                    ConfigureWeaponSlot(
                        animator.GetBoneTransform(HumanBodyBones.LeftHand),
                        "Left Hand Shield Slot",
                        WeaponModelSlot.LeftHandShieldSlot);
                }

                PrefabUtility.SaveAsPrefabAsset(playerRoot, k_PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(playerRoot);
            }
        }

        private static void ConfigureWeaponSlot(
            Transform hand,
            string slotName,
            WeaponModelSlot slotType)
        {
            if (hand == null)
            {
                throw new InvalidOperationException($"Player avatar is missing {slotType}.");
            }

            Transform slotTransform = hand.Find(slotName);
            if (slotTransform == null)
            {
                GameObject slotObject = new GameObject(slotName);
                slotTransform = slotObject.transform;
                slotTransform.SetParent(hand, false);
            }

            slotTransform.localPosition = Vector3.zero;
            slotTransform.localRotation = Quaternion.identity;
            slotTransform.localScale = Vector3.one;
            WeaponModelInstantiationSlot slot =
                GetOrAddComponent<WeaponModelInstantiationSlot>(slotTransform.gameObject);
            SetEnum(slot, "m_weaponModelSlot", (int)slotType);
        }

        private static void ConfigureAnimator()
        {
            AnimatorController controller = LoadRequiredAsset<AnimatorController>(k_ControllerPath);
            AnimationClip rightSwapClip = LoadRequiredAsset<AnimationClip>(k_RightSwapClipPath);
            AnimationClip leftSwapClip = LoadRequiredAsset<AnimationClip>(k_LeftSwapClipPath);
            AvatarMask mask = ConfigureUpperBodyMask();
            AnimatorControllerLayer layer = GetOrCreateUpperBodyLayer(controller, mask);
            AnimatorStateMachine stateMachine = layer.stateMachine;
            AnimatorState emptyState = GetOrCreateState(stateMachine, k_EmptyStateName);
            AnimatorState rightSwapState = GetOrCreateState(
                stateMachine,
                k_RightSwapStateName);
            AnimatorState leftSwapState = GetOrCreateState(
                stateMachine,
                k_LeftSwapStateName);

            emptyState.motion = null;
            rightSwapState.motion = rightSwapClip;
            leftSwapState.motion = leftSwapClip;
            stateMachine.defaultState = emptyState;
            ConfigureReturnTransition(rightSwapState, emptyState);
            ConfigureReturnTransition(leftSwapState, emptyState);
            EditorUtility.SetDirty(emptyState);
            EditorUtility.SetDirty(rightSwapState);
            EditorUtility.SetDirty(leftSwapState);
            EditorUtility.SetDirty(stateMachine);
            EditorUtility.SetDirty(controller);
        }

        private static AvatarMask ConfigureUpperBodyMask()
        {
            AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(k_UpperBodyMaskPath);
            if (mask == null)
            {
                mask = new AvatarMask();
                AssetDatabase.CreateAsset(mask, k_UpperBodyMaskPath);
            }

            for (int bodyPart = 0; bodyPart < (int)AvatarMaskBodyPart.LastBodyPart; bodyPart++)
            {
                mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)bodyPart, false);
            }

            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftHandIK, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightHandIK, true);
            EditorUtility.SetDirty(mask);
            return mask;
        }

        private static AnimatorControllerLayer GetOrCreateUpperBodyLayer(
            AnimatorController controller,
            AvatarMask mask)
        {
            AnimatorControllerLayer[] layers = controller.layers;
            int layerIndex = Array.FindIndex(
                layers,
                candidate => candidate.name == k_UpperBodyLayerName);
            if (layerIndex < 0)
            {
                AnimatorStateMachine stateMachine = new AnimatorStateMachine
                {
                    name = k_UpperBodyLayerName
                };
                AssetDatabase.AddObjectToAsset(stateMachine, controller);
                controller.AddLayer(new AnimatorControllerLayer
                {
                    name = k_UpperBodyLayerName,
                    defaultWeight = 1f,
                    blendingMode = AnimatorLayerBlendingMode.Override,
                    avatarMask = mask,
                    stateMachine = stateMachine
                });
                layers = controller.layers;
                layerIndex = layers.Length - 1;
            }

            layers[layerIndex].defaultWeight = 1f;
            layers[layerIndex].blendingMode = AnimatorLayerBlendingMode.Override;
            layers[layerIndex].avatarMask = mask;
            controller.layers = layers;
            return controller.layers[layerIndex];
        }

        private static AnimatorState GetOrCreateState(
            AnimatorStateMachine stateMachine,
            string stateName)
        {
            AnimatorState state = stateMachine.states
                .Select(childState => childState.state)
                .FirstOrDefault(candidate => candidate.name == stateName);
            return state ?? stateMachine.AddState(stateName);
        }

        private static void ConfigureReturnTransition(
            AnimatorState sourceState,
            AnimatorState destinationState)
        {
            AnimatorStateTransition transition = sourceState.transitions
                .FirstOrDefault(candidate => candidate.destinationState == destinationState) ??
                sourceState.AddTransition(destinationState);
            transition.hasExitTime = true;
            transition.exitTime = 0.9f;
            transition.hasFixedDuration = true;
            transition.duration = 0.1f;
            transition.canTransitionToSelf = false;
            transition.conditions = Array.Empty<AnimatorCondition>();
            EditorUtility.SetDirty(transition);
        }

        private static void ConfigureMainMenuScene()
        {
            Scene scene = SceneManager.GetSceneByPath(k_MainMenuScenePath);
            bool openedForSetup = !scene.IsValid() || !scene.isLoaded;
            if (openedForSetup)
            {
                scene = EditorSceneManager.OpenScene(
                    k_MainMenuScenePath,
                    OpenSceneMode.Additive);
            }

            try
            {
                WorldItemDatabase database = scene.GetRootGameObjects()
                    .Select(root => root.GetComponent<WorldItemDatabase>())
                    .FirstOrDefault(candidate => candidate != null);
                if (database == null)
                {
                    GameObject databasePrefab =
                        LoadRequiredAsset<GameObject>(k_WorldItemDatabasePrefabPath);
                    if (PrefabUtility.InstantiatePrefab(databasePrefab, scene) == null)
                    {
                        throw new InvalidOperationException(
                            "Could not add World Item Database to the main menu Scene.");
                    }
                }

                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                if (openedForSetup)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void ValidateWeaponItem(
            WeaponItem weapon,
            int expectedID,
            bool expectedUnarmed,
            string expectedPrefabPath)
        {
            GameObject expectedPrefab = LoadRequiredAsset<GameObject>(expectedPrefabPath);
            if (weapon.ItemID != expectedID ||
                weapon.IsUnarmed != expectedUnarmed ||
                weapon.WeaponModel != expectedPrefab)
            {
                throw new InvalidOperationException(
                    $"Weapon asset {weapon.name} has invalid identity or model data.");
            }
        }

        private static void ValidateWeaponPrefab(string prefabPath, bool expectsMesh)
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                Transform pivot = prefabRoot.transform.Find("Weapon Pivot");
                Transform mesh = pivot?.Find("Weapon Mesh");
                Transform colliderTransform = pivot?.Find("Damage Collider");
                BoxCollider boxCollider = colliderTransform?.GetComponent<BoxCollider>();
                MeleeWeaponDamageCollider damageCollider =
                    colliderTransform?.GetComponent<MeleeWeaponDamageCollider>();
                WeaponManager weaponManager = prefabRoot.GetComponent<WeaponManager>();
                if (pivot == null ||
                    expectsMesh && mesh == null ||
                    !expectsMesh && mesh != null ||
                    boxCollider == null ||
                    damageCollider == null ||
                    weaponManager == null ||
                    !boxCollider.isTrigger ||
                    boxCollider.enabled ||
                    colliderTransform.gameObject.layer != LayerMask.NameToLayer(k_DamageLayerName))
                {
                    throw new InvalidOperationException(
                        $"Weapon prefab {prefabPath} has an invalid model or damage collider hierarchy.");
                }

                ValidateObjectReference(
                    weaponManager,
                    "m_meleeDamageCollider",
                    damageCollider);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static void ValidateWorldItemDatabase(
            WeaponItem unarmed,
            WeaponItem straightSword,
            WeaponItem broadsword)
        {
            GameObject databasePrefab =
                LoadRequiredAsset<GameObject>(k_WorldItemDatabasePrefabPath);
            WorldItemDatabase database =
                GetRequiredComponent<WorldItemDatabase>(databasePrefab);
            SerializedProperty items = GetRequiredProperty(
                new SerializedObject(database),
                "m_items");
            WeaponItem mediumShield =
                AssetDatabase.LoadAssetAtPath<WeaponItem>(
                    k_MediumShieldAssetPath);
            List<UnityEngine.Object> expectedItems = new List<UnityEngine.Object>
            {
                unarmed,
                straightSword,
                broadsword
            };
            if (mediumShield != null)
            {
                expectedItems.Add(mediumShield);
            }

            HeadEquipmentItem head =
                AssetDatabase.LoadAssetAtPath<HeadEquipmentItem>(
                    k_HeadEquipmentAssetPath);
            BodyEquipmentItem body =
                AssetDatabase.LoadAssetAtPath<BodyEquipmentItem>(
                    k_BodyEquipmentAssetPath);
            HandEquipmentItem hands =
                AssetDatabase.LoadAssetAtPath<HandEquipmentItem>(
                    k_HandEquipmentAssetPath);
            LegEquipmentItem legs =
                AssetDatabase.LoadAssetAtPath<LegEquipmentItem>(
                    k_LegEquipmentAssetPath);
            if (head != null && body != null && hands != null && legs != null)
            {
                expectedItems.Add(head);
                expectedItems.Add(body);
                expectedItems.Add(hands);
                expectedItems.Add(legs);
            }

            if (items.arraySize != expectedItems.Count)
            {
                throw new InvalidOperationException(
                    "World Item Database does not contain all configured weapons.");
            }

            for (int itemIndex = 0; itemIndex < expectedItems.Count; itemIndex++)
            {
                if (items.GetArrayElementAtIndex(itemIndex).objectReferenceValue !=
                    expectedItems[itemIndex])
                {
                    throw new InvalidOperationException(
                        $"World Item Database ID {itemIndex} is not stable.");
                }
            }
        }

        private static void ValidatePlayerPrefab(
            WeaponItem unarmed,
            WeaponItem straightSword,
            WeaponItem broadsword)
        {
            GameObject playerRoot = PrefabUtility.LoadPrefabContents(k_PlayerPrefabPath);
            try
            {
                PlayerInventoryManager inventory =
                    GetRequiredComponent<PlayerInventoryManager>(playerRoot);
                GetRequiredComponent<PlayerEquipmentManager>(playerRoot);
                ValidateObjectReference(inventory, "m_unarmedWeapon", unarmed);
                ValidateObjectArray(
                    inventory,
                    "m_weaponsInRightHandSlots",
                    new UnityEngine.Object[] { straightSword, broadsword, unarmed });
                WeaponItem mediumShield =
                    AssetDatabase.LoadAssetAtPath<WeaponItem>(
                        k_MediumShieldAssetPath);
                ValidateObjectArray(
                    inventory,
                    "m_weaponsInLeftHandSlots",
                    mediumShield != null
                        ? new UnityEngine.Object[]
                        {
                            mediumShield,
                            broadsword,
                            unarmed
                        }
                        : new UnityEngine.Object[]
                        {
                            broadsword,
                            unarmed,
                            unarmed
                        });

                WeaponModelInstantiationSlot[] slots =
                    playerRoot.GetComponentsInChildren<WeaponModelInstantiationSlot>(true);
                int expectedSlotCount = mediumShield != null ? 3 : 2;
                if (slots.Length != expectedSlotCount ||
                    slots.Count(slot =>
                        slot.WeaponModelSlot == WeaponModelSlot.RightHandSlot) != 1 ||
                    slots.Count(slot =>
                        slot.WeaponModelSlot == WeaponModelSlot.LeftHandSlot) != 1 ||
                    mediumShield != null && slots.Count(slot =>
                        slot.WeaponModelSlot ==
                            WeaponModelSlot.LeftHandShieldSlot) != 1)
                {
                    throw new InvalidOperationException(
                        "Player prefab has invalid weapon or shield attachment slots.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(playerRoot);
            }
        }

        private static void ValidateAnimator()
        {
            AnimatorController controller = LoadRequiredAsset<AnimatorController>(k_ControllerPath);
            AnimationClip rightSwapClip = LoadRequiredAsset<AnimationClip>(k_RightSwapClipPath);
            AnimationClip leftSwapClip = LoadRequiredAsset<AnimationClip>(k_LeftSwapClipPath);
            AvatarMask expectedMask = LoadRequiredAsset<AvatarMask>(k_UpperBodyMaskPath);
            AnimatorControllerLayer layer = controller.layers.FirstOrDefault(
                candidate => candidate.name == k_UpperBodyLayerName) ??
                throw new InvalidOperationException(
                    "Animator is missing Upper Body Override.");
            AnimatorState rightState = GetRequiredState(
                layer.stateMachine,
                k_RightSwapStateName);
            AnimatorState leftState = GetRequiredState(
                layer.stateMachine,
                k_LeftSwapStateName);
            if (layer.avatarMask != expectedMask ||
                layer.defaultWeight != 1f ||
                layer.blendingMode != AnimatorLayerBlendingMode.Override ||
                rightState.motion != rightSwapClip ||
                leftState.motion != leftSwapClip)
            {
                throw new InvalidOperationException(
                    "Upper-body weapon swap layer is not configured correctly.");
            }
        }

        private static void ValidateInputActions()
        {
            InputActionAsset inputActions =
                LoadRequiredAsset<InputActionAsset>(k_InputActionsPath);
            InputActionMap movementMap = inputActions.FindActionMap("Player Movement", true);
            InputAction rightAction = movementMap.FindAction("Switch Right Weapon", true);
            InputAction leftAction = movementMap.FindAction("Switch Left Weapon", true);
            if (!HasBinding(rightAction, "<Gamepad>/dpad/right") ||
                !HasBinding(rightAction, "<Keyboard>/e") ||
                !HasBinding(leftAction, "<Gamepad>/dpad/left") ||
                !HasBinding(leftAction, "<Keyboard>/q"))
            {
                throw new InvalidOperationException(
                    "Weapon switch input requires D-Pad Right/Left and E/Q bindings.");
            }
        }

        private static void ValidateMainMenuScene()
        {
            Scene scene = SceneManager.GetSceneByPath(k_MainMenuScenePath);
            bool openedForValidation = !scene.IsValid() || !scene.isLoaded;
            if (openedForValidation)
            {
                scene = EditorSceneManager.OpenScene(
                    k_MainMenuScenePath,
                    OpenSceneMode.Additive);
            }

            try
            {
                int databaseCount = scene.GetRootGameObjects().Count(root =>
                    root.GetComponent<WorldItemDatabase>() != null);
                if (databaseCount != 1)
                {
                    throw new InvalidOperationException(
                        "Main menu Scene must bootstrap exactly one World Item Database.");
                }
            }
            finally
            {
                if (openedForValidation)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static bool TryCalculateLocalBounds(
            Transform relativeTo,
            GameObject model,
            out Bounds bounds)
        {
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                bounds = default;
                return false;
            }

            Vector3 minimum = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            Vector3 maximum = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            foreach (Renderer renderer in renderers)
            {
                Bounds rendererBounds = renderer.bounds;
                Vector3 rendererMinimum = relativeTo.InverseTransformPoint(rendererBounds.min);
                Vector3 rendererMaximum = relativeTo.InverseTransformPoint(rendererBounds.max);
                minimum = Vector3.Min(minimum, rendererMinimum);
                maximum = Vector3.Max(maximum, rendererMaximum);
            }

            bounds = new Bounds((minimum + maximum) * 0.5f, maximum - minimum);
            return true;
        }

        private static Vector3 ScaleColliderToBlade(Vector3 modelSize)
        {
            Vector3 colliderSize = modelSize;
            int longestAxis = modelSize.x >= modelSize.y && modelSize.x >= modelSize.z
                ? 0
                : modelSize.y >= modelSize.z ? 1 : 2;
            colliderSize[longestAxis] *= 0.82f;
            return colliderSize;
        }

        private static AnimatorState GetRequiredState(
            AnimatorStateMachine stateMachine,
            string stateName)
        {
            return stateMachine.states
                .Select(childState => childState.state)
                .FirstOrDefault(candidate => candidate.name == stateName) ??
                throw new InvalidOperationException($"Animator is missing {stateName}.");
        }

        private static bool HasBinding(InputAction action, string bindingPath)
        {
            return action.bindings.Any(binding => binding.path == bindingPath);
        }

        private static void EnsureFolder(string folderPath)
        {
            string[] segments = folderPath.Split('/');
            string currentPath = segments[0];
            for (int segmentIndex = 1; segmentIndex < segments.Length; segmentIndex++)
            {
                string nextPath = $"{currentPath}/{segments[segmentIndex]}";
                if (!AssetDatabase.IsValidFolder(nextPath))
                {
                    AssetDatabase.CreateFolder(currentPath, segments[segmentIndex]);
                }

                currentPath = nextPath;
            }
        }

        private static T GetRequiredComponent<T>(GameObject gameObject) where T : Component
        {
            T component = gameObject.GetComponent<T>();
            return component != null
                ? component
                : throw new InvalidOperationException(
                    $"{gameObject.name} is missing {typeof(T).Name}.");
        }

        private static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
        {
            T component = gameObject.GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
        }

        private static T LoadRequiredAsset<T>(string assetPath) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            return asset != null
                ? asset
                : throw new InvalidOperationException($"Could not load {assetPath}.");
        }

        private static void SetObjectReference(
            UnityEngine.Object target,
            string propertyName,
            UnityEngine.Object value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SetObjectReference(serializedObject, propertyName, value);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObjectReference(
            SerializedObject serializedObject,
            string propertyName,
            UnityEngine.Object value)
        {
            GetRequiredProperty(serializedObject, propertyName).objectReferenceValue = value;
        }

        private static void SetObjectArray(
            UnityEngine.Object target,
            string propertyName,
            UnityEngine.Object[] values)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = GetRequiredProperty(serializedObject, propertyName);
            property.arraySize = values.Length;
            for (int valueIndex = 0; valueIndex < values.Length; valueIndex++)
            {
                property.GetArrayElementAtIndex(valueIndex).objectReferenceValue = values[valueIndex];
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetString(
            SerializedObject serializedObject,
            string propertyName,
            string value)
        {
            GetRequiredProperty(serializedObject, propertyName).stringValue = value;
        }

        private static void SetBool(
            SerializedObject serializedObject,
            string propertyName,
            bool value)
        {
            GetRequiredProperty(serializedObject, propertyName).boolValue = value;
        }

        private static void SetInt(
            SerializedObject serializedObject,
            string propertyName,
            int value)
        {
            GetRequiredProperty(serializedObject, propertyName).intValue = value;
        }

        private static void SetFloat(
            SerializedObject serializedObject,
            string propertyName,
            float value)
        {
            GetRequiredProperty(serializedObject, propertyName).floatValue = value;
        }

        private static void SetEnum(
            UnityEngine.Object target,
            string propertyName,
            int value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            GetRequiredProperty(serializedObject, propertyName).enumValueIndex = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ValidateObjectReference(
            UnityEngine.Object target,
            string propertyName,
            UnityEngine.Object expectedValue)
        {
            SerializedProperty property = GetRequiredProperty(
                new SerializedObject(target),
                propertyName);
            if (property.objectReferenceValue != expectedValue)
            {
                throw new InvalidOperationException(
                    $"{target.name}.{propertyName} is not assigned correctly.");
            }
        }

        private static void ValidateObjectArray(
            UnityEngine.Object target,
            string propertyName,
            UnityEngine.Object[] expectedValues)
        {
            SerializedProperty property = GetRequiredProperty(
                new SerializedObject(target),
                propertyName);
            if (property.arraySize != expectedValues.Length)
            {
                throw new InvalidOperationException(
                    $"{target.name}.{propertyName} has the wrong size.");
            }

            for (int valueIndex = 0; valueIndex < expectedValues.Length; valueIndex++)
            {
                if (property.GetArrayElementAtIndex(valueIndex).objectReferenceValue !=
                    expectedValues[valueIndex])
                {
                    throw new InvalidOperationException(
                        $"{target.name}.{propertyName}[{valueIndex}] is invalid.");
                }
            }
        }

        private static SerializedProperty GetRequiredProperty(
            SerializedObject serializedObject,
            string propertyName)
        {
            return serializedObject.FindProperty(propertyName) ??
                throw new InvalidOperationException(
                    $"Could not find {serializedObject.targetObject.GetType().Name}." +
                    propertyName);
        }
    }
}
