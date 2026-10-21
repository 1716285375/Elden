using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ZZ.Editor
{
    public static partial class EldenRingArtImport
    {
        private const string k_BaseSource = "F:/MyProject/RE/EldenRing/Converted/PlayerBase";
        private const string k_BaseOutput = "Assets/_Game/Art/Characters/EldenRing/PlayerBase";
        private const string k_ModularRoot = "Extracted Modular Skin";

        /// <summary>Builds original body segments and independently wearable armor on the installed skeleton.</summary>
        [MenuItem("Tools/ZZ/Elden Ring/Install Modular Player Body")]
        public static void InstallModularPlayerBody()
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException("Stop Play Mode before installing the modular body.");
            }

            Directory.CreateDirectory(k_BaseOutput + "/Meshes");
            Directory.CreateDirectory(k_BaseOutput + "/Materials");
            Directory.CreateDirectory(k_BaseOutput + "/Textures");
            foreach (string texture in Directory.GetFiles(k_BaseSource, "*.png"))
            {
                File.Copy(texture, k_BaseOutput + "/Textures/" + Path.GetFileName(texture), true);
            }
            AssetDatabase.Refresh();
            ConfigureTextures(k_BaseOutput);
            var body = JsonUtility.FromJson<ExportDocument>(File.ReadAllText(k_BaseSource + "/models.json"));
            var face = JsonUtility.FromJson<ExportDocument>(File.ReadAllText(
                "F:/MyProject/RE/EldenRing/Converted/PlayerHead/models.json"));
            body.Models = body.Models.Concat(face.Models.Where(model => model.Name == "FG_A_0100")).ToArray();
            var outfit = JsonUtility.FromJson<ExportDocument>(File.ReadAllText(k_Source));
            var masks = JsonUtility.FromJson<MaskDocument>(File.ReadAllText(k_BaseSource + "/armor-masks.json"));
            GameObject player = PrefabUtility.LoadPrefabContents(k_Player);
            var report = new StringBuilder();
            try
            {
                Animator animator = player.GetComponentInChildren<Animator>(true);
                Transform visual = animator.transform.Find(k_VisualName);
                if (visual == null)
                {
                    throw new InvalidOperationException("Install the verified Set1100 skeleton first.");
                }

                SkinnedMeshRenderer template = visual.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .First(renderer => renderer.sharedMesh != null && renderer.bones.Length > 0);
                var targets = template.bones.ToDictionary(
                    bone => bone.name.StartsWith("ER_Set1100Avatar_", StringComparison.Ordinal)
                        ? bone.name.Substring("ER_Set1100Avatar_".Length)
                        : bone.name.Substring("ER_".Length), bone => bone);
                Matrix4x4[] bindposes = template.sharedMesh.bindposes;
                Transform rootBone = template.rootBone;
                Bounds armorBounds = template.localBounds;
                if (bindposes.Length != targets.Count)
                {
                    throw new InvalidOperationException("Installed skeleton bindposes are inconsistent.");
                }
                CompletePlayerFingerMapping(animator, visual, outfit, targets, ref bindposes);

                Transform previous = visual.Find(k_ModularRoot);
                if (previous != null)
                {
                    Object.DestroyImmediate(previous.gameObject);
                }
                var root = new GameObject(k_ModularRoot);
                root.transform.SetParent(visual, false);
                var parts = new List<ImportedPart>();
                foreach (ModelRecord model in body.Models)
                {
                    bool isFemale = model.Name.Contains("_F_");
                    var sources = BuildSourceBones(new ExportDocument { Models = new[] { model } });
                    for (int index = 0; index < model.Meshes.Length; index++)
                    {
                        MeshRecord mesh = model.Meshes[index];
                        string materialName = model.Materials[mesh.Material].Name;
                        if (materialName.Contains("hairy") || materialName.Contains("Special"))
                        {
                            continue;
                        }
                        int mask = ParseDrawMask(materialName);
                        bool isFace = model.Name.StartsWith("FG_");
                        if ((isFace && (mask > 2 || index == model.Meshes.Length - 1)) ||
                            (!isFace && model.Name.StartsWith("FC_") && mask == 0))
                        {
                            continue;
                        }
                        var meshObject = new GameObject(model.Name + "_" + index);
                        meshObject.transform.SetParent(root.transform, false);
                        var renderer = meshObject.AddComponent<SkinnedMeshRenderer>();
                        ImportMesh(model, mesh, sources, targets, renderer, index, k_BaseOutput);
                        // Reuse the original A-pose bind matrices; the live Humanoid is already in its T-pose.
                        renderer.sharedMesh.bindposes = bindposes;
                        EditorUtility.SetDirty(renderer.sharedMesh);
                        renderer.rootBone = rootBone;
                        renderer.quality = SkinQuality.Bone4;
                        renderer.localBounds = new Bounds(Vector3.up * 0.9f, new Vector3(3f, 2.4f, 2f));
                        renderer.sharedMaterial = BuildBodyMaterial(model, isFemale);
                        parts.Add(new ImportedPart(renderer, -1, isFace ? 0 : isFemale ? 2 : 1,
                            isFace ? -1 : mask));
                    }
                }

                foreach (ModelRecord model in outfit.Models.Where(model => model.Meshes.Length > 0))
                {
                    int itemId = model.Name.StartsWith("HD_") ? 4 :
                        model.Name.StartsWith("BD_") ? 5 : model.Name.StartsWith("AM_") ? 6 : 7;
                    Material[] materials = BuildMaterials(model, k_Output);
                    var sources = BuildSourceBones(outfit);
                    for (int index = 0; index < model.Meshes.Length; index++)
                    {
                        MeshRecord mesh = model.Meshes[index];
                        var meshObject = new GameObject(model.Name + "_" + index);
                        meshObject.transform.SetParent(root.transform, false);
                        var renderer = meshObject.AddComponent<SkinnedMeshRenderer>();
                        ImportMesh(model, mesh, sources, targets, renderer, index, k_BaseOutput);
                        renderer.sharedMesh.bindposes = bindposes;
                        EditorUtility.SetDirty(renderer.sharedMesh);
                        renderer.rootBone = rootBone;
                        renderer.sharedMaterial = materials[mesh.Material];
                        renderer.quality = SkinQuality.Bone4;
                        renderer.localBounds = armorBounds;
                        parts.Add(new ImportedPart(renderer, itemId, 0,
                            ParseDrawMask(model.Materials[mesh.Material].Name)));
                    }
                }

                int removedRenderers = 0;
                foreach (SkinnedMeshRenderer renderer in player.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (!renderer.transform.IsChildOf(root.transform))
                    {
                        Object.DestroyImmediate(renderer);
                        removedRenderers++;
                    }
                }
                foreach (LODGroup group in player.GetComponentsInChildren<LODGroup>(true))
                {
                    if (!group.transform.IsChildOf(root.transform))
                    {
                        Object.DestroyImmediate(group);
                    }
                }
                PlayerSkinnedEquipment skin = player.GetComponent<PlayerSkinnedEquipment>() ??
                    player.AddComponent<PlayerSkinnedEquipment>();
                var serialized = new SerializedObject(skin);
                SerializedProperty entries = serialized.FindProperty("m_parts");
                entries.arraySize = parts.Count;
                for (int index = 0; index < parts.Count; index++)
                {
                    ImportedPart part = parts[index];
                    SerializedProperty entry = entries.GetArrayElementAtIndex(index);
                    entry.FindPropertyRelative("Renderer").objectReferenceValue = part.Renderer;
                    entry.FindPropertyRelative("ItemId").intValue = part.ItemId;
                    entry.FindPropertyRelative("BodyType").intValue = part.BodyType;
                    entry.FindPropertyRelative("Mask").intValue = part.Mask;
                }
                SerializedProperty armorMasks = serialized.FindProperty("m_armorMasks");
                armorMasks.arraySize = masks.ArmorMasks.Length;
                for (int index = 0; index < masks.ArmorMasks.Length; index++)
                {
                    SerializedProperty entry = armorMasks.GetArrayElementAtIndex(index);
                    entry.FindPropertyRelative("ItemId").intValue = masks.ArmorMasks[index].ItemId;
                    SerializedProperty hidden = entry.FindPropertyRelative("HiddenMasks");
                    hidden.arraySize = masks.ArmorMasks[index].HiddenMasks.Length;
                    for (int maskIndex = 0; maskIndex < hidden.arraySize; maskIndex++)
                    {
                        hidden.GetArrayElementAtIndex(maskIndex).intValue = masks.ArmorMasks[index].HiddenMasks[maskIndex];
                    }
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
                skin.Refresh(true, 4, 5, 6, 7);
                PrefabUtility.SaveAsPrefabAsset(player, k_Player);
                AssetDatabase.SaveAssets();
                report.AppendLine("Parts=" + parts.Count + "; Skeleton=" + animator.avatar.name);
                report.AppendLine("Removed obsolete player skin renderer components=" + removedRenderers);
                report.AppendLine("Armor item IDs=4,5,6,7; body masks read from regulation.bin rows 60000..60300.");
                Directory.CreateDirectory("Docs/ModularPlayer");
                File.WriteAllText("Docs/ModularPlayer/import.txt", report.ToString());
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(player);
            }
        }

        /// <summary>Captures the extracted body and outfit after sampling the existing combat clips.</summary>
        [MenuItem("Tools/ZZ/Elden Ring/Verify Modular Player Animation")]
        public static void VerifyModularPlayerAnimation()
        {
            GameObject player = PrefabUtility.LoadPrefabContents(k_Player);
            var report = new StringBuilder();
            try
            {
                Animator animator = player.GetComponentInChildren<Animator>(true);
                animator.fireEvents = false;
                animator.Rebind();
                PlayerSkinnedEquipment skin = player.GetComponent<PlayerSkinnedEquipment>();
                AnimationMode.StartAnimationMode();
                foreach (bool isMale in new[] { true, false })
                {
                    foreach (bool isEquipped in new[] { false, true })
                    {
                        skin.Refresh(isMale, isEquipped ? 4 : -1, isEquipped ? 5 : -1,
                            isEquipped ? 6 : -1, isEquipped ? 7 : -1);
                        var renderers = player.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                            .Where(renderer => renderer.enabled).ToArray();
                        foreach (string name in new[] { "core_main_idle_01", "straight_sword_main_light_attack_01",
                            "core_main_run_FR_01", "core_main_roll_med_to_idle_F_01" })
                        {
                            string clipPath = Directory.GetFiles(
                                "Assets/_Game/Art/Characters/Shared/Humanoid/Animations", name + ".anim",
                                SearchOption.AllDirectories).Single().Replace('\\', '/');
                            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                            float maximumDisplacement = 0f;
                            Vector3[][] previous = null;
                            foreach (float phase in new[] { 0.1f, 0.45f, 0.8f })
                            {
                                AnimationMode.BeginSampling();
                                AnimationMode.SampleAnimationClip(animator.gameObject, clip, clip.length * phase);
                                AnimationMode.EndSampling();
                                Vector3[][] current = renderers.Select(BakeVertices).ToArray();
                                for (int index = 0; index < current.Length; index++)
                                {
                                    if (current[index].Any(vertex => float.IsNaN(vertex.sqrMagnitude) ||
                                        float.IsInfinity(vertex.sqrMagnitude) || vertex.magnitude > 5f))
                                    {
                                        throw new InvalidOperationException("Invalid animated skin: " + renderers[index].name);
                                    }
                                    if (previous != null)
                                    {
                                        maximumDisplacement = Mathf.Max(maximumDisplacement,
                                            previous[index].Zip(current[index], Vector3.Distance).Max());
                                    }
                                }
                                previous = current;
                            }
                            if (maximumDisplacement < 0.00001f)
                            {
                                throw new InvalidOperationException("Animation did not deform modular skin: " + name);
                            }
                            report.AppendLine($"PASS Male={isMale}; Equipped={isEquipped}; {name}; delta={maximumDisplacement:F4}m");
                        }
                        string idlePath = Directory.GetFiles(
                            "Assets/_Game/Art/Characters/Shared/Humanoid/Animations", "core_main_idle_01.anim",
                            SearchOption.AllDirectories).Single().Replace('\\', '/');
                        AnimationMode.BeginSampling();
                        AnimationMode.SampleAnimationClip(animator.gameObject,
                            AssetDatabase.LoadAssetAtPath<AnimationClip>(idlePath), 0.2f);
                        AnimationMode.EndSampling();
                        string filename = $"modular-{(isMale ? "male" : "female")}-{(isEquipped ? "armor" : "body")}.png";
                        Capture(player, filename, k_ModularRoot);
                        File.Copy(Path.GetDirectoryName(k_Report) + "/" + filename,
                            "Docs/ModularPlayer/" + filename, true);
                    }
                }
                File.WriteAllText("Docs/ModularPlayer/animation-verification.txt", report.ToString());
            }
            finally
            {
                AnimationMode.StopAnimationMode();
                PrefabUtility.UnloadPrefabContents(player);
            }
        }

        private static int ParseDrawMask(string materialName)
        {
            string[] tokens = materialName.Split('#');
            return tokens.Length > 1 && int.TryParse(tokens[1], out int mask) ? mask : -1;
        }

        /// <summary>Checks replicated equipment callbacks in an unsaved host session and restores every tested value.</summary>
        public static void VerifyRuntimeModularEquipment(PlayerManager player)
        {
            if (!Application.isPlaying || WorldSaveGameManager.Instance?.CurrentCharacterSlot != CharacterSlot.NoSlot ||
                player == null || !player.IsOwner || !player.IsSpawned)
            {
                throw new InvalidOperationException("Use an unsaved spawned host player for equipment verification.");
            }
            PlayerNetworkManager network = player.PlayerNetworkManager;
            int head = network.CurrentHeadEquipmentID.Value;
            int body = network.CurrentBodyEquipmentID.Value;
            int hand = network.CurrentHandEquipmentID.Value;
            int leg = network.CurrentLegEquipmentID.Value;
            bool isMale = network.IsMale.Value;
            var report = new StringBuilder();
            try
            {
                foreach (bool male in new[] { true, false })
                {
                    network.IsMale.Value = male;
                    network.CurrentHeadEquipmentID.Value = -1;
                    network.CurrentBodyEquipmentID.Value = -1;
                    network.CurrentHandEquipmentID.Value = -1;
                    network.CurrentLegEquipmentID.Value = -1;
                    var parts = player.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                        .Where(renderer => renderer.transform.parent.name == k_ModularRoot).ToArray();
                    if (parts.Length < 88 || parts.Any(renderer => renderer.enabled &&
                        (renderer.name.Contains("_1100_") || renderer.name.Contains(male ? "_F_" : "_M_"))))
                    {
                        throw new InvalidOperationException("Replicated body type or armor removal did not update the skin.");
                    }
                    network.CurrentHeadEquipmentID.Value = 4;
                    network.CurrentBodyEquipmentID.Value = 5;
                    network.CurrentHandEquipmentID.Value = 6;
                    network.CurrentLegEquipmentID.Value = 7;
                    foreach (string prefix in new[] { "HD_M_1100", "BD_M_1100", "AM_M_1100", "LG_M_1100" })
                    {
                        if (!parts.Any(renderer => renderer.enabled && renderer.name.StartsWith(prefix)))
                        {
                            throw new InvalidOperationException("Replicated equipment did not enable " + prefix);
                        }
                    }
                    report.AppendLine($"PASS Spawned owner; Male={male}; network callbacks equip/unequip four armor slots.");
                }
                File.WriteAllText("Docs/ModularPlayer/runtime-equipment.txt", report.ToString());
            }
            finally
            {
                network.IsMale.Value = isMale;
                network.CurrentHeadEquipmentID.Value = head;
                network.CurrentBodyEquipmentID.Value = body;
                network.CurrentHandEquipmentID.Value = hand;
                network.CurrentLegEquipmentID.Value = leg;
            }
        }

        private static Material BuildBodyMaterial(ModelRecord model, bool isFemale)
        {
            string textureName = model.Name.StartsWith("LG_") ? "LG_" :
                model.Name.StartsWith("BD_") ? "BD_" : "FC_";
            textureName = model.Name.StartsWith("FG_") ? "FG_A_0100_a" :
                textureName + (isFemale ? "F_0000_a" : "M_0000_a");
            string path = k_BaseOutput + "/Materials/" + textureName + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                return material;
            }
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(k_BaseOutput + "/Textures/" + textureName + ".png");
            if (texture == null)
            {
                throw new InvalidOperationException("Missing original body texture: " + textureName);
            }
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.name = textureName;
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", 0.25f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private readonly struct ImportedPart
        {
            public readonly SkinnedMeshRenderer Renderer;
            public readonly int ItemId;
            public readonly int BodyType;
            public readonly int Mask;

            public ImportedPart(SkinnedMeshRenderer renderer, int itemId, int bodyType, int mask)
            {
                Renderer = renderer;
                ItemId = itemId;
                BodyType = bodyType;
                Mask = mask;
            }
        }

        [Serializable] private sealed class MaskDocument { public MaskRecord[] ArmorMasks; }
        [Serializable] private sealed class MaskRecord { public int ItemId; public int[] HiddenMasks; }
    }
}
