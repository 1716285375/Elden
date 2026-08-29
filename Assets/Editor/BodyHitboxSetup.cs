using System;
using UnityEditor;
using UnityEngine;

namespace ZZ.Editor
{
    /// <summary>
    /// Upgrades the player from CharacterController-only damage detection to a
    /// skeleton-accurate body hitbox system, as described in the Body Accurate
    /// Hit Detection plan.
    /// </summary>
    public static class BodyHitboxSetup
    {
        private const string k_PlayerPrefabPath = "Assets/_Game/Prefabs/Characters/Player/Player.prefab";
        private const string k_TagManagerPath = "ProjectSettings/TagManager.asset";
        private const string k_PhysicsManagerPath = "ProjectSettings/DynamicsManager.asset";
        private const string k_DamageColliderLayerName = "Damage Collider";
        private const string k_DamageableCharacterLayerName = "Damageable Character";

        private readonly struct HitboxDefinition
        {
            public HitboxDefinition(
                HumanBodyBones bone,
                int direction,
                Vector3 center,
                float radius,
                float height)
            {
                Bone = bone;
                Direction = direction;
                Center = center;
                Radius = radius;
                Height = height;
            }

            public HumanBodyBones Bone { get; }
            public int Direction { get; }
            public Vector3 Center { get; }
            public float Radius { get; }
            public float Height { get; }
        }

        /// <summary>
        /// Body regions represented by a small set of capsule hitboxes, each aligned
        /// with its skeleton bone so it follows the animation. Values are first-pass
        /// approximations and should be tuned in the Inspector per model.
        /// </summary>
        private static readonly HitboxDefinition[] s_Hitboxes =
        {
            new HitboxDefinition(HumanBodyBones.Head, 1, new Vector3(0f, 0.1f, 0f), 0.12f, 0.24f),
            new HitboxDefinition(HumanBodyBones.Neck, 1, Vector3.zero, 0.07f, 0.12f),
            new HitboxDefinition(HumanBodyBones.Spine, 1, new Vector3(0f, 0.15f, 0f), 0.22f, 0.45f),

            new HitboxDefinition(HumanBodyBones.LeftUpperArm, 1, new Vector3(0f, -0.14f, 0f), 0.06f, 0.3f),
            new HitboxDefinition(HumanBodyBones.RightUpperArm, 1, new Vector3(0f, -0.14f, 0f), 0.06f, 0.3f),
            new HitboxDefinition(HumanBodyBones.LeftLowerArm, 1, new Vector3(0f, -0.12f, 0f), 0.05f, 0.26f),
            new HitboxDefinition(HumanBodyBones.RightLowerArm, 1, new Vector3(0f, -0.12f, 0f), 0.05f, 0.26f),
            new HitboxDefinition(HumanBodyBones.LeftHand, 1, new Vector3(0f, -0.06f, 0f), 0.05f, 0.16f),
            new HitboxDefinition(HumanBodyBones.RightHand, 1, new Vector3(0f, -0.06f, 0f), 0.05f, 0.16f),

            new HitboxDefinition(HumanBodyBones.LeftUpperLeg, 1, new Vector3(0f, -0.2f, 0f), 0.09f, 0.42f),
            new HitboxDefinition(HumanBodyBones.RightUpperLeg, 1, new Vector3(0f, -0.2f, 0f), 0.09f, 0.42f),
            new HitboxDefinition(HumanBodyBones.LeftLowerLeg, 1, new Vector3(0f, -0.2f, 0f), 0.08f, 0.42f),
            new HitboxDefinition(HumanBodyBones.RightLowerLeg, 1, new Vector3(0f, -0.2f, 0f), 0.08f, 0.42f),
            new HitboxDefinition(HumanBodyBones.LeftFoot, 1, new Vector3(0f, -0.04f, 0f), 0.06f, 0.2f),
            new HitboxDefinition(HumanBodyBones.RightFoot, 1, new Vector3(0f, -0.04f, 0f), 0.06f, 0.2f)
        };

        [MenuItem("Tools/Elden/Configure Body Hitboxes")]
        public static void ConfigureBodyHitboxes()
        {
            int damageableLayer = EnsureLayer(k_DamageableCharacterLayerName);
            ConfigureLayerCollisions(damageableLayer);
            ConfigurePlayerPrefab(damageableLayer);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateBodyHitboxes();
            Debug.Log(
                "[BodyHitboxSetup] Configured skeleton body hitboxes, a kinematic Rigidbody, " +
                "and the damageable layer collision matrix.");
        }

        [MenuItem("Tools/Elden/Validate Body Hitboxes")]
        public static void ValidateBodyHitboxes()
        {
            ValidateLayerCollisions();
            ValidatePlayerPrefab();
            Debug.Log(
                "[BodyHitboxValidation] Body hitboxes, Rigidbody, and layer matrix are valid.");
        }

