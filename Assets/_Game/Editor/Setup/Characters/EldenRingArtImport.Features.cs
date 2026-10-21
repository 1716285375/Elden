using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ZZ.Editor
{
    public static partial class EldenRingArtImport
    {
        private const string k_FeatureSource = "F:/MyProject/RE/EldenRing/Converted/PlayerFeatures";

        private static void InstallFacialFeatures(Transform visual)
        {
            Transform previous = visual.Find("Extracted Facial Features");
            if (previous != null)
            {
                Object.DestroyImmediate(previous.gameObject);
            }
            var root = new GameObject("Extracted Facial Features").transform;
            root.SetParent(visual, false);
            Directory.CreateDirectory(k_AppearanceOutput + "/Features");
            foreach (string file in Directory.GetFiles(k_FeatureSource + "/Textures", "*.png"))
            {
                string destination = k_AppearanceOutput + "/Features/" + Path.GetFileName(file);
                File.Copy(file, destination, true);
                AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceSynchronousImport);
            }
            var source = JsonUtility.FromJson<ExportDocument>(File.ReadAllText(k_FeatureSource + "/models.json"));
            var controls = JsonUtility.FromJson<FaceControlDocument>(
                File.ReadAllText(k_FeatureSource + "/feature-controls.json")).Meshes.ToDictionary(item => item.MeshName);
            SkinnedMeshRenderer template = visual.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .First(item => item.name.StartsWith("FG_A_0100_"));
            var targets = template.bones.ToDictionary(bone => bone.name.Replace("ER_Set1100Avatar_", "")
                .Replace("ER_", ""), bone => bone);
            foreach (ModelRecord model in source.Models)
            {
                var group = new GameObject("Appearance Feature " + model.Name.Substring(model.Name.Length - 4));
                group.transform.SetParent(root, false);
                var bones = BuildSourceBones(new ExportDocument { Models = new[] { model } });
                for (int index = 0; index < model.Meshes.Length; index++)
                {
                    var part = new GameObject(model.Name + "_" + index);
                    part.transform.SetParent(group.transform, false);
                    var renderer = part.AddComponent<SkinnedMeshRenderer>();
                    ImportMesh(model, model.Meshes[index], bones, targets, renderer, index, k_AppearanceOutput);
                    renderer.sharedMesh.bindposes = template.sharedMesh.bindposes;
                    renderer.rootBone = template.rootBone;
                    renderer.localBounds = template.localBounds;
                    renderer.sharedMaterial = FeatureMaterial(model, model.Meshes[index].Material);
                    AddFaceControls(renderer.sharedMesh, controls[part.name]);
                }
            }
            File.Copy(k_FeatureSource + "/feature-controls-report.json",
                "Docs/CharacterCreationRefresh/feature-export-report.json", true);
        }

        private static Material FeatureMaterial(ModelRecord model, int materialIndex)
        {
            bool isEye = model.Name == "FG_A_1500";
            string path = k_AppearanceOutput + "/Features/" + model.Name + "_" + materialIndex + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find(isEye ? "ZZ/Characters/Extracted Eye" :
                    "Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            if (isEye)
            {
                material.SetTexture("_BaseMap", FeatureTexture("FG_A_1500_eye_a"));
                material.SetTexture("_IrisMask", FeatureTexture("FG_A_1500_eye_3m"));
            }
            else
            {
                TextureRecord normal = model.Materials[materialIndex].Textures.First(item => item.Type.Contains("Normal"));
                string opacity = normal.Name.Substring(0, normal.Name.Length - 2) + "_opacity";
                material.SetTexture("_BaseMap", FeatureTexture(opacity));
                material.SetFloat("_AlphaClip", 1f);
                material.SetFloat("_Cutoff", 0.25f);
                material.SetFloat("_Cull", 0f);
                material.SetFloat("_Smoothness", 0.15f);
                material.EnableKeyword("_ALPHATEST_ON");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Texture2D FeatureTexture(string name)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                k_AppearanceOutput + "/Features/" + name + ".png");
            if (texture == null)
            {
                throw new InvalidDataException("Missing original facial texture: " + name);
            }
            return texture;
        }
    }
}
