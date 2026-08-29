using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ZZ.Editor
{
    /// <summary>Creates and validates the authored particle trails required by EP156.</summary>
    public static class WeaponTrailSystemSetup
    {
        private const string k_TrailMaterialPath =
            "Assets/_Game/Art/VFX/Combat/Weapon Trail.mat";
        private const string k_TrailPrefabPath =
            "Assets/_Game/Prefabs/Effects/Weapon Trail.prefab";
        private const string k_BroadswordPrefabPath =
            "Assets/_Game/Prefabs/Equipment/Weapons/Melee Weapons/Broadsword.prefab";
        private const string k_StraightSwordPrefabPath =
            "Assets/_Game/Prefabs/Equipment/Weapons/Melee Weapons/Straight Sword.prefab";

        private static readonly string[] s_weaponPrefabPaths =
        {
            k_BroadswordPrefabPath,
            k_StraightSwordPrefabPath
        };

        private static readonly Color s_trailColor =
            new(0.45f, 0.82f, 1f, 0.9f);

        [MenuItem("Tools/Elden/Configure Weapon Trail System")]
        public static void ConfigureWeaponTrailSystem()
        {
            Material trailMaterial = ConfigureTrailMaterial();
            GameObject trailPrefab = ConfigureTrailPrefab(trailMaterial);
            foreach (string prefabPath in s_weaponPrefabPaths)
            {
                ConfigureWeaponPrefab(prefabPath, trailPrefab);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateWeaponTrailSystem();
            Debug.Log(
                "[WeaponTrailSystemSetup] Configured EP156 particle trails " +
                "for Broadsword and Straight Sword.");
        }

        [MenuItem("Tools/Elden/Validate Weapon Trail System")]
        public static void ValidateWeaponTrailSystem()
        {
            Material material = LoadRequiredAsset<Material>(
                k_TrailMaterialPath);
            GameObject trailPrefab = LoadRequiredAsset<GameObject>(
                k_TrailPrefabPath);
            ValidateTrailParticles(
                trailPrefab.GetComponent<ParticleSystem>(),
                material,
                k_TrailPrefabPath);
            foreach (string prefabPath in s_weaponPrefabPaths)
            {
                ValidateWeaponPrefab(prefabPath, material);
            }

            Debug.Log(
                "[WeaponTrailSystemSetup] EP156 weapon trail validation passed.");
        }

        private static Material ConfigureTrailMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(
                k_TrailMaterialPath);
            if (material == null)
            {
                Shader shader = FindParticleShader();
                material = new Material(shader)
                {
                    name = "Weapon Trail",
                    renderQueue = (int)RenderQueue.Transparent,
                    enableInstancing = true
                };
                AssetDatabase.CreateAsset(material, k_TrailMaterialPath);
            }

            SetColorIfPresent(material, "_BaseColor", s_trailColor);
            SetColorIfPresent(material, "_Color", s_trailColor);
            SetFloatIfPresent(material, "_Surface", 1f);
            SetFloatIfPresent(material, "_Blend", 1f);
            SetFloatIfPresent(material, "_SrcBlend", (float)BlendMode.SrcAlpha);
            SetFloatIfPresent(
                material,
                "_DstBlend",
                (float)BlendMode.OneMinusSrcAlpha);
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject ConfigureTrailPrefab(Material trailMaterial)
        {
            GameObject source = new(
                "Weapon Trail Particles",
                typeof(ParticleSystem));
            try
            {
                ConfigureParticles(
                    source.GetComponent<ParticleSystem>(),
                    trailMaterial);
                return PrefabUtility.SaveAsPrefabAsset(
                    source,
                    k_TrailPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
            }
        }

        private static Shader FindParticleShader()
        {
            string[] shaderNames =
            {
                "Universal Render Pipeline/Particles/Unlit",
                "Particles/Standard Unlit",
                "Sprites/Default"
            };
            foreach (string shaderName in shaderNames)
            {
                Shader shader = Shader.Find(shaderName);
                if (shader != null)
                {
                    return shader;
                }
            }

            throw new InvalidOperationException(
                "A compatible particle shader is required for weapon trails.");
        }

        private static void ConfigureWeaponPrefab(
            string prefabPath,
            GameObject trailPrefab)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                WeaponManager weaponManager =
                    root.GetComponent<WeaponManager>();
                if (weaponManager == null)
                {
                    throw new InvalidOperationException(
                        $"WeaponManager is missing from {prefabPath}.");
                }

                MeleeWeaponDamageCollider damageCollider =
                    root.GetComponentInChildren<MeleeWeaponDamageCollider>(true);
                if (damageCollider == null)
                {
                    throw new InvalidOperationException(
                        $"MeleeWeaponDamageCollider is missing from {prefabPath}.");
                }

                ParticleSystem particles = GetOrCreateTrailParticles(
                    root.transform,
                    damageCollider,
                    trailPrefab);
                ConfigureBladePlacement(particles, damageCollider);
                SerializedObject serializedManager =
                    new(weaponManager);
                serializedManager.FindProperty("m_particleWeaponTrail")
                    .objectReferenceValue = particles;
                serializedManager.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static ParticleSystem GetOrCreateTrailParticles(
            Transform root,
            MeleeWeaponDamageCollider damageCollider,
            GameObject trailPrefab)
        {
            ParticleSystem[] particleSystems =
                root.GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem particles in particleSystems)
            {
                if (!particles.name.Contains("Weapon Trail"))
                {
                    continue;
                }

                GameObject source =
                    PrefabUtility.GetCorrespondingObjectFromSource(
                        particles.gameObject);
                if (source == trailPrefab)
                {
                    return particles;
                }

                UnityEngine.Object.DestroyImmediate(particles.gameObject);
                break;
            }

            Transform trailParent = damageCollider.transform.parent ?? root;
            Transform effects = trailParent.Find("Effects");
            if (effects == null)
            {
                GameObject effectsObject = new("Effects");
                effects = effectsObject.transform;
                effects.SetParent(trailParent, false);
            }

            GameObject trailObject = (GameObject)PrefabUtility.InstantiatePrefab(
                trailPrefab,
                effects);
            return trailObject.GetComponent<ParticleSystem>();
        }

        private static void ConfigureParticles(
            ParticleSystem particles,
            Material trailMaterial)
        {
            ParticleSystem.MainModule main = particles.main;
            main.duration = 1f;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.35f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.22f);
            main.startColor = s_trailColor;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 256;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = false;
            emission.rateOverTime = 40f;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, 10)
            });

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.SingleSidedEdge;
            shape.radius = 0.5f;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime =
                particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(
                CreateTrailGradient());

            ParticleSystem.TrailModule trails = particles.trails;
            trails.enabled = true;
            trails.mode = ParticleSystemTrailMode.PerParticle;
            trails.ratio = 1f;
            trails.lifetime = 1f;
            trails.minVertexDistance = 0.01f;
            trails.textureMode = ParticleSystemTrailTextureMode.Stretch;
            trails.worldSpace = true;
            trails.dieWithParticles = false;
            trails.widthOverTrail = new ParticleSystem.MinMaxCurve(
                1f,
                new AnimationCurve(
                    new Keyframe(0f, 1f),
                    new Keyframe(1f, 0.05f)));

            ParticleSystemRenderer particleRenderer =
                particles.GetComponent<ParticleSystemRenderer>();
            particleRenderer.renderMode = ParticleSystemRenderMode.None;
            particleRenderer.trailMaterial = trailMaterial;
            particleRenderer.sortingOrder = 2;
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            EditorUtility.SetDirty(particles);
            EditorUtility.SetDirty(particleRenderer);
        }

        private static void ConfigureBladePlacement(
            ParticleSystem particles,
            MeleeWeaponDamageCollider damageCollider)
        {
            BoxCollider bladeCollider =
                damageCollider.GetComponent<BoxCollider>();
            if (bladeCollider == null)
            {
                throw new InvalidOperationException(
                    "Weapon trail setup requires a BoxCollider blade volume.");
            }

            Transform particleTransform = particles.transform;
            particleTransform.localPosition =
                damageCollider.transform.localPosition + bladeCollider.center;
            particleTransform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            particleTransform.localScale = Vector3.one;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.radius = Mathf.Max(0.1f, bladeCollider.size.y * 0.5f);
        }

        private static Gradient CreateTrailGradient()
        {
            Gradient gradient = new();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(s_trailColor, 0.35f),
                    new GradientColorKey(new Color(0.1f, 0.35f, 0.8f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.9f, 0f),
                    new GradientAlphaKey(0.55f, 0.45f),
                    new GradientAlphaKey(0f, 1f)
                });
            return gradient;
        }

        private static void ValidateWeaponPrefab(
            string prefabPath,
            Material expectedMaterial)
        {
            GameObject prefab = LoadRequiredAsset<GameObject>(prefabPath);
            WeaponManager weaponManager = prefab.GetComponent<WeaponManager>();
            ParticleSystem particles = weaponManager?.WeaponTrailParticles;
            ValidateTrailParticles(particles, expectedMaterial, prefabPath);
            if (PrefabUtility.GetCorrespondingObjectFromSource(
                    particles.gameObject) == null)
            {
                throw new InvalidOperationException(
                    $"Weapon trail on {prefabPath} must use the shared prefab.");
            }
        }

        private static void ValidateTrailParticles(
            ParticleSystem particles,
            Material expectedMaterial,
            string assetPath)
        {
            ParticleSystemRenderer particleRenderer =
                particles?.GetComponent<ParticleSystemRenderer>();
            if (particles == null ||
                particles.main.playOnAwake ||
                particles.main.simulationSpace !=
                    ParticleSystemSimulationSpace.World ||
                particles.shape.shapeType !=
                    ParticleSystemShapeType.SingleSidedEdge ||
                !particles.trails.enabled ||
                !Mathf.Approximately(particles.trails.ratio, 1f) ||
                particleRenderer == null ||
                particleRenderer.renderMode != ParticleSystemRenderMode.None ||
                particleRenderer.trailMaterial != expectedMaterial)
            {
                throw new InvalidOperationException(
                    $"Weapon trail is not configured correctly at {assetPath}.");
            }
        }

        private static T LoadRequiredAsset<T>(string path)
            where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                throw new InvalidOperationException(
                    $"Required asset was not found at {path}.");
            }

            return asset;
        }

        private static void SetColorIfPresent(
            Material material,
            string propertyName,
            Color value)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetColor(propertyName, value);
            }
        }

        private static void SetFloatIfPresent(
            Material material,
            string propertyName,
            float value)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetFloat(propertyName, value);
            }
        }
    }
}