        private static void ConfigurePlayerPrefab(int damageableLayer)
        {
            GameObject playerRoot = PrefabUtility.LoadPrefabContents(k_PlayerPrefabPath);

            try
            {
                Animator animator = playerRoot.GetComponentInChildren<Animator>(true);
                if (animator == null || !animator.isHuman)
                {
                    throw new InvalidOperationException(
                        "The Player prefab needs a humanoid Animator to resolve body bones.");
                }

                foreach (HitboxDefinition hitbox in s_Hitboxes)
                {
                    Transform bone = animator.GetBoneTransform(hitbox.Bone);
                    if (bone == null)
                    {
                        Debug.LogWarning(
                            $"[BodyHitboxSetup] Missing bone {hitbox.Bone}; skipped.");
                        continue;
                    }

                    CapsuleCollider collider =
                        GetOrAddComponent<CapsuleCollider>(bone.gameObject);
                    collider.direction = hitbox.Direction;
                    collider.center = hitbox.Center;
                    collider.radius = hitbox.Radius;
                    collider.height = hitbox.Height;
                    collider.isTrigger = false;
                    bone.gameObject.layer = damageableLayer;
                    EditorUtility.SetDirty(collider);
                }

                Rigidbody rigidbody = GetOrAddComponent<Rigidbody>(playerRoot);
                rigidbody.useGravity = false;
                rigidbody.isKinematic = true;
                EditorUtility.SetDirty(rigidbody);

                PrefabUtility.SaveAsPrefabAsset(playerRoot, k_PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(playerRoot);
            }
        }

        private static void ConfigureLayerCollisions(int damageableLayer)
        {
            int damageColliderLayer = GetRequiredLayer(k_DamageColliderLayerName);

            // Weapon damage colliders detect body hitboxes.
            Physics.IgnoreLayerCollision(damageColliderLayer, damageableLayer, false);

            // Body hitboxes of different characters still collide with each other.
            Physics.IgnoreLayerCollision(damageableLayer, damageableLayer, false);

            UnityEngine.Object physicsManager =
                LoadRequiredSettingsAsset(k_PhysicsManagerPath);
            EditorUtility.SetDirty(physicsManager);
        }

        private static void ValidatePlayerPrefab()
        {
            GameObject playerRoot = PrefabUtility.LoadPrefabContents(k_PlayerPrefabPath);

            try
            {
                int damageableLayer = GetRequiredLayer(k_DamageableCharacterLayerName);
                Animator animator = playerRoot.GetComponentInChildren<Animator>(true);
                if (animator == null || !animator.isHuman)
                {
                    throw new InvalidOperationException(
                        "The Player prefab needs a humanoid Animator.");
                }

                Rigidbody rigidbody = playerRoot.GetComponent<Rigidbody>();
                if (rigidbody == null || !rigidbody.isKinematic || rigidbody.useGravity)
                {
                    throw new InvalidOperationException(
                        "The Player root needs a kinematic, gravity-free Rigidbody.");
                }

                foreach (HitboxDefinition hitbox in s_Hitboxes)
                {
                    Transform bone = animator.GetBoneTransform(hitbox.Bone);
                    CapsuleCollider collider = bone?.GetComponent<CapsuleCollider>();
                    if (bone == null ||
                        collider == null ||
                        collider.isTrigger ||
                        bone.gameObject.layer != damageableLayer)
                    {
                        throw new InvalidOperationException(
                            $"Bone {hitbox.Bone} needs a non-trigger CapsuleCollider " +
                            $"on the {k_DamageableCharacterLayerName} layer.");
                    }
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(playerRoot);
            }
        }

        private static void ValidateLayerCollisions()
        {
            int damageColliderLayer = GetRequiredLayer(k_DamageColliderLayerName);
            int damageableLayer = GetRequiredLayer(k_DamageableCharacterLayerName);
            if (Physics.GetIgnoreLayerCollision(damageColliderLayer, damageableLayer) ||
                Physics.GetIgnoreLayerCollision(damageableLayer, damageableLayer))
            {
                throw new InvalidOperationException(
                    "Damage Collider and Damageable Character layers must collide.");
            }
        }

        private static int EnsureLayer(string layerName)
        {
            int existingLayer = LayerMask.NameToLayer(layerName);
            if (existingLayer >= 0)
            {
                return existingLayer;
            }

            UnityEngine.Object tagManager = LoadRequiredSettingsAsset(k_TagManagerPath);
            SerializedObject serializedTagManager = new SerializedObject(tagManager);
            SerializedProperty layers = GetRequiredProperty(serializedTagManager, "layers");
            for (int layerIndex = 8; layerIndex < layers.arraySize; layerIndex++)
            {
                SerializedProperty layer = layers.GetArrayElementAtIndex(layerIndex);
                if (!string.IsNullOrEmpty(layer.stringValue))
                {
                    continue;
                }

                layer.stringValue = layerName;
                serializedTagManager.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(tagManager);
                AssetDatabase.SaveAssets();
                return layerIndex;
            }

            throw new InvalidOperationException(
                $"No empty user layer is available for '{layerName}'.");
        }

        private static int GetRequiredLayer(string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            return layer >= 0
                ? layer
                : throw new InvalidOperationException(
                    $"Could not find the required '{layerName}' layer.");
        }

        private static UnityEngine.Object LoadRequiredSettingsAsset(string assetPath)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            return assets.Length > 0
                ? assets[0]
                : throw new InvalidOperationException($"Could not load {assetPath}.");
        }

        private static T GetOrAddComponent<T>(GameObject gameObject)
            where T : Component
        {
            T component = gameObject.GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
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
