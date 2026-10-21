using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ZZ.Editor
{
    /// <summary>Retains authored terrain heights and density maps while replacing retired surface assets.</summary>
    public static class RealisticTerrainImport
    {
        private const string k_Output = "Assets/_Game/Art/Environment/Terrain";
        private const string k_Textures = RealisticNatureImport.Root + "/Textures/Terrain";

        /// <summary>Rebinds terrain rendering and collision to independent terrain data with realistic layers.</summary>
        public static void Replace(Scene scene)
        {
            if (!File.Exists("Docs/ArtRefresh/terrain-downloads.json"))
            {
                throw new InvalidOperationException("Finish verified terrain texture downloads first.");
            }
            Directory.CreateDirectory(k_Output);
            AssetDatabase.Refresh();
            foreach (string path in Directory.GetFiles(k_Textures).Where(path => !path.EndsWith(".meta")))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path.Replace('\\', '/'));
                bool normal = path.Contains("_nor_gl_");
                importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.sRGBTexture = !normal;
                importer.maxTextureSize = 2048;
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
            }
            foreach (Terrain terrain in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Terrain>(true)))
            {
                if (!AssetDatabase.GetAssetPath(terrain.terrainData).Contains("/IdyllicFantasyNature/"))
                {
                    continue;
                }
                TerrainData source = terrain.terrainData;
                TerrainData replacement = CloneTerrain(source);
                terrain.terrainData = replacement;
                terrain.materialTemplate = TerrainMaterial();
                foreach (TerrainCollider collider in scene.GetRootGameObjects().SelectMany(root =>
                    root.GetComponentsInChildren<TerrainCollider>(true)).Where(collider => collider.terrainData == source))
                {
                    collider.terrainData = replacement;
                }
            }
        }

        private static TerrainData CloneTerrain(TerrainData source)
        {
            string path = k_Output + "/" + source.name + ".asset";
            TerrainData data = AssetDatabase.LoadAssetAtPath<TerrainData>(path);
            if (data != null)
            {
                return data;
            }
            data = Object.Instantiate(source);
            data.name = source.name;
            data.terrainLayers = source.terrainLayers.Select(CloneLayer).ToArray();
            if (source.treeInstanceCount == 0)
            {
                data.treePrototypes = Array.Empty<TreePrototype>();
            }
            else
            {
                throw new InvalidDataException("Terrain trees require explicit prototype migration: " + source.name);
            }
            DetailPrototype[] details = data.detailPrototypes;
            Texture2D grass = AssetDatabase.LoadAssetAtPath<Texture2D>(
                RealisticNatureImport.Root + "/Textures/Packed/grass_medium_01_Base.png");
            if (grass == null)
            {
                throw new InvalidDataException("Missing alpha-packed realistic terrain grass.");
            }
            foreach (DetailPrototype detail in details)
            {
                detail.prototype = null;
                detail.prototypeTexture = grass;
                detail.usePrototypeMesh = false;
                detail.useInstancing = false;
                detail.renderMode = DetailRenderMode.GrassBillboard;
                detail.healthyColor = Color.white;
                detail.dryColor = new Color(0.82f, 0.8f, 0.65f);
            }
            data.detailPrototypes = details;
            if (data.size != source.size || data.heightmapResolution != source.heightmapResolution ||
                !data.GetHeights(0, 0, data.heightmapResolution, data.heightmapResolution).Cast<float>().SequenceEqual(
                    source.GetHeights(0, 0, source.heightmapResolution, source.heightmapResolution).Cast<float>()))
            {
                throw new InvalidDataException("Terrain height data changed while replacing art: " + source.name);
            }
            AssetDatabase.CreateAsset(data, path);
            return data;
        }

        private static TerrainLayer CloneLayer(TerrainLayer source)
        {
            string path = k_Output + "/" + source.name + ".terrainlayer";
            TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (layer != null)
            {
                return layer;
            }
            string name = source.name.ToLowerInvariant();
            string texture = name.Contains("grass") ? "grass_ground" : name.Contains("cobble") ? "cobblestone_floor_05" :
                name.Contains("forest") ? "forest_ground_04" : name.Contains("sand") ? "coast_sand_01" :
                name.Contains("stone") ? "brown_mud_rocks_01" : name.Contains("rock") ? "dark_rock" : "brown_mud";
            layer = Object.Instantiate(source);
            layer.name = source.name;
            layer.diffuseTexture = Texture(texture, "diff");
            layer.normalMapTexture = Texture(texture, "nor_gl");
            layer.maskMapTexture = null;
            layer.tileSize = new Vector2(8f, 8f);
            layer.metallic = 0f;
            layer.smoothness = 0.12f;
            layer.normalScale = 1f;
            AssetDatabase.CreateAsset(layer, path);
            return layer;
        }

        private static Texture2D Texture(string asset, string channel)
        {
            string path = Directory.GetFiles(k_Textures, asset + "_" + channel + "_2k.*")
                .First(candidate => !candidate.EndsWith(".meta"));
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path.Replace('\\', '/'));
        }

        private static Material TerrainMaterial()
        {
            string path = k_Output + "/Terrain.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            return material;
        }
    }
}
