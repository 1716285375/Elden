using UnityEditor;
using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Resolves the shared greybox materials, creating them on first use.
    /// </summary>
    /// <remarks>
    /// Greybox colour is not decoration: it encodes spatial function so a room can
    /// be read at a glance. Walkable reads light, blocking reads dark, props read
    /// blue, markers read hot, triggers read transparent.
    /// </remarks>
    public static class LV01GreyboxMaterials
    {
        private const string k_Folder = "Assets/_Game/Art/Shared/Materials/Greybox";
        private const string k_LitShader = "Universal Render Pipeline/Lit";
        private const string k_UnlitShader = "Universal Render Pipeline/Unlit";

        /// <summary>Returns the material that encodes the supplied spatial role.</summary>
        public static Material Get(GreyboxRole role)
        {
            return role switch
            {
                GreyboxRole.Walkable => LoadOrCreate("MAT_Greybox_Walkable",
                    new Color(0.75f, 0.75f, 0.75f), "Surfaces the player stands on"),
                GreyboxRole.Blocking => LoadOrCreate("MAT_Greybox_Blocking",
                    new Color(0.24f, 0.24f, 0.24f), "Solid volumes the player cannot pass"),
                GreyboxRole.Cover => LoadOrCreate("MAT_Greybox_Cover",
                    new Color(0.43f, 0.43f, 0.43f), "Chest-high cover"),
                GreyboxRole.Prop => LoadOrCreate("MAT_Greybox_Prop",
                    new Color(0.37f, 0.48f, 0.6f), "Dressing placeholders"),
                GreyboxRole.Marker => LoadOrCreate("MAT_Greybox_Marker",
                    new Color(1f, 0.18f, 0.82f), "Gameplay markers"),
                GreyboxRole.Trigger => LoadOrCreateTransparent("MAT_Greybox_Trigger",
                    new Color(0f, 0.9f, 1f), "Trigger volumes"),
                _ => LoadOrCreate("MAT_Greybox_Base",
                    new Color(0.54f, 0.54f, 0.54f), "Neutral structure")
            };
        }

        private static Material LoadOrCreate(string name, Color color, string purpose)
        {
            return LoadOrCreate(name, k_LitShader, color, purpose, false);
        }

        private static Material LoadOrCreateTransparent(string name, Color color, string purpose)
        {
            return LoadOrCreate(name, k_UnlitShader, color, purpose, true);
        }

        private static Material LoadOrCreate(
            string name,
            string shaderName,
            Color color,
            string purpose,
            bool transparent)
        {
            EnsureFolderExists();
            string path = $"{k_Folder}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                return material;
            }

            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError(
                    $"[LV01Greybox] Shader '{shaderName}' not found; " +
                    $"falling back to the built-in Standard shader for {name}.");
                shader = Shader.Find("Standard");
            }

            material = new Material(shader)
            {
                name = name
            };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0f);

            if (transparent)
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_AlphaClip", 0f);
                material.SetOverrideTag("RenderType", "Transparent");
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            }

            AssetDatabase.CreateAsset(material, path);
            Debug.Log($"[LV01Greybox] Created {path} - {purpose}.");
            return material;
        }

        private static void EnsureFolderExists()
        {
            if (AssetDatabase.IsValidFolder(k_Folder))
            {
                return;
            }

            string parent = "Assets/_Game/Art/Shared/Materials";
            AssetDatabase.CreateFolder(parent, "Greybox");
        }
    }
}
