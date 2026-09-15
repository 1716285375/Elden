using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ZZ.Rendering.Stylized;

namespace ZZ.Editor
{
    /// <summary>Authors a clean animation palette using the existing toon renderer and unchanged source textures.</summary>
    public static class AnimeArtDirectionSetup
    {
        private const string k_ProfilePath = "Assets/_Game/Settings/Rendering/Volumes/VP_HiFiStyle.asset";
        private const string k_BackupDirectory = ".utmp/art-direction-before";

        /// <summary>Applies an internally reviewable animation preset, preserving originals in a local backup.</summary>
        [MenuItem("Tools/ZZ/Art/Apply Clean Animation Direction")]
        public static void Apply()
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException("Stop Play Mode before authoring art direction.");
            }
            Directory.CreateDirectory(k_BackupDirectory);
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(k_ProfilePath);
            if (profile == null)
            {
                throw new InvalidOperationException("The authored style profile is missing.");
            }
            Backup(k_ProfilePath);
            if (profile.TryGet(out HiFiStyleVolume comic))
            {
                comic.Enable.Override(false);
                EditorUtility.SetDirty(comic);
            }
            Bloom bloom = GetOrAdd<Bloom>(profile);
            bloom.intensity.Override(0.15f);
            bloom.threshold.Override(1.2f);
            bloom.scatter.Override(0.6f);
            ColorAdjustments color = GetOrAdd<ColorAdjustments>(profile);
            color.saturation.Override(6f);
            color.contrast.Override(4f);
            color.postExposure.Override(0.05f);
            EditorUtility.SetDirty(bloom);
            EditorUtility.SetDirty(color);
            EditorUtility.SetDirty(profile);

            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/_Game" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null || material.shader == null || material.shader.name != "Game/Stylized/HiFiToon")
                {
                    continue;
                }
                Backup(path);
                material.SetFloat("_SurfacePrintStrength", 0f);
                material.SetFloat("_ShadowThreshold", 0.5f);
                material.SetFloat("_ShadowSoftness", 0.035f);
                material.SetColor("_ShadowColor", new Color(0.48f, 0.52f, 0.68f, 1f));
                material.SetFloat("_OutlineWidthPixels", 1f);
                material.SetFloat("_RimIntensity", 0.15f);
                material.SetColor("_RimColor", new Color(0.84f, 0.92f, 1f, 1f));
                EditorUtility.SetDirty(material);
                count++;
            }
            AssetDatabase.SaveAssets();
            File.WriteAllText(".utmp/anime-art-direction.txt",
                $"Applied clean animation profile and {count} toon materials.\n" +
                "Textures and GUIDs preserved. Source asset rights remain unverified.\n");
        }

        private static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet(out T component))
            {
                return component;
            }
            component = profile.Add<T>();
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        /// <summary>Applies the first-party opaque animation shader to the player's existing material dependencies.</summary>
        [MenuItem("Tools/ZZ/Art/Apply Player Animation Materials")]
        public static void ApplyPlayerMaterials()
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException("Stop Play Mode before authoring player materials.");
            }
            Shader shader = Shader.Find("ZZ/Anime/Character");
            if (shader == null || ShaderUtil.ShaderHasError(shader))
            {
                throw new InvalidOperationException("Animation shader must compile before applying it.");
            }
            Directory.CreateDirectory(k_BackupDirectory);
            int count = 0;
            string[] dependencies = AssetDatabase.GetDependencies(
                "Assets/_Game/Prefabs/Characters/Player/Player.prefab", true);
            foreach (string path in dependencies)
            {
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null || material.shader == null ||
                    material.shader.name != "Universal Render Pipeline/Lit" ||
                    material.GetFloat("_Surface") > 0f || material.GetFloat("_AlphaClip") > 0f)
                {
                    continue;
                }
                Backup(path);
                material.shader = shader;
                material.shaderKeywords = Array.Empty<string>();
                EditorUtility.SetDirty(material);
                count++;
            }
            AssetDatabase.SaveAssets();
            File.AppendAllText(".utmp/anime-art-direction.txt", $"Player opaque materials converted: {count}\n");
        }

        private static void Backup(string path)
        {
            string backupPath = Path.Combine(k_BackupDirectory,
                AssetDatabase.AssetPathToGUID(path) + Path.GetExtension(path));
            if (!File.Exists(backupPath))
            {
                File.Copy(path, backupPath);
            }
        }
    }
}
