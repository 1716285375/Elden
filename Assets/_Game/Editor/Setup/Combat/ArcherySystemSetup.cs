using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ZZ.Editor
{
    /// <summary>Creates and validates the EP82-86 archery data and presentation assets.</summary>
    public static class ArcherySystemSetup
    {
        private const string k_HumanoidControllerPath =
            "Assets/_Game/Art/Characters/Shared/Humanoid/AnimationControllers/Base/Humanoid/" +
            "Humanoid Animator Controller.controller";
        private const string k_BowOverridePath =
            "Assets/_Game/Art/Characters/Shared/Humanoid/Animations/Combat/Bow/Bow.overrideController";
        private const string k_BowWeaponControllerPath =
            "Assets/_Game/Art/Characters/Shared/Humanoid/Animations/Combat/Bow/Bow Weapon.controller";
        private const string k_BowPrefabPath =
            "Assets/_Game/Prefabs/Equipment/Weapons/Longbow.prefab";
        private const string k_DrawArrowPrefabPath =
            "Assets/_Game/Prefabs/Projectiles/Draw Arrow.prefab";
        private const string k_ReleaseArrowPrefabPath =
            "Assets/_Game/Prefabs/Projectiles/Released Arrow.prefab";
        private const string k_BowItemPath =
            "Assets/_Game/Data/Items/Weapons/Ranged Weapons/Longbow.asset";
        private const string k_StandardArrowPath =
            "Assets/_Game/Data/Items/Projectiles/Standard Arrow.asset";
        private const string k_FireArrowPath =
            "Assets/_Game/Data/Items/Projectiles/Fire Arrow.asset";
        private const string k_MainFireActionPath =
            "Assets/_Game/Data/Actions/Ranged/Fire Main Projectile.asset";
        private const string k_SecondaryFireActionPath =
            "Assets/_Game/Data/Actions/Ranged/Fire Secondary Projectile.asset";
        private const string k_AimActionPath =
            "Assets/_Game/Data/Actions/Ranged/Aim Bow.asset";
        private const string k_WorldItemDatabasePath =
            "Assets/_Game/Prefabs/World/Managers/World Item Database.prefab";
        private const string k_PlayerPrefabPath =
            "Assets/_Game/Prefabs/Characters/Player/Player.prefab";
        private const string k_UIManagerPrefabPath =
            "Assets/_Game/Prefabs/World/Managers/Player UI Manager.prefab";
        private const string k_MainMenuScenePath =
            WorldScenePathLayout.MainMenuScenePath;
        private const string k_ActionLayerName = "Action Override";
        private const string k_EmptyStateName = "Empty";
        private const string k_HasArrowNotched = "hasArrowNotched";
        private const string k_IsHoldingArrow = "isHoldingArrow";
        private const string k_IsAiming = "isAiming";

        private const string k_BowDrawClipPath =
            "Assets/_Game/Art/Characters/Shared/Humanoid/Animations/Combat/Bow/Bow_Draw.anim";
        private const string k_BowAimClipPath =
            "Assets/_Game/Art/Characters/Shared/Humanoid/Animations/Combat/Bow/Bow_Aim.anim";
        private const string k_BowFireClipPath =
            "Assets/_Game/Art/Characters/Shared/Humanoid/Animations/Combat/Bow/Bow_Fire.anim";
        private const string k_BowOutOfAmmoClipPath =
            "Assets/_Game/Art/Characters/Shared/Humanoid/Animations/Combat/Bow/Bow_Out_Of_Ammo.anim";
        private const string k_ModelDrawClipPath =
            "Assets/_Game/Art/Characters/Shared/Humanoid/Animations/Combat/Bow/BowModel_Draw.anim";
        private const string k_ModelAimClipPath =
            "Assets/_Game/Art/Characters/Shared/Humanoid/Animations/Combat/Bow/BowModel_Aim.anim";
        private const string k_ModelFireClipPath =
            "Assets/_Game/Art/Characters/Shared/Humanoid/Animations/Combat/Bow/BowModel_Fire.anim";

        [MenuItem("Tools/Elden/Configure Archery System")]
        public static void ConfigureArcherySystem()
        {
            EnsureFolders();
            ConfigureProjectileLayerCollisions();
            ConfigureAnimationClips();
            ConfigureCharacterAnimator();
            ConfigureBowWeaponAnimator();
            ConfigurePrefabs();
            ConfigureActionsAndItems();
            ConfigureWorldItemDatabase();
            ConfigurePlayerPrefab();
            ConfigureCrosshair();
            ConfigureMainMenuSystems();
            AssetDatabase.SaveAssets();
            ValidateArcherySystem();
            Debug.Log(
                "[ArcherySystemSetup] Configured bow, dual ammunition slots, " +
                "draw/fire animation events, aiming UI, and projectile prefabs.");
        }

        [MenuItem("Tools/Elden/Validate Archery System")]
        public static void ValidateArcherySystem()
        {
            ValidateItems();
            ValidateProjectilePrefab();
            ValidateAnimator();
            ValidatePlayerAndUI();
            Debug.Log(
                "[ArcherySystemValidation] Data separation, Animator flow, " +
                "projectile physics, network-ready player state, and crosshair are valid.");
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/_Game/Art/Characters/Shared/Humanoid/Animations", "Archery");
            EnsureFolder("Assets/_Game/Prefabs", "Weapons");
            EnsureFolder("Assets/_Game/Prefabs", "Projectiles");
            EnsureFolder("Assets/_Game/Data/Items/Weapons", "Ranged Weapons");
            EnsureFolder("Assets/_Game/Data/Items", "Projectiles");
            EnsureFolder("Assets/_Game/Data/Actions", "Ranged");
        }

        private static void ConfigureProjectileLayerCollisions()
        {
            int projectileLayer = LayerMask.NameToLayer("Projectile");
            int playerLayer = LayerMask.NameToLayer("Player");
            int damageableCharacterLayer = LayerMask.NameToLayer(
                "Damageable Character");
            if (projectileLayer < 0 ||
                playerLayer < 0 ||
                damageableCharacterLayer < 0)
            {
                throw new InvalidOperationException(
                    "Archery requires Projectile, Player, and Damageable Character layers.");
            }

            Physics.IgnoreLayerCollision(projectileLayer, playerLayer, true);
            Physics.IgnoreLayerCollision(
                projectileLayer,
                damageableCharacterLayer,
                true);
        }

        private static void ConfigureAnimationClips()
        {
            AnimationClip bowDraw = ConfigureTimingClip(
                k_BowDrawClipPath,
                0.6f,
                false);
            AnimationClip bowAim = ConfigureTimingClip(
                k_BowAimClipPath,
                1f,
                true);
            AnimationClip bowFire = ConfigureTimingClip(
                k_BowFireClipPath,
                0.75f,
                false);
            ConfigureTimingClip(k_BowOutOfAmmoClipPath, 0.55f, false);
            AnimationUtility.SetAnimationEvents(
                bowFire,
                new[]
                {
                    new AnimationEvent
                    {
                        functionName = "ReleaseArrow",
                        time = 0.05f,
                        messageOptions = SendMessageOptions.RequireReceiver
                    }
                });
            EditorUtility.SetDirty(bowDraw);
            EditorUtility.SetDirty(bowAim);
            EditorUtility.SetDirty(bowFire);

            ConfigureBowModelClip(k_ModelDrawClipPath, 0f, -0.15f, 0.35f, false);
            ConfigureBowModelClip(k_ModelAimClipPath, -0.15f, -0.15f, 1f, true);
            ConfigureBowModelClip(k_ModelFireClipPath, -0.15f, 0f, 0.25f, false);
        }

        private static AnimationClip ConfigureTimingClip(
            string assetPath,
            float duration,
            bool loopTime)
        {
            AnimationClip clip = LoadOrCreateAnimationClip(assetPath);
            clip.frameRate = 30f;
            clip.ClearCurves();
            clip.SetCurve(
                "__ArcheryTiming__",
                typeof(Transform),
                "m_LocalPosition.x",
                AnimationCurve.Constant(0f, duration, 0f));
            SetLoopTime(clip, loopTime);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static void ConfigureBowModelClip(
            string assetPath,
            float startPosition,
            float endPosition,
            float duration,
            bool loopTime)
        {
            AnimationClip clip = LoadOrCreateAnimationClip(assetPath);
            clip.frameRate = 30f;
            clip.ClearCurves();
            clip.SetCurve(
                "Bow String",
                typeof(Transform),
                "m_LocalPosition.z",
                AnimationCurve.Linear(
                    0f,
                    startPosition,
                    duration,
                    endPosition));
            SetLoopTime(clip, loopTime);
            EditorUtility.SetDirty(clip);
        }

        private static void ConfigureCharacterAnimator()
        {
            AnimatorController controller = LoadRequiredAsset<AnimatorController>(
                k_HumanoidControllerPath);
            EnsureBoolParameter(controller, k_HasArrowNotched);
            EnsureBoolParameter(controller, k_IsHoldingArrow);
            EnsureBoolParameter(controller, k_IsAiming);
            AnimatorStateMachine stateMachine = controller.layers
                .First(layer => layer.name == k_ActionLayerName)
                .stateMachine;
            AnimatorState empty = GetRequiredState(stateMachine, k_EmptyStateName);
            AnimatorState draw = ConfigureState(
                stateMachine,
                "Bow_Draw",
                k_BowDrawClipPath,
                new Vector3(3180f, 250f, 0f));
            AnimatorState aim = ConfigureState(
                stateMachine,
                "Bow_Aim",
                k_BowAimClipPath,
                new Vector3(3440f, 250f, 0f));
            AnimatorState fire = ConfigureState(
                stateMachine,
                "Bow_Fire",
                k_BowFireClipPath,
                new Vector3(3700f, 250f, 0f));
            AnimatorState outOfAmmo = ConfigureState(
                stateMachine,
                "Bow_Out_Of_Ammo",
                k_BowOutOfAmmoClipPath,
                new Vector3(3440f, 440f, 0f));

            ClearTransitions(draw);
            ClearTransitions(aim);
            ClearTransitions(fire);
            ClearTransitions(outOfAmmo);
            AddBoolTransition(draw, aim, k_IsHoldingArrow, true, true, 0.9f, 0.05f);
            AddBoolTransition(draw, fire, k_IsHoldingArrow, false, true, 0.5f, 0.04f);
            AddBoolTransition(draw, empty, k_HasArrowNotched, false, false, 0f, 0.04f);
            AddBoolTransition(aim, fire, k_IsHoldingArrow, false, false, 0f, 0.04f);
            AddBoolTransition(aim, empty, k_HasArrowNotched, false, false, 0f, 0.04f);
            AddExitTransition(fire, empty, 0.9f, 0.08f);
            AddExitTransition(outOfAmmo, empty, 0.9f, 0.08f);
            if (aim.behaviours.All(behaviour =>
                    behaviour is not ToggleNotchedArrowMovement))
            {
                aim.AddStateMachineBehaviour<ToggleNotchedArrowMovement>();
            }

            AnimatorOverrideController bowOverride =
                AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(
                    k_BowOverridePath);
            if (bowOverride == null)
            {
                bowOverride = new AnimatorOverrideController(controller);
                AssetDatabase.CreateAsset(bowOverride, k_BowOverridePath);
            }
            else
            {
                bowOverride.runtimeAnimatorController = controller;
            }

            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(bowOverride);
        }

        private static void ConfigureBowWeaponAnimator()
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                k_BowWeaponControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(
                    k_BowWeaponControllerPath);
            }

            EnsureBoolParameter(controller, k_HasArrowNotched);
            EnsureBoolParameter(controller, k_IsHoldingArrow);
            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState empty = GetOrAddState(
                stateMachine,
                "Empty",
                new Vector3(220f, 20f, 0f));
            stateMachine.defaultState = empty;
            AnimatorState draw = ConfigureState(
                stateMachine,
                "Draw",
                k_ModelDrawClipPath,
                new Vector3(440f, 20f, 0f));
            AnimatorState aim = ConfigureState(
                stateMachine,
                "Aim",
                k_ModelAimClipPath,
                new Vector3(660f, 20f, 0f));
            AnimatorState fire = ConfigureState(
                stateMachine,
                "Fire",
                k_ModelFireClipPath,
                new Vector3(880f, 20f, 0f));
            ClearTransitions(empty);
            ClearTransitions(draw);
            ClearTransitions(aim);
            ClearTransitions(fire);
            AddBoolTransition(empty, draw, k_HasArrowNotched, true, false, 0f, 0.03f);
            AddBoolTransition(draw, aim, k_IsHoldingArrow, true, true, 0.9f, 0.03f);
            AddBoolTransition(draw, fire, k_IsHoldingArrow, false, true, 0.5f, 0.03f);
            AddBoolTransition(draw, empty, k_HasArrowNotched, false, false, 0f, 0.03f);
            AddBoolTransition(aim, fire, k_IsHoldingArrow, false, false, 0f, 0.03f);
            AddBoolTransition(aim, empty, k_HasArrowNotched, false, false, 0f, 0.03f);
            AddExitTransition(fire, empty, 0.9f, 0.03f);
            EditorUtility.SetDirty(controller);
        }

        private static void ConfigurePrefabs()
        {
            CreateBowPrefab();
            CreateDrawArrowPrefab();
            CreateReleasedArrowPrefab();
        }

        private static void CreateBowPrefab()
        {
            GameObject root = new GameObject("Longbow");
            try
            {
                WeaponManager manager = root.AddComponent<WeaponManager>();
                Animator animator = root.AddComponent<Animator>();
                animator.runtimeAnimatorController =
                    LoadRequiredAsset<AnimatorController>(k_BowWeaponControllerPath);
                GameObject upperLimb = CreatePrimitiveChild(
                    root.transform,
                    "Upper Limb",
                    PrimitiveType.Cylinder,
                    new Vector3(0f, 0.38f, 0f),
                    new Vector3(0.04f, 0.42f, 0.04f),
                    new Vector3(0f, 0f, -18f));
                GameObject lowerLimb = CreatePrimitiveChild(
                    root.transform,
                    "Lower Limb",
                    PrimitiveType.Cylinder,
                    new Vector3(0f, -0.38f, 0f),
                    new Vector3(0.04f, 0.42f, 0.04f),
                    new Vector3(0f, 0f, 18f));
                GameObject bowString = new GameObject("Bow String");
                bowString.transform.SetParent(root.transform, false);
                CreatePrimitiveChild(
                    bowString.transform,
                    "String Upper",
                    PrimitiveType.Cylinder,
                    new Vector3(0f, 0.38f, 0f),
                    new Vector3(0.008f, 0.4f, 0.008f),
                    Vector3.zero);
                CreatePrimitiveChild(
                    bowString.transform,
                    "String Lower",
                    PrimitiveType.Cylinder,
                    new Vector3(0f, -0.38f, 0f),
                    new Vector3(0.008f, 0.4f, 0.008f),
                    Vector3.zero);
                upperLimb.name = "Upper Limb";
                lowerLimb.name = "Lower Limb";
                SerializedObject serializedManager = new SerializedObject(manager);
                SetObject(serializedManager, "m_weaponAnimator", animator);
                serializedManager.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, k_BowPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void CreateDrawArrowPrefab()
        {
            GameObject root = new GameObject("Draw Arrow");
            try
            {
                CreateArrowVisual(root.transform);
                SetLayerRecursively(root, LayerMask.NameToLayer("Projectile"));
                PrefabUtility.SaveAsPrefabAsset(root, k_DrawArrowPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void CreateReleasedArrowPrefab()
        {
            GameObject root = new GameObject("Released Arrow");
            try
            {
                int projectileLayer = LayerMask.NameToLayer("Projectile");
                int damageLayer = LayerMask.NameToLayer("Damage Collider");
                root.layer = projectileLayer;
                Rigidbody rigidbody = root.AddComponent<Rigidbody>();
                rigidbody.mass = 0.1f;
                rigidbody.useGravity = true;
                rigidbody.collisionDetectionMode =
                    CollisionDetectionMode.ContinuousDynamic;
                rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                CapsuleCollider physicalCollider = root.AddComponent<CapsuleCollider>();
                physicalCollider.direction = 2;
                physicalCollider.radius = 0.035f;
                physicalCollider.height = 1.05f;
                root.AddComponent<RangedProjectileManager>();
                CreateArrowVisual(root.transform);

                GameObject damageObject = new GameObject("Damage Collider");
                damageObject.layer = damageLayer;
                damageObject.transform.SetParent(root.transform, false);
                CapsuleCollider damageCollider =
                    damageObject.AddComponent<CapsuleCollider>();
                damageCollider.direction = 2;
                damageCollider.radius = 0.05f;
                damageCollider.height = 1.1f;
                damageCollider.isTrigger = true;
                damageObject.AddComponent<RangeProjectileDamageCollider>();
                PrefabUtility.SaveAsPrefabAsset(root, k_ReleaseArrowPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void CreateArrowVisual(Transform parent)
        {
            CreatePrimitiveChild(
                parent,
                "Arrow Shaft",
                PrimitiveType.Cylinder,
                Vector3.zero,
                new Vector3(0.018f, 0.5f, 0.018f),
                new Vector3(90f, 0f, 0f));
            CreatePrimitiveChild(
                parent,
                "Arrow Head",
                PrimitiveType.Sphere,
                new Vector3(0f, 0f, 0.56f),
                new Vector3(0.05f, 0.05f, 0.09f),
                Vector3.zero);
        }

        private static void ConfigureActionsAndItems()
        {
            FireProjectileWeaponItemAction mainAction =
                LoadOrCreateAsset<FireProjectileWeaponItemAction>(
                    k_MainFireActionPath);
            FireProjectileWeaponItemAction secondaryAction =
                LoadOrCreateAsset<FireProjectileWeaponItemAction>(
                    k_SecondaryFireActionPath);
            AimAction aimAction = LoadOrCreateAsset<AimAction>(k_AimActionPath);
            SetEnum(new SerializedObject(mainAction), "m_projectileSlot", ProjectileSlot.Main);
            SetEnum(
                new SerializedObject(secondaryAction),
                "m_projectileSlot",
                ProjectileSlot.Secondary);

            RangedProjectileItem standardArrow =
                LoadOrCreateAsset<RangedProjectileItem>(k_StandardArrowPath);
            RangedProjectileItem fireArrow =
                LoadOrCreateAsset<RangedProjectileItem>(k_FireArrowPath);
            ConfigureProjectileItem(
                standardArrow,
                "Standard Arrow",
                "A balanced arrow with reliable physical damage.",
                35f,
                0f);
            ConfigureProjectileItem(
                fireArrow,
                "Fire Arrow",
                "An arrow tipped to inflict both physical and fire damage.",
                20f,
                25f);

            RangedWeaponItem bow = LoadOrCreateAsset<RangedWeaponItem>(k_BowItemPath);
            SerializedObject serializedBow = new SerializedObject(bow);
            SetString(serializedBow, "m_itemName", "Longbow");
            SetString(
                serializedBow,
                "m_itemDescription",
                "A two-handed bow compatible with Arrow-class ammunition.");
            SetObject(
                serializedBow,
                "m_weaponModel",
                LoadRequiredAsset<GameObject>(k_BowPrefabPath));
            SetBool(serializedBow, "m_isUnarmed", false);
            SetEnum(serializedBow, "m_weaponModelType", WeaponModelType.Weapon);
            SetEnum(serializedBow, "m_weaponClass", WeaponClass.Bow);
            SetObject(
                serializedBow,
                "m_weaponAnimator",
                LoadRequiredAsset<AnimatorOverrideController>(k_BowOverridePath));
            SetFloat(serializedBow, "m_baseStaminaCost", 10f);
            SetFloat(serializedBow, "m_basePoiseDamage", 5f);
            SetObject(serializedBow, "m_rightHandAction", mainAction);
            SetObject(serializedBow, "m_rightHandHeavyAction", secondaryAction);
            SetObject(serializedBow, "m_twoHandRightAction", mainAction);
            SetObject(serializedBow, "m_twoHandRightHeavyAction", secondaryAction);
            SetObject(serializedBow, "m_leftHandAction", aimAction);
            SetEnum(serializedBow, "m_projectileClass", ProjectileClass.Arrow);
            serializedBow.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(bow);
            EditorUtility.SetDirty(mainAction);
            EditorUtility.SetDirty(secondaryAction);
            EditorUtility.SetDirty(aimAction);
        }

        private static void ConfigureProjectileItem(
            RangedProjectileItem projectile,
            string itemName,
            string description,
            float physicalDamage,
            float fireDamage)
        {
            SerializedObject serializedProjectile = new SerializedObject(projectile);
            SetString(serializedProjectile, "m_itemName", itemName);
            SetString(serializedProjectile, "m_itemDescription", description);
            SetEnum(
                serializedProjectile,
                "m_projectileClass",
                ProjectileClass.Arrow);
            SetFloat(serializedProjectile, "m_forwardVelocity", 30f);
            SetFloat(serializedProjectile, "m_upwardVelocity", 1.5f);
            SetFloat(serializedProjectile, "m_ammoMass", 0.1f);
            SetInt(serializedProjectile, "m_maxAmmoAmount", 30);
            SetInt(serializedProjectile, "m_currentAmmoAmount", 30);
            SetFloat(serializedProjectile, "m_physicalDamage", physicalDamage);
            SetFloat(serializedProjectile, "m_fireDamage", fireDamage);
            SetFloat(serializedProjectile, "m_poiseDamage", 5f);
            SetObject(
                serializedProjectile,
                "m_drawProjectileModel",
                LoadRequiredAsset<GameObject>(k_DrawArrowPrefabPath));
            GameObject releasePrefab = LoadRequiredAsset<GameObject>(
                k_ReleaseArrowPrefabPath);
            SetObject(
                serializedProjectile,
                "m_releaseProjectileModel",
                releasePrefab.GetComponent<RangedProjectileManager>());
            serializedProjectile.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(projectile);
        }

        private static void ConfigureWorldItemDatabase()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(
                k_WorldItemDatabasePath);
            try
            {
                WorldItemDatabase database = root.GetComponent<WorldItemDatabase>();
                SerializedObject serializedDatabase = new SerializedObject(database);
                RangedWeaponItem bow = LoadRequiredAsset<RangedWeaponItem>(k_BowItemPath);
                RangedProjectileItem standardArrow =
                    LoadRequiredAsset<RangedProjectileItem>(k_StandardArrowPath);
                RangedProjectileItem fireArrow =
                    LoadRequiredAsset<RangedProjectileItem>(k_FireArrowPath);
                AppendUnique(serializedDatabase.FindProperty("m_items"), bow);
                AppendUnique(
                    serializedDatabase.FindProperty("m_items"),
                    standardArrow);
                AppendUnique(serializedDatabase.FindProperty("m_items"), fireArrow);
                AppendUnique(
                    serializedDatabase.FindProperty("m_projectiles"),
                    standardArrow);
                AppendUnique(
                    serializedDatabase.FindProperty("m_projectiles"),
                    fireArrow);
                serializedDatabase.ApplyModifiedPropertiesWithoutUndo();
                AssignItemID(bow, 11);
                AssignItemID(standardArrow, 12);
                AssignItemID(fireArrow, 13);
                PrefabUtility.SaveAsPrefabAsset(root, k_WorldItemDatabasePath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConfigurePlayerPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(k_PlayerPrefabPath);
            try
            {
                PlayerInventoryManager inventory =
                    root.GetComponent<PlayerInventoryManager>();
                SerializedObject serializedInventory = new SerializedObject(inventory);
                RangedWeaponItem bow = LoadRequiredAsset<RangedWeaponItem>(k_BowItemPath);
                RangedProjectileItem standardArrow =
                    LoadRequiredAsset<RangedProjectileItem>(k_StandardArrowPath);
                RangedProjectileItem fireArrow =
                    LoadRequiredAsset<RangedProjectileItem>(k_FireArrowPath);
                SetObject(
                    serializedInventory,
                    "m_startingMainProjectile",
                    standardArrow);
                SetObject(
                    serializedInventory,
                    "m_startingSecondaryProjectile",
                    fireArrow);
                SerializedProperty rightSlots = serializedInventory.FindProperty(
                    "m_weaponsInRightHandSlots");
                AppendUnique(rightSlots, bow);
                serializedInventory.ApplyModifiedPropertiesWithoutUndo();

                PlayerManager player = root.GetComponent<PlayerManager>();
                SerializedObject serializedPlayer = new SerializedObject(player);
                SerializedProperty lockOnProperty = serializedPlayer.FindProperty(
                    "m_lockOnTransform");
                Transform lockOnTransform = root.transform.Find(
                    "Lock On Transform");
                if (lockOnTransform == null)
                {
                    lockOnTransform = new GameObject("Lock On Transform").transform;
                    lockOnTransform.SetParent(root.transform, false);
                    lockOnTransform.localPosition = Vector3.up * 1.2f;
                }

                lockOnProperty.objectReferenceValue = lockOnTransform;
                serializedPlayer.ApplyModifiedPropertiesWithoutUndo();
                Transform projectilePivot = root
                    .GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(candidate =>
                        candidate.name == "Projectile Pivot");
                if (projectilePivot == null)
                {
                    projectilePivot = new GameObject("Projectile Pivot").transform;
                }

                projectilePivot.SetParent(lockOnTransform, false);
                projectilePivot.localPosition = new Vector3(-0.25f, 0f, 0.15f);

                PrefabUtility.SaveAsPrefabAsset(root, k_PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConfigureCrosshair()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(k_UIManagerPrefabPath);
            try
            {
                PlayerUIHUDManager hud =
                    root.GetComponentInChildren<PlayerUIHUDManager>(true);
                Canvas canvas = root.GetComponentInChildren<Canvas>(true);
                Transform crosshairTransform = canvas?.transform.Find("Crosshair");
                GameObject crosshair = crosshairTransform != null
                    ? crosshairTransform.gameObject
                    : new GameObject("Crosshair", typeof(RectTransform));
                crosshair.transform.SetParent(canvas.transform, false);
                RectTransform rect = (RectTransform)crosshair.transform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(36f, 36f);
                if (crosshair.transform.childCount == 0)
                {
                    CreateCrosshairLine(rect, "Top", new Vector2(0f, 11f), new Vector2(2f, 10f));
                    CreateCrosshairLine(rect, "Bottom", new Vector2(0f, -11f), new Vector2(2f, 10f));
                    CreateCrosshairLine(rect, "Left", new Vector2(-11f, 0f), new Vector2(10f, 2f));
                    CreateCrosshairLine(rect, "Right", new Vector2(11f, 0f), new Vector2(10f, 2f));
                    CreateCrosshairLine(rect, "Center", Vector2.zero, new Vector2(3f, 3f));
                }

                SerializedObject serializedHUD = new SerializedObject(hud);
                SetObject(serializedHUD, "m_crosshair", crosshair);
                serializedHUD.ApplyModifiedPropertiesWithoutUndo();
                crosshair.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root, k_UIManagerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConfigureMainMenuSystems()
        {
            Scene scene = EditorSceneManager.OpenScene(
                k_MainMenuScenePath,
                OpenSceneMode.Single);
            PlayerCamera camera = UnityEngine.Object.FindFirstObjectByType<PlayerCamera>(
                FindObjectsInactive.Include);
            if (camera != null)
            {
                SerializedObject serializedCamera = new SerializedObject(camera);
                SetFloat(serializedCamera, "m_standardFieldOfView", 60f);
                SetFloat(serializedCamera, "m_aimFieldOfView", 40f);
                SetFloat(serializedCamera, "m_standardNearClipPlane", 0.3f);
                SetFloat(serializedCamera, "m_aimNearClipPlane", 1.3f);
                serializedCamera.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(camera);
            }

            WorldActionManager actionManager =
                UnityEngine.Object.FindFirstObjectByType<WorldActionManager>(
                    FindObjectsInactive.Include);
            if (actionManager != null)
            {
                SerializedObject serializedActions = new SerializedObject(actionManager);
                SerializedProperty actions = serializedActions.FindProperty(
                    "m_weaponActions");
                AppendUnique(
                    actions,
                    LoadRequiredAsset<FireProjectileWeaponItemAction>(
                        k_MainFireActionPath));
                AppendUnique(
                    actions,
                    LoadRequiredAsset<FireProjectileWeaponItemAction>(
                        k_SecondaryFireActionPath));
                AppendUnique(
                    actions,
                    LoadRequiredAsset<AimAction>(k_AimActionPath));
                serializedActions.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(actionManager);
            }

            EditorSceneManager.SaveScene(scene);
        }

        private static void ValidateItems()
        {
            RangedWeaponItem bow = LoadRequiredAsset<RangedWeaponItem>(k_BowItemPath);
            RangedProjectileItem standardArrow =
                LoadRequiredAsset<RangedProjectileItem>(k_StandardArrowPath);
            RangedProjectileItem fireArrow =
                LoadRequiredAsset<RangedProjectileItem>(k_FireArrowPath);
            if (bow.ItemID != 11 ||
                standardArrow.ItemID != 12 ||
                fireArrow.ItemID != 13 ||
                bow.WeaponClass != WeaponClass.Bow ||
                !bow.CanFireProjectile(standardArrow) ||
                standardArrow.MaxAmmoAmount != 30 ||
                fireArrow.FireDamage <= 0f)
            {
                throw new InvalidOperationException(
                    "Archery item IDs, compatibility, capacity, or damage are invalid.");
            }
        }

        private static void ValidateProjectilePrefab()
        {
            GameObject prefab = LoadRequiredAsset<GameObject>(k_ReleaseArrowPrefabPath);
            Rigidbody rigidbody = prefab.GetComponent<Rigidbody>();
            CapsuleCollider physicalCollider = prefab.GetComponent<CapsuleCollider>();
            RangeProjectileDamageCollider damageCollider =
                prefab.GetComponentInChildren<RangeProjectileDamageCollider>(true);
            int projectileLayer = LayerMask.NameToLayer("Projectile");
            if (prefab.GetComponent<RangedProjectileManager>() == null ||
                rigidbody == null ||
                rigidbody.collisionDetectionMode !=
                    CollisionDetectionMode.ContinuousDynamic ||
                physicalCollider == null ||
                damageCollider == null ||
                prefab.layer != projectileLayer ||
                damageCollider.gameObject.layer !=
                    LayerMask.NameToLayer("Damage Collider") ||
                !Physics.GetIgnoreLayerCollision(
                    projectileLayer,
                    LayerMask.NameToLayer("Player")) ||
                !Physics.GetIgnoreLayerCollision(
                    projectileLayer,
                    LayerMask.NameToLayer("Damageable Character")))
            {
                throw new InvalidOperationException(
                    "Released Arrow needs continuous physics and an isolated damage collider.");
            }
        }

        private static void ValidateAnimator()
        {
            AnimatorController controller = LoadRequiredAsset<AnimatorController>(
                k_HumanoidControllerPath);
            AnimatorStateMachine stateMachine = controller.layers
                .First(layer => layer.name == k_ActionLayerName)
                .stateMachine;
            AnimatorState fire = GetRequiredState(stateMachine, "Bow_Fire");
            AnimatorState aim = GetRequiredState(stateMachine, "Bow_Aim");
            string[] events = AnimationUtility.GetAnimationEvents(
                    LoadRequiredAsset<AnimationClip>(k_BowFireClipPath))
                .Select(animationEvent => animationEvent.functionName)
                .ToArray();
            if (fire.motion == null ||
                aim.behaviours.All(behaviour =>
                    behaviour is not ToggleNotchedArrowMovement) ||
                !events.Contains("ReleaseArrow") ||
                controller.parameters.Count(parameter =>
                    parameter.name == k_HasArrowNotched ||
                    parameter.name == k_IsHoldingArrow ||
                    parameter.name == k_IsAiming) != 3)
            {
                throw new InvalidOperationException(
                    "The character Animator is missing the archery flow or release event.");
            }
        }

        private static void ValidatePlayerAndUI()
        {
            GameObject playerRoot = PrefabUtility.LoadPrefabContents(k_PlayerPrefabPath);
            try
            {
                PlayerInventoryManager inventory =
                    playerRoot.GetComponent<PlayerInventoryManager>();
                SerializedObject serializedInventory = new SerializedObject(inventory);
                Transform pivot = playerRoot.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(candidate => candidate.name == "Projectile Pivot");
                if (serializedInventory.FindProperty("m_startingMainProjectile")
                        .objectReferenceValue == null ||
                    serializedInventory.FindProperty("m_startingSecondaryProjectile")
                        .objectReferenceValue == null ||
                    pivot == null)
                {
                    throw new InvalidOperationException(
                        "Player needs two ammunition slots and a Projectile Pivot.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(playerRoot);
            }

            GameObject uiRoot = PrefabUtility.LoadPrefabContents(k_UIManagerPrefabPath);
            try
            {
                PlayerUIHUDManager hud =
                    uiRoot.GetComponentInChildren<PlayerUIHUDManager>(true);
                GameObject crosshair = new SerializedObject(hud)
                    .FindProperty("m_crosshair")
                    .objectReferenceValue as GameObject;
                RectTransform rect = crosshair?.transform as RectTransform;
                if (rect == null ||
                    rect.anchorMin != new Vector2(0.5f, 0.5f) ||
                    rect.anchorMax != new Vector2(0.5f, 0.5f) ||
                    crosshair.activeSelf)
                {
                    throw new InvalidOperationException(
                        "The aim crosshair must begin hidden at exact screen center.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(uiRoot);
            }
        }

        private static AnimatorState ConfigureState(
            AnimatorStateMachine stateMachine,
            string stateName,
            string clipPath,
            Vector3 position)
        {
            AnimatorState state = GetOrAddState(stateMachine, stateName, position);
            state.motion = LoadRequiredAsset<AnimationClip>(clipPath);
            EditorUtility.SetDirty(state);
            return state;
        }

        private static AnimatorState GetOrAddState(
            AnimatorStateMachine stateMachine,
            string stateName,
            Vector3 position)
        {
            return stateMachine.states
                .Select(childState => childState.state)
                .FirstOrDefault(state => state.name == stateName) ??
                stateMachine.AddState(stateName, position);
        }

        private static AnimatorState GetRequiredState(
            AnimatorStateMachine stateMachine,
            string stateName)
        {
            return stateMachine.states
                .Select(childState => childState.state)
                .FirstOrDefault(state => state.name == stateName) ??
                throw new InvalidOperationException(
                    $"Animator state {stateName} is missing.");
        }

        private static void ClearTransitions(AnimatorState state)
        {
            foreach (AnimatorStateTransition transition in state.transitions.ToArray())
            {
                state.RemoveTransition(transition);
            }
        }

        private static void AddBoolTransition(
            AnimatorState source,
            AnimatorState destination,
            string parameter,
            bool expectedValue,
            bool hasExitTime,
            float exitTime,
            float duration)
        {
            AnimatorStateTransition transition = source.AddTransition(destination);
            transition.hasExitTime = hasExitTime;
            transition.exitTime = exitTime;
            transition.hasFixedDuration = true;
            transition.duration = duration;
            transition.AddCondition(
                expectedValue ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot,
                0f,
                parameter);
            EditorUtility.SetDirty(transition);
        }

        private static void AddExitTransition(
            AnimatorState source,
            AnimatorState destination,
            float exitTime,
            float duration)
        {
            AnimatorStateTransition transition = source.AddTransition(destination);
            transition.hasExitTime = true;
            transition.exitTime = exitTime;
            transition.hasFixedDuration = true;
            transition.duration = duration;
            EditorUtility.SetDirty(transition);
        }

        private static void EnsureBoolParameter(
            AnimatorController controller,
            string parameterName)
        {
            AnimatorControllerParameter parameter = controller.parameters
                .FirstOrDefault(candidate => candidate.name == parameterName);
            if (parameter == null)
            {
                controller.AddParameter(
                    parameterName,
                    AnimatorControllerParameterType.Bool);
            }
            else if (parameter.type != AnimatorControllerParameterType.Bool)
            {
                throw new InvalidOperationException(
                    $"Animator parameter {parameterName} must be Bool.");
            }
        }

        private static void SetLoopTime(AnimationClip clip, bool loopTime)
        {
            AnimationClipSettings settings =
                AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loopTime;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
        }

        private static GameObject CreatePrimitiveChild(
            Transform parent,
            string objectName,
            PrimitiveType primitiveType,
            Vector3 localPosition,
            Vector3 localScale,
            Vector3 localEulerAngles)
        {
            GameObject child = GameObject.CreatePrimitive(primitiveType);
            child.name = objectName;
            child.transform.SetParent(parent, false);
            child.transform.localPosition = localPosition;
            child.transform.localRotation = Quaternion.Euler(localEulerAngles);
            child.transform.localScale = localScale;
            Collider collider = child.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }

            return child;
        }

        private static void CreateCrosshairLine(
            RectTransform parent,
            string objectName,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            GameObject line = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(Image));
            RectTransform rect = (RectTransform)line.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            Image image = line.GetComponent<Image>();
            image.color = Color.white;
            image.raycastTarget = false;
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                transform.gameObject.layer = layer;
            }
        }

        private static void AppendUnique(
            SerializedProperty array,
            UnityEngine.Object value)
        {
            for (int index = 0; index < array.arraySize; index++)
            {
                if (array.GetArrayElementAtIndex(index).objectReferenceValue == value)
                {
                    return;
                }
            }

            int newIndex = array.arraySize;
            array.InsertArrayElementAtIndex(newIndex);
            array.GetArrayElementAtIndex(newIndex).objectReferenceValue = value;
        }

        private static void AssignItemID(Item item, int itemID)
        {
            SerializedObject serializedItem = new SerializedObject(item);
            SetInt(serializedItem, "m_itemID", itemID);
            serializedItem.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
        }

        private static void EnsureFolder(string parentFolder, string childFolder)
        {
            string path = $"{parentFolder}/{childFolder}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parentFolder, childFolder);
            }
        }

        private static T LoadOrCreateAsset<T>(string assetPath)
            where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, assetPath);
            return asset;
        }

        private static AnimationClip LoadOrCreateAnimationClip(string assetPath)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                assetPath);
            if (clip != null)
            {
                return clip;
            }

            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, assetPath);
            return clip;
        }

        private static T LoadRequiredAsset<T>(string assetPath)
            where T : UnityEngine.Object
        {
            return AssetDatabase.LoadAssetAtPath<T>(assetPath) ??
                throw new InvalidOperationException(
                    $"Required asset is missing: {assetPath}");
        }

        private static void SetObject(
            SerializedObject serializedObject,
            string propertyName,
            UnityEngine.Object value)
        {
            GetProperty(serializedObject, propertyName).objectReferenceValue = value;
        }

        private static void SetString(
            SerializedObject serializedObject,
            string propertyName,
            string value)
        {
            GetProperty(serializedObject, propertyName).stringValue = value;
        }

        private static void SetFloat(
            SerializedObject serializedObject,
            string propertyName,
            float value)
        {
            GetProperty(serializedObject, propertyName).floatValue = value;
        }

        private static void SetInt(
            SerializedObject serializedObject,
            string propertyName,
            int value)
        {
            GetProperty(serializedObject, propertyName).intValue = value;
        }

        private static void SetBool(
            SerializedObject serializedObject,
            string propertyName,
            bool value)
        {
            GetProperty(serializedObject, propertyName).boolValue = value;
        }

        private static void SetEnum<T>(
            SerializedObject serializedObject,
            string propertyName,
            T value) where T : Enum
        {
            GetProperty(serializedObject, propertyName).intValue =
                Convert.ToInt32(value);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static SerializedProperty GetProperty(
            SerializedObject serializedObject,
            string propertyName)
        {
            return serializedObject.FindProperty(propertyName) ??
                throw new InvalidOperationException(
                    $"{serializedObject.targetObject.GetType().Name} is missing " +
                    $"serialized property {propertyName}.");
        }
    }
}
