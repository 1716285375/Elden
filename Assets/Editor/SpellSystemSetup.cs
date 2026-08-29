using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ZZ.Editor
{
    /// <summary>Configures and validates the EP77-79 catalyst and Fireball loop.</summary>
    public static class SpellSystemSetup
    {
        private const string k_ActionPath =
            "Assets/_Game/Data/Actions/Spells/Cast Incantation.asset";
        private const string k_FireballPath =
            "Assets/_Game/Data/Items/Spells/Fireball.asset";
        private const string k_CatalystPath =
            "Assets/_Game/Data/Items/Weapons/Catalysts/Incantation Catalyst.asset";
        private const string k_CatalystPrefabPath =
            "Assets/_Game/Prefabs/Abilities/Incantation Catalyst.prefab";
        private const string k_FireballPrefabPath =
            "Assets/_Game/Prefabs/Abilities/Fireball.prefab";
        private const string k_WarmUpPrefabPath =
            "Assets/_Game/Prefabs/Abilities/Fireball Warm Up.prefab";
        private const string k_ReleasePrefabPath =
            "Assets/_Game/Prefabs/Abilities/Fireball Release.prefab";
        private const string k_FullChargePrefabPath =
            "Assets/_Game/Prefabs/Abilities/Fireball Full Charge.prefab";
        private const string k_ImpactPrefabPath =
            "Assets/_Game/Prefabs/Abilities/Fireball Impact.prefab";
        private const string k_FullImpactPrefabPath =
            "Assets/_Game/Prefabs/Abilities/Fireball Full Impact.prefab";
        private const string k_FireMaterialPath =
            "Assets/_Game/Art/VFX/Abilities/Spells/Fireball.mat";
        private const string k_FullChargeMaterialPath =
            "Assets/_Game/Art/VFX/Abilities/Spells/Fireball Full Charge.mat";
        private const string k_PlayerPrefabPath =
            "Assets/_Game/Prefabs/Characters/Player/Player.prefab";
        private const string k_DatabasePrefabPath =
            "Assets/_Game/Prefabs/World/Managers/World Item Database.prefab";
        private const string k_InputAssetPath = "Assets/_Game/Settings/Input/PlayerControls.inputactions";
        private const string k_AnimatorPath =
            "Assets/Art/Animations/Animator Controllers/Humanoid/" +
            "Humanoid Animator Controller.controller";
        private const string k_AnimatorOverridePath =
            "Assets/Art/Animations/Controllers/Overrides/Charm.overrideController";
        private const string k_StaffModelPath =
            "Assets/Art/Models/Equipment/Weapons/Staff/SM_Wep_Staff_02.obj";
        private const string k_SpellIconPath =
            "Assets/Art/Textures/UI/Abilities/Ability_Fire_Orb_Icon_01.png";
        private const string k_CastSoundPath =
            "Assets/Art/Audio/SFX/Abilities/SFX_Cast_FireBall_Base_01.wav";
        private const string k_ImpactSoundPath =
            "Assets/Art/Audio/SFX/Abilities/SFX_Explosion_Fireball_Base_01.wav";
        private const string k_ActionLayerName = "Action Override";
        private const string k_EmptyStateName = "Empty";
        private const string k_ProjectileLayerName = "Projectile";
        private const int k_ProjectileLayer = 12;

        private static readonly SpellAnimationDefinition[] s_spellAnimations =
        {
            new SpellAnimationDefinition(
                "Cast_Spell_Right_Charge",
                "Assets/Art/Animations/Characters/Humanoid/Combat/General/" +
                "sphand_main_projectile_02_charge.anim"),
            new SpellAnimationDefinition(
                "Cast_Spell_Right_Hold",
                "Assets/Art/Animations/Characters/Humanoid/Actions/" +
                "sphand_main_projectile_02_hold.anim"),
            new SpellAnimationDefinition(
                "Cast_Spell_Right_Release",
                "Assets/Art/Animations/Characters/Humanoid/Combat/General/" +
                "sphand_main_projectile_02_release.anim"),
            new SpellAnimationDefinition(
                "Cast_Spell_Right_Release_Full",
                "Assets/Art/Animations/Characters/Humanoid/Combat/General/" +
                "sphand_main_projectile_02_release_full.anim"),
            new SpellAnimationDefinition(
                "Cast_Spell_Left_Charge",
                "Assets/Art/Animations/Characters/Humanoid/Combat/General/" +
                "sphand_off_projectile_02_charge.anim"),
            new SpellAnimationDefinition(
                "Cast_Spell_Left_Hold",
                "Assets/Art/Animations/Characters/Humanoid/Actions/" +
                "sphand_off_projectile_02_hold.anim"),
            new SpellAnimationDefinition(
                "Cast_Spell_Left_Release",
                "Assets/Art/Animations/Characters/Humanoid/Combat/General/" +
                "sphand_off_projectile_02_release.anim"),
            new SpellAnimationDefinition(
                "Cast_Spell_Left_Release_Full",
                "Assets/Art/Animations/Characters/Humanoid/Combat/General/" +
                "sphand_off_projectile_02_release_full.anim")
        };

        [MenuItem("Tools/Elden/Configure Spell System")]
        public static void ConfigureSpellSystem()
        {
            ConfigureProjectileLayer();
            Material fireMaterial = CreateOrUpdateMaterial(
                k_FireMaterialPath,
                new Color(1f, 0.18f, 0.015f, 1f));
            Material fullChargeMaterial = CreateOrUpdateMaterial(
                k_FullChargeMaterialPath,
                new Color(1f, 0.78f, 0.08f, 1f));
            GameObject warmUp = CreateParticleEffectPrefab(
                k_WarmUpPrefabPath,
                fireMaterial,
                true,
                0.18f,
                34f);
            GameObject release = CreateParticleEffectPrefab(
                k_ReleasePrefabPath,
                fireMaterial,
                false,
                0.32f,
                70f,
                LoadRequiredAsset<AudioClip>(k_CastSoundPath));
            GameObject fullCharge = CreateParticleEffectPrefab(
                k_FullChargePrefabPath,
                fullChargeMaterial,
                true,
                0.28f,
                48f);
            GameObject impact = CreateParticleEffectPrefab(
                k_ImpactPrefabPath,
                fireMaterial,
                false,
                0.48f,
                110f);
            GameObject fullImpact = CreateParticleEffectPrefab(
                k_FullImpactPrefabPath,
                fullChargeMaterial,
                false,
                0.72f,
                150f);
            FireballManager fireballPrefab = CreateFireballPrefab(
                fireMaterial,
                impact,
                fullImpact);
            CastIncantationAction action = CreateOrLoadAsset<CastIncantationAction>(
                k_ActionPath);
            GameObject catalystPrefab = CreateCatalystPrefab();
            CasterWeaponItem catalyst = ConfigureCatalyst(
                action,
                catalystPrefab);
            FireballSpell fireball = ConfigureFireball(
                fireballPrefab,
                warmUp,
                release,
                fullCharge);
            ConfigureDatabase(catalyst, fireball);
            ConfigurePlayer(catalyst, fireball);
            ConfigureAnimator();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateSpellSystem();
            Debug.Log(
                "[SpellSystemSetup] Configured EP77-79 spell data, catalyst input, " +
                "replicated charge presentation, and owner-authoritative Fireball damage.");
        }

        [MenuItem("Tools/Elden/Validate Spell System")]
        public static void ValidateSpellSystem()
        {
            ValidateRuntimeContracts();
            ValidateSpellAssets();
            ValidateProjectilePrefab();
            ValidatePlayerAndDatabase();
            ValidateAnimator();
            ValidateInput();
            Debug.Log(
                "[SpellSystemValidation] EP77-79 data, Animator, input, physics, " +
                "late-join state, and projectile authority are valid.");
        }

        private static void ConfigureProjectileLayer()
        {
            UnityEngine.Object tagManager = AssetDatabase.LoadAllAssetsAtPath(
                    "ProjectSettings/TagManager.asset")
                .FirstOrDefault() ??
                throw new InvalidOperationException("Could not load TagManager.asset.");
            SerializedObject serializedTags = new SerializedObject(tagManager);
            SerializedProperty layers = GetRequiredProperty(serializedTags, "layers");
            if (layers.arraySize <= k_ProjectileLayer)
            {
                throw new InvalidOperationException(
                    "TagManager does not expose the required Projectile layer slot.");
            }

            layers.GetArrayElementAtIndex(k_ProjectileLayer).stringValue =
                k_ProjectileLayerName;
            serializedTags.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(tagManager);
        }

        private static Material CreateOrUpdateMaterial(string path, Color color)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                    Shader.Find("Particles/Standard Unlit") ??
                    throw new InvalidOperationException(
                        "Could not resolve a supported particle shader.");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            material.renderQueue = 3000;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject CreateParticleEffectPrefab(
            string path,
            Material material,
            bool loops,
            float size,
            float emissionRate,
            AudioClip audioClip = null)
        {
            GameObject root = new GameObject(
                System.IO.Path.GetFileNameWithoutExtension(path));
            try
            {
                ParticleSystem particles = root.AddComponent<ParticleSystem>();
                ParticleSystem.MainModule main = particles.main;
                main.duration = loops ? 1f : 0.65f;
                main.loop = loops;
                main.playOnAwake = true;
                main.startLifetime = loops
                    ? new ParticleSystem.MinMaxCurve(0.28f, 0.7f)
                    : new ParticleSystem.MinMaxCurve(0.2f, 0.55f);
                main.startSpeed = loops
                    ? new ParticleSystem.MinMaxCurve(0.08f, 0.35f)
                    : new ParticleSystem.MinMaxCurve(1.2f, 3.8f);
                main.startSize = new ParticleSystem.MinMaxCurve(size * 0.45f, size);
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = 180;
                main.stopAction = loops
                    ? ParticleSystemStopAction.None
                    : ParticleSystemStopAction.Destroy;
                ParticleSystem.EmissionModule emission = particles.emission;
                emission.rateOverTime = loops ? emissionRate : 0f;
                if (!loops)
                {
                    emission.SetBursts(
                        new[]
                        {
                            new ParticleSystem.Burst(0f, (short)emissionRate)
                        });
                }

                ParticleSystem.ShapeModule shape = particles.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = size * 0.65f;
                ParticleSystemRenderer renderer =
                    root.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode =
                    UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                if (audioClip != null)
                {
                    AudioSource audioSource = root.AddComponent<AudioSource>();
                    audioSource.clip = audioClip;
                    audioSource.playOnAwake = true;
                    audioSource.spatialBlend = 1f;
                    audioSource.minDistance = 1f;
                    audioSource.maxDistance = 24f;
                }

                SavePrefab(root, path);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            return LoadRequiredAsset<GameObject>(path);
        }

        private static FireballManager CreateFireballPrefab(
            Material material,
            GameObject impact,
            GameObject fullImpact)
        {
            GameObject root = new GameObject("Fireball");
            try
            {
                root.layer = k_ProjectileLayer;
                Rigidbody rigidbody = root.AddComponent<Rigidbody>();
                rigidbody.useGravity = false;
                rigidbody.isKinematic = false;
                rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                rigidbody.collisionDetectionMode =
                    CollisionDetectionMode.ContinuousDynamic;
                SphereCollider travelCollider = root.AddComponent<SphereCollider>();
                travelCollider.isTrigger = false;
                travelCollider.radius = 0.22f;
                ParticleSystem particles = root.AddComponent<ParticleSystem>();
                ParticleSystem.MainModule main = particles.main;
                main.duration = 1f;
                main.loop = true;
                main.playOnAwake = true;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.34f);
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = 90;
                ParticleSystem.EmissionModule emission = particles.emission;
                emission.rateOverTime = 48f;
                ParticleSystem.ShapeModule shape = particles.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.16f;
                ParticleSystemRenderer renderer =
                    root.GetComponent<ParticleSystemRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode =
                    UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                FireballManager manager = root.AddComponent<FireballManager>();

                GameObject damageObject = new GameObject("Damage Collider");
                damageObject.layer = LayerMask.NameToLayer("Damage Collider");
                damageObject.transform.SetParent(root.transform, false);
                Rigidbody damageRigidbody = damageObject.AddComponent<Rigidbody>();
                damageRigidbody.useGravity = false;
                damageRigidbody.isKinematic = true;
                SphereCollider damageCollider =
                    damageObject.AddComponent<SphereCollider>();
                damageCollider.isTrigger = true;
                damageCollider.radius = 0.34f;
                damageObject.AddComponent<SpellProjectileDamageCollider>();

                SerializedObject serializedManager = new SerializedObject(manager);
                SetObjectReference(serializedManager, "m_impactEffect", impact);
                SetObjectReference(
                    serializedManager,
                    "m_fullChargeImpactEffect",
                    fullImpact);
                SetObjectReference(
                    serializedManager,
                    "m_impactSound",
                    LoadRequiredAsset<AudioClip>(k_ImpactSoundPath));
                GetRequiredProperty(
                    serializedManager,
                    "m_forwardVelocity").floatValue = 18f;
                GetRequiredProperty(
                    serializedManager,
                    "m_homingTurnSpeed").floatValue = 180f;
                serializedManager.ApplyModifiedPropertiesWithoutUndo();
                SavePrefab(root, k_FireballPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            return LoadRequiredAsset<GameObject>(k_FireballPrefabPath)
                .GetComponent<FireballManager>();
        }

        private static GameObject CreateCatalystPrefab()
        {
            GameObject root = new GameObject("Incantation Catalyst");
            try
            {
                root.AddComponent<WeaponManager>();
                GameObject staffModel = LoadRequiredAsset<GameObject>(k_StaffModelPath);
                GameObject visual = PrefabUtility.InstantiatePrefab(
                        staffModel,
                        root.transform) as GameObject ??
                    throw new InvalidOperationException(
                        "Could not instantiate the staff model.");
                visual.name = "Staff Visual";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one;
                GameObject anchorObject = new GameObject("Spell Instantiation Location");
                anchorObject.transform.SetParent(root.transform, false);
                anchorObject.transform.localPosition = new Vector3(0f, 1.25f, 0.12f);
                SpellInstantiationLocation location =
                    root.AddComponent<SpellInstantiationLocation>();
                SerializedObject serializedLocation = new SerializedObject(location);
                SetObjectReference(
                    serializedLocation,
                    "m_instantiationTransform",
                    anchorObject.transform);
                serializedLocation.ApplyModifiedPropertiesWithoutUndo();
                SavePrefab(root, k_CatalystPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            return LoadRequiredAsset<GameObject>(k_CatalystPrefabPath);
        }

        private static CasterWeaponItem ConfigureCatalyst(
            CastIncantationAction action,
            GameObject catalystPrefab)
        {
            CasterWeaponItem catalyst = CreateOrLoadAsset<CasterWeaponItem>(
                k_CatalystPath);
            SerializedObject serializedCatalyst = new SerializedObject(catalyst);
            GetRequiredProperty(serializedCatalyst, "m_itemName").stringValue =
                "Incantation Catalyst";
            GetRequiredProperty(serializedCatalyst, "m_itemDescription").stringValue =
                "A simple sacred catalyst capable of charging and casting incantations.";
            SetObjectReference(
                serializedCatalyst,
                "m_itemIcon",
                LoadRequiredSprite(k_SpellIconPath));
            SetObjectReference(serializedCatalyst, "m_weaponModel", catalystPrefab);
            SetObjectReference(
                serializedCatalyst,
                "m_weaponAnimator",
                LoadRequiredAsset<AnimatorOverrideController>(k_AnimatorOverridePath));
            GetRequiredProperty(serializedCatalyst, "m_weaponModelType").enumValueIndex =
                (int)WeaponModelType.Weapon;
            GetRequiredProperty(serializedCatalyst, "m_weaponClass").enumValueIndex =
                (int)WeaponClass.Spear;
            GetRequiredProperty(serializedCatalyst, "m_weaponPivotScale").vector3Value =
                Vector3.one;
            SetObjectReference(serializedCatalyst, "m_rightHandAction", action);
            SetObjectReference(serializedCatalyst, "m_leftHandAction", action);
            GetRequiredProperty(serializedCatalyst, "m_spellClass").enumValueIndex =
                (int)SpellClass.Incantation;
            serializedCatalyst.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalyst);
            return catalyst;
        }

        private static FireballSpell ConfigureFireball(
            FireballManager fireballPrefab,
            GameObject warmUp,
            GameObject release,
            GameObject fullCharge)
        {
            FireballSpell fireball = CreateOrLoadAsset<FireballSpell>(k_FireballPath);
            SerializedObject serializedFireball = new SerializedObject(fireball);
            GetRequiredProperty(serializedFireball, "m_itemName").stringValue = "Fireball";
            GetRequiredProperty(serializedFireball, "m_itemDescription").stringValue =
                "Conjures a homing ball of flame. Hold the catalyst input for full charge.";
            SetObjectReference(
                serializedFireball,
                "m_itemIcon",
                LoadRequiredSprite(k_SpellIconPath));
            GetRequiredProperty(serializedFireball, "m_spellClass").enumValueIndex =
                (int)SpellClass.Incantation;
            GetRequiredProperty(serializedFireball, "m_spellSlotsUsed").intValue = 1;
            GetRequiredProperty(serializedFireball, "m_fullChargeModifier").floatValue =
                1.4f;
            SetObjectReference(serializedFireball, "m_spellWarmUpEffect", warmUp);
            SetObjectReference(serializedFireball, "m_spellReleaseEffect", release);
            SetObjectReference(
                serializedFireball,
                "m_spellFullyChargedEffect",
                fullCharge);
            SetObjectReference(serializedFireball, "m_fireballPrefab", fireballPrefab);
            GetRequiredProperty(serializedFireball, "m_fireDamage").floatValue = 150f;
            serializedFireball.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(fireball);
            return fireball;
        }

        private static void ConfigureDatabase(
            CasterWeaponItem catalyst,
            FireballSpell fireball)
        {
            EditPrefab(
                k_DatabasePrefabPath,
                root =>
                {
                    WorldItemDatabase database =
                        GetRequiredComponent<WorldItemDatabase>(root);
                    SerializedObject serializedDatabase =
                        new SerializedObject(database);
                    AppendUniqueObject(
                        GetRequiredProperty(serializedDatabase, "m_items"),
                        catalyst);
                    AppendUniqueObject(
                        GetRequiredProperty(serializedDatabase, "m_items"),
                        fireball);
                    AppendUniqueObject(
                        GetRequiredProperty(serializedDatabase, "m_spells"),
                        fireball);
                    serializedDatabase.ApplyModifiedPropertiesWithoutUndo();
                    typeof(WorldItemDatabase).GetMethod(
                            "AssignItemIDs",
                            BindingFlags.Instance | BindingFlags.NonPublic)
                        ?.Invoke(database, null);
                    EditorUtility.SetDirty(database);
                });
        }

        private static void ConfigurePlayer(
            CasterWeaponItem catalyst,
            FireballSpell fireball)
        {
            EditPrefab(
                k_PlayerPrefabPath,
                root =>
                {
                    PlayerInventoryManager inventory =
                        GetRequiredComponent<PlayerInventoryManager>(root);
                    SerializedObject serializedInventory =
                        new SerializedObject(inventory);
                    SetQuickSlot(
                        serializedInventory,
                        "m_weaponsInRightHandSlots",
                        2,
                        catalyst);
                    SetQuickSlot(
                        serializedInventory,
                        "m_weaponsInLeftHandSlots",
                        2,
                        catalyst);
                    SetObjectReference(
                        serializedInventory,
                        "m_startingSpell",
                        fireball);
                    serializedInventory.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(inventory);
                });
        }

        private static void ConfigureAnimator()
        {
            AnimatorController controller =
                LoadRequiredAsset<AnimatorController>(k_AnimatorPath);
            EnsureBoolParameter(controller, "isChargingRightSpell");
            EnsureBoolParameter(controller, "isChargingLeftSpell");
            EnsureBoolParameter(controller, "isSpellFullyCharged");
            AnimatorStateMachine stateMachine = controller.layers
                .Single(layer => layer.name == k_ActionLayerName)
                .stateMachine;
            AnimatorState emptyState = GetOrCreateState(
                stateMachine,
                k_EmptyStateName);
            Dictionary<string, AnimatorState> states = new();
            foreach (SpellAnimationDefinition definition in s_spellAnimations)
            {
                AnimatorState state = GetOrCreateState(
                    stateMachine,
                    definition.StateName);
                state.motion = LoadRequiredAsset<AnimationClip>(
                    definition.AnimationPath);
                states.Add(definition.StateName, state);
                EditorUtility.SetDirty(state);
            }

            ConfigureTransition(
                states["Cast_Spell_Right_Charge"],
                states["Cast_Spell_Right_Hold"],
                true,
                0.9f,
                null);
            ConfigureTransition(
                states["Cast_Spell_Left_Charge"],
                states["Cast_Spell_Left_Hold"],
                true,
                0.9f,
                null);
            ConfigureTransition(
                states["Cast_Spell_Right_Hold"],
                emptyState,
                false,
                0f,
                "isChargingRightSpell");
            ConfigureTransition(
                states["Cast_Spell_Left_Hold"],
                emptyState,
                false,
                0f,
                "isChargingLeftSpell");
            foreach (string releaseStateName in new[]
                     {
                         "Cast_Spell_Right_Release",
                         "Cast_Spell_Right_Release_Full",
                         "Cast_Spell_Left_Release",
                         "Cast_Spell_Left_Release_Full"
                     })
            {
                ConfigureTransition(
                    states[releaseStateName],
                    emptyState,
                    true,
                    0.95f,
                    null);
            }

            ConfigureSpellClipEvents();
            EditorUtility.SetDirty(controller);
        }

        private static void ConfigureSpellClipEvents()
        {
            foreach (SpellAnimationDefinition definition in s_spellAnimations)
            {
                AnimationClip clip = LoadRequiredAsset<AnimationClip>(
                    definition.AnimationPath);
                if (definition.StateName.EndsWith("_Hold", StringComparison.Ordinal))
                {
                    AnimationClipSettings settings =
                        AnimationUtility.GetAnimationClipSettings(clip);
                    settings.loopTime = true;
                    AnimationUtility.SetAnimationClipSettings(clip, settings);
                }

                if (!definition.StateName.Contains("_Release", StringComparison.Ordinal))
                {
                    continue;
                }

                List<AnimationEvent> events =
                    AnimationUtility.GetAnimationEvents(clip).ToList();
                if (events.All(animationEvent =>
                        animationEvent.functionName != "CompleteSpellCast"))
                {
                    events.Add(new AnimationEvent
                    {
                        functionName = "CompleteSpellCast",
                        time = Mathf.Max(0f, clip.length - 0.06f)
                    });
                    AnimationUtility.SetAnimationEvents(clip, events.ToArray());
                }
            }
        }

        private static void ValidateRuntimeContracts()
        {
            string[] requiredSpellMethods =
            {
                nameof(SpellItem.AttemptToCastSpell),
                nameof(SpellItem.SuccessfullyCastSpell),
                nameof(SpellItem.SuccessfullyCastSpellFullCharge),
                nameof(SpellItem.SuccessfullyChargeSpell),
                nameof(SpellItem.InstantiateSpellWarmUpEffects),
                nameof(SpellItem.CanICastThisSpell)
            };
            bool hasMethods = requiredSpellMethods.All(methodName =>
                typeof(SpellItem).GetMethod(methodName) != null);
            bool hasNetworkState = new[]
            {
                "m_currentSpellID",
                "m_isChargingRightSpell",
                "m_isChargingLeftSpell"
            }.All(fieldName => typeof(PlayerNetworkManager).GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic) != null);
            if (!hasMethods ||
                !hasNetworkState ||
                !typeof(WeaponItemAction).IsAssignableFrom(
                    typeof(CastIncantationAction)) ||
                !typeof(WeaponItem).IsAssignableFrom(typeof(CasterWeaponItem)))
            {
                throw new InvalidOperationException(
                    "The spell runtime inheritance or network contract is incomplete.");
            }
        }

        private static void ValidateSpellAssets()
        {
            FireballSpell fireball = LoadRequiredAsset<FireballSpell>(k_FireballPath);
            CasterWeaponItem catalyst =
                LoadRequiredAsset<CasterWeaponItem>(k_CatalystPath);
            if (fireball.SpellClass != SpellClass.Incantation ||
                !Mathf.Approximately(fireball.FullChargeModifier, 1.4f) ||
                !Mathf.Approximately(fireball.FireDamage, 150f) ||
                fireball.FireballPrefab == null ||
                catalyst.SpellClass != SpellClass.Incantation ||
                catalyst.RightHandAction is not CastIncantationAction ||
                catalyst.LeftHandAction is not CastIncantationAction)
            {
                throw new InvalidOperationException(
                    "Fireball or Incantation Catalyst data is incomplete.");
            }
        }

        private static void ValidateProjectilePrefab()
        {
            GameObject fireball = LoadRequiredAsset<GameObject>(k_FireballPrefabPath);
            Rigidbody rigidbody = fireball.GetComponent<Rigidbody>();
            SphereCollider travelCollider = fireball.GetComponent<SphereCollider>();
            SpellProjectileDamageCollider damageCollider =
                fireball.GetComponentInChildren<SpellProjectileDamageCollider>(true);
            Rigidbody damageRigidbody = damageCollider?.GetComponent<Rigidbody>();
            if (fireball.layer != k_ProjectileLayer ||
                rigidbody == null ||
                rigidbody.useGravity ||
                travelCollider == null ||
                travelCollider.isTrigger ||
                damageCollider == null ||
                damageCollider.gameObject.layer !=
                    LayerMask.NameToLayer("Damage Collider") ||
                damageRigidbody == null ||
                !damageRigidbody.isKinematic)
            {
                throw new InvalidOperationException(
                    "Fireball requires its travel and damage collider hierarchy.");
            }
        }

        private static void ValidatePlayerAndDatabase()
        {
            CasterWeaponItem catalyst =
                LoadRequiredAsset<CasterWeaponItem>(k_CatalystPath);
            FireballSpell fireball = LoadRequiredAsset<FireballSpell>(k_FireballPath);
            GameObject databaseRoot = PrefabUtility.LoadPrefabContents(
                k_DatabasePrefabPath);
            GameObject playerRoot = PrefabUtility.LoadPrefabContents(
                k_PlayerPrefabPath);
            try
            {
                WorldItemDatabase database =
                    GetRequiredComponent<WorldItemDatabase>(databaseRoot);
                PlayerInventoryManager inventory =
                    GetRequiredComponent<PlayerInventoryManager>(playerRoot);
                SerializedObject serializedInventory =
                    new SerializedObject(inventory);
                bool catalystInBothSlots =
                    GetRequiredProperty(
                            serializedInventory,
                            "m_weaponsInRightHandSlots")
                        .GetArrayElementAtIndex(2).objectReferenceValue == catalyst &&
                    GetRequiredProperty(
                            serializedInventory,
                            "m_weaponsInLeftHandSlots")
                        .GetArrayElementAtIndex(2).objectReferenceValue == catalyst;
                if (!database.Items.Contains(catalyst) ||
                    !database.Items.Contains(fireball) ||
                    database.Spells.Count(spell => spell == fireball) != 1 ||
                    database.GetSpellByID(fireball.ItemID) != fireball ||
                    !catalystInBothSlots ||
                    GetRequiredProperty(serializedInventory, "m_startingSpell")
                        .objectReferenceValue != fireball)
                {
                    throw new InvalidOperationException(
                        "The player or WorldItemDatabase spell registration is incomplete.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(databaseRoot);
                PrefabUtility.UnloadPrefabContents(playerRoot);
            }
        }

        private static void ValidateAnimator()
        {
            AnimatorController controller =
                LoadRequiredAsset<AnimatorController>(k_AnimatorPath);
            string[] parameters = controller.parameters
                .Select(parameter => parameter.name)
                .ToArray();
            AnimatorStateMachine stateMachine = controller.layers
                .Single(layer => layer.name == k_ActionLayerName)
                .stateMachine;
            HashSet<string> states = stateMachine.states
                .Select(child => child.state.name)
                .ToHashSet();
            bool hasCompleteEvents = s_spellAnimations
                .Where(definition => definition.StateName.Contains("_Release"))
                .All(definition => AnimationUtility.GetAnimationEvents(
                        LoadRequiredAsset<AnimationClip>(definition.AnimationPath))
                    .Any(animationEvent =>
                        animationEvent.functionName == "CompleteSpellCast"));
            if (!parameters.Contains("isChargingRightSpell") ||
                !parameters.Contains("isChargingLeftSpell") ||
                !parameters.Contains("isSpellFullyCharged") ||
                s_spellAnimations.Any(definition =>
                    !states.Contains(definition.StateName)) ||
                !hasCompleteEvents)
            {
                throw new InvalidOperationException(
                    "The spell Animator parameters, states, or events are incomplete.");
            }
        }

        private static void ValidateInput()
        {
            InputActionAsset inputAsset =
                LoadRequiredAsset<InputActionAsset>(k_InputAssetPath);
            InputActionMap movement = inputAsset.FindActionMap("Player Movement", true);
            foreach (string actionName in new[] { "RB", "LB" })
            {
                InputAction action = movement.FindAction(actionName, true);
                if (action.bindings.Count != 2 ||
                    action.bindings.Any(binding =>
                        !string.Equals(
                            binding.interactions,
                            "Hold(duration=0.05)",
                            StringComparison.OrdinalIgnoreCase)))
                {
                    throw new InvalidOperationException(
                        $"{actionName} requires Hold interactions for both schemes.");
                }
            }

            if (LayerMask.NameToLayer(k_ProjectileLayerName) != k_ProjectileLayer)
            {
                throw new InvalidOperationException(
                    "The Projectile physics layer is not configured.");
            }
        }

        private static void EnsureBoolParameter(
            AnimatorController controller,
            string parameterName)
        {
            if (controller.parameters.Any(parameter =>
                    parameter.name == parameterName))
            {
                return;
            }

            controller.AddParameter(parameterName, AnimatorControllerParameterType.Bool);
        }

        private static AnimatorState GetOrCreateState(
            AnimatorStateMachine stateMachine,
            string stateName)
        {
            AnimatorState state = stateMachine.states
                .Select(child => child.state)
                .FirstOrDefault(candidate => candidate.name == stateName);
            return state ?? stateMachine.AddState(stateName);
        }

        private static void ConfigureTransition(
            AnimatorState source,
            AnimatorState destination,
            bool hasExitTime,
            float exitTime,
            string falseCondition)
        {
            foreach (AnimatorStateTransition existing in source.transitions
                         .Where(transition =>
                             transition.destinationState == destination)
                         .ToArray())
            {
                source.RemoveTransition(existing);
            }

            AnimatorStateTransition transition = source.AddTransition(destination);
            transition.hasExitTime = hasExitTime;
            transition.exitTime = exitTime;
            transition.hasFixedDuration = true;
            transition.duration = 0.05f;
            transition.canTransitionToSelf = false;
            if (!string.IsNullOrEmpty(falseCondition))
            {
                transition.AddCondition(
                    AnimatorConditionMode.IfNot,
                    0f,
                    falseCondition);
            }
        }

        private static void SetQuickSlot(
            SerializedObject serializedInventory,
            string propertyName,
            int slotIndex,
            WeaponItem weapon)
        {
            SerializedProperty slots = GetRequiredProperty(
                serializedInventory,
                propertyName);
            if (slots.arraySize <= slotIndex)
            {
                slots.arraySize = slotIndex + 1;
            }

            slots.GetArrayElementAtIndex(slotIndex).objectReferenceValue = weapon;
        }

        private static void AppendUniqueObject(
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

        private static T CreateOrLoadAsset<T>(string path)
            where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EditPrefab(string path, Action<GameObject> edit)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                edit(root);
                if (PrefabUtility.SaveAsPrefabAsset(root, path) == null)
                {
                    throw new InvalidOperationException($"Could not save {path}.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void SavePrefab(GameObject root, string path)
        {
            if (PrefabUtility.SaveAsPrefabAsset(root, path) == null)
            {
                throw new InvalidOperationException($"Could not save {path}.");
            }
        }

        private static T LoadRequiredAsset<T>(string path)
            where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            return asset != null
                ? asset
                : throw new InvalidOperationException($"Missing asset {path}.");
        }

        private static Sprite LoadRequiredSprite(string path)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null)
            {
                return sprite;
            }

            TextureImporter importer = AssetImporter.GetAtPath(path) as
                TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException(
                    $"Missing sprite texture {path}.");
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path) ??
                throw new InvalidOperationException(
                    $"Could not import {path} as a Sprite.");
        }

        private static T GetRequiredComponent<T>(GameObject root)
            where T : Component
        {
            T component = root.GetComponent<T>();
            return component != null
                ? component
                : throw new InvalidOperationException(
                    $"{root.name} is missing {typeof(T).Name}.");
        }

        private static SerializedProperty GetRequiredProperty(
            SerializedObject serializedObject,
            string propertyName)
        {
            return serializedObject.FindProperty(propertyName) ??
                throw new InvalidOperationException(
                    $"{serializedObject.targetObject.name} is missing {propertyName}.");
        }

        private static void SetObjectReference(
            SerializedObject serializedObject,
            string propertyName,
            UnityEngine.Object value)
        {
            GetRequiredProperty(serializedObject, propertyName).objectReferenceValue =
                value;
        }

        private readonly struct SpellAnimationDefinition
        {
            public SpellAnimationDefinition(string stateName, string animationPath)
            {
                StateName = stateName;
                AnimationPath = animationPath;
            }

            public string StateName { get; }

            public string AnimationPath { get; }
        }
    }
}
