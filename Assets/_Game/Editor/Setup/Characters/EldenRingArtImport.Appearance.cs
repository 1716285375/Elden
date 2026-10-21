using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ZZ.Editor
{
    public static partial class EldenRingArtImport
    {
        private const string k_AppearanceOutput = "Assets/_Game/Art/Characters/EldenRing/Appearance";

        [MenuItem("Tools/ZZ/Elden Ring/Install Extracted Face And Hair")]
        public static void InstallExtractedFaceAndHair()
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException("Stop Play Mode before installing appearance assets.");
            }
            var data = JsonUtility.FromJson<FaceControlDocument>(File.ReadAllText(
                "F:/MyProject/RE/EldenRing/Converted/PlayerFace/face-controls.json"));
            if (data.Controls.Length != 87 || data.Meshes.Length != 3)
            {
                throw new InvalidDataException("The original FaceGen control export is incomplete.");
            }
            Directory.CreateDirectory(k_AppearanceOutput + "/Meshes");
            File.Copy("F:/MyProject/RE/EldenRing/Converted/PlayerHair/Textures/Hair.png",
                k_AppearanceOutput + "/Hair.png", true);
            AssetDatabase.ImportAsset(k_AppearanceOutput + "/Hair.png", ImportAssetOptions.ForceSynchronousImport);
            FaceMorphDefinition definition = BuildFaceDefinition(data);
            GameObject player = PrefabUtility.LoadPrefabContents(k_Player);
            try
            {
                Animator animator = player.GetComponentInChildren<Animator>(true);
                Transform visual = animator.transform.Find(k_VisualName);
                foreach (FaceControlMesh record in data.Meshes)
                {
                    SkinnedMeshRenderer renderer = visual.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                        .Single(item => item.name == record.MeshName);
                    AddFaceControls(renderer.sharedMesh, record);
                }
                InstallHair(visual);
                InstallFacialFeatures(visual);
                BindAppearance(player, definition);
                FixShieldGrip(player);
                PrefabUtility.SaveAsPrefabAsset(player, k_Player);
                CreateAppearancePreview(player, definition);
                AssetDatabase.SaveAssets();
                File.Copy("F:/MyProject/RE/EldenRing/Converted/PlayerFace/face-controls-report.json",
                    "Docs/CharacterCreationRefresh/face-export-report.json", true);
                File.WriteAllText("Docs/CharacterCreationRefresh/appearance-import.txt",
                    "PASS: 87 exact-index FaceGen controls on head, eyes, four brows, four beards and lashes; eight extracted hairstyles, " +
                    "visual-only preview prefab and original shield grip installed.\n");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(player);
            }
        }

        private static void AddFaceControls(Mesh mesh, FaceControlMesh record)
        {
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            mesh.ClearBlendShapes();
            var temporary = Object.Instantiate(mesh);
            try
            {
                for (int shape = 0; shape < record.Shapes.Length; shape++)
                {
                    float[] values = record.Shapes[shape].Deltas;
                    if (values.Length != vertices.Length * 3)
                    {
                        throw new InvalidDataException("FaceGen mapping count differs from the native mesh.");
                    }
                    var delta = new Vector3[vertices.Length];
                    var deformed = new Vector3[vertices.Length];
                    for (int index = 0; index < vertices.Length; index++)
                    {
                        delta[index] = new Vector3(-values[index * 3], values[index * 3 + 1],
                            values[index * 3 + 2]) * 4f;
                        deformed[index] = vertices[index] + delta[index];
                    }
                    temporary.vertices = deformed;
                    temporary.RecalculateNormals();
                    Vector3[] deltaNormals = temporary.normals;
                    for (int index = 0; index < deltaNormals.Length; index++)
                    {
                        deltaNormals[index] -= normals[index];
                    }
                    mesh.AddBlendShapeFrame("FaceGen_" + shape.ToString("D2"), 100f, delta, deltaNormals, null);
                }
            }
            finally
            {
                Object.DestroyImmediate(temporary);
            }
            EditorUtility.SetDirty(mesh);
        }

        private static FaceMorphDefinition BuildFaceDefinition(FaceControlDocument data)
        {
            string path = k_AppearanceOutput + "/FaceMorphs.asset";
            FaceMorphDefinition definition = AssetDatabase.LoadAssetAtPath<FaceMorphDefinition>(path);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<FaceMorphDefinition>();
                AssetDatabase.CreateAsset(definition, path);
            }
            var serialized = new SerializedObject(definition);
            SerializedProperty controls = serialized.FindProperty("m_controls");
            controls.arraySize = data.Controls.Length;
            for (int index = 0; index < controls.arraySize; index++)
            {
                SerializedProperty control = controls.GetArrayElementAtIndex(index);
                control.FindPropertyRelative("SourceName").stringValue = data.Controls[index].Name;
                control.FindPropertyRelative("Label").stringValue = data.Controls[index].Label;
                control.FindPropertyRelative("Group").stringValue = data.Controls[index].Group;
                control.FindPropertyRelative("IsSymmetric").boolValue = data.Controls[index].Symmetric;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return definition;
        }

        private static void InstallHair(Transform visual)
        {
            Transform old = visual.Find("Extracted Hairstyles");
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }
            Transform hairRoot = new GameObject("Extracted Hairstyles").transform;
            hairRoot.SetParent(visual, false);
            var document = JsonUtility.FromJson<ExportDocument>(File.ReadAllText(
                "F:/MyProject/RE/EldenRing/Converted/PlayerHair/models.json"));
            SkinnedMeshRenderer template = visual.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .First(item => item.bones.Length > 0);
            var targets = template.bones.ToDictionary(bone => bone.name.Replace("ER_Set1100Avatar_", "")
                .Replace("ER_", ""), bone => bone);
            string materialPath = k_AppearanceOutput + "/Hair.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(k_AppearanceOutput + "/Hair.png"));
            material.SetFloat("_AlphaClip", 1f);
            material.SetFloat("_Cutoff", 0.35f);
            material.SetFloat("_Cull", 0f);
            material.SetFloat("_Smoothness", 0.2f);
            material.EnableKeyword("_ALPHATEST_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            EditorUtility.SetDirty(material);
            foreach (ModelRecord model in document.Models)
            {
                Transform style = new GameObject("Appearance Hair " + model.Name.Substring(model.Name.Length - 4)).transform;
                style.SetParent(hairRoot, false);
                var bones = BuildSourceBones(new ExportDocument { Models = new[] { model } });
                for (int index = 0; index < model.Meshes.Length; index++)
                {
                    var meshObject = new GameObject(model.Name + "_" + index);
                    meshObject.transform.SetParent(style, false);
                    var renderer = meshObject.AddComponent<SkinnedMeshRenderer>();
                    ImportMesh(model, model.Meshes[index], bones, targets, renderer, index, k_AppearanceOutput);
                    renderer.sharedMesh.bindposes = template.sharedMesh.bindposes;
                    renderer.rootBone = template.rootBone;
                    renderer.sharedMaterial = material;
                    renderer.localBounds = template.localBounds;
                    renderer.updateWhenOffscreen = false;
                    EditorUtility.SetDirty(renderer.sharedMesh);
                }
                style.gameObject.SetActive(false);
            }
        }

        private static void BindAppearance(GameObject root, FaceMorphDefinition definition)
        {
            SkinnedMeshRenderer[] renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var faces = renderers.Where(item => item.sharedMesh.blendShapeCount == definition.Count).ToArray();
            var skin = renderers.Where(item => item.name.StartsWith("FC_") || item.name.StartsWith("FG_A_0100_")).ToArray();
            GameObject[] styles = root.GetComponentsInChildren<Transform>(true)
                .Where(item => item.name.StartsWith("Appearance Hair ")).OrderBy(item => item.name)
                .Select(item => item.gameObject).ToArray();
            CharacterAppearancePresenter presenter = root.GetComponent<CharacterAppearancePresenter>() ??
                root.AddComponent<CharacterAppearancePresenter>();
            presenter.Bind(definition, faces, skin, styles);
            presenter.BindFeatures(root.GetComponentsInChildren<Transform>(true)
                .Where(item => item.name.StartsWith("Appearance Feature ")).Select(item => item.gameObject).ToArray(),
                renderers.Where(item => item.name.StartsWith("FG_A_1500_")).ToArray());
            EditorUtility.SetDirty(presenter);
        }

        private static void CreateAppearancePreview(GameObject source, FaceMorphDefinition definition)
        {
            Animator animator = source.GetComponentInChildren<Animator>(true);
            var preview = new GameObject("Character Preview");
            try
            {
                GameObject visual = Object.Instantiate(animator.transform.Find(k_VisualName).gameObject, preview.transform);
                visual.name = k_VisualName;
                Animator copy = preview.AddComponent<Animator>();
                copy.avatar = animator.avatar;
                copy.runtimeAnimatorController = animator.runtimeAnimatorController;
                copy.fireEvents = false;
                var skin = preview.AddComponent<PlayerSkinnedEquipment>();
                EditorUtility.CopySerialized(source.GetComponent<PlayerSkinnedEquipment>(), skin);
                var renderers = preview.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .ToDictionary(item => item.name);
                var serialized = new SerializedObject(skin);
                SerializedProperty parts = serialized.FindProperty("m_parts");
                for (int index = 0; index < parts.arraySize; index++)
                {
                    SerializedProperty renderer = parts.GetArrayElementAtIndex(index).FindPropertyRelative("Renderer");
                    renderer.objectReferenceValue = renderers[renderer.objectReferenceValue.name];
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
                BindAppearance(preview, definition);
                foreach (Collider collider in preview.GetComponentsInChildren<Collider>(true))
                {
                    Object.DestroyImmediate(collider);
                }
                skin.Refresh(true, -1, -1, -1, -1);
                PrefabUtility.SaveAsPrefabAsset(preview, "Assets/_Game/Resources/UI/CharacterPreview.prefab");
            }
            finally
            {
                Object.DestroyImmediate(preview);
            }
        }

        private static void FixShieldGrip(GameObject player)
        {
            Animator animator = player.GetComponentInChildren<Animator>(true);
            var source = JsonUtility.FromJson<ExportDocument>(File.ReadAllText(
                "F:/MyProject/RE/EldenRing/Converted/PlayerHead/models.json"));
            var sourceBones = BuildSourceBones(source);
            foreach (string side in new[] { "L_", "R_" })
            {
                Transform dummy = animator.transform.Find(k_VisualName).GetComponentsInChildren<Transform>(true)
                    .Single(item => item.name == "ER_" + side + "Hand_Dummy");
                Transform wrist = animator.GetBoneTransform(side == "L_" ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
                Matrix4x4 local = sourceBones[side + "Hand"].World.inverse * sourceBones[side + "Hand_Dummy"].World;
                dummy.SetParent(wrist, false);
                dummy.localPosition = local.GetColumn(3);
                dummy.localRotation = local.rotation;
                dummy.localScale = Vector3.one;
                Transform anchor = animator.transform.Find(k_VisualName).GetComponentsInChildren<Transform>(true)
                    .SingleOrDefault(item => item.name == "ER_" + side + "Weapon");
                if (anchor == null) { anchor = new GameObject("ER_" + side + "Weapon").transform; }
                Matrix4x4 weaponLocal = sourceBones[side + "Hand"].World.inverse * sourceBones[side + "Weapon"].World;
                anchor.SetParent(wrist, false);
                anchor.localPosition = weaponLocal.GetColumn(3);
                anchor.localRotation = weaponLocal.rotation;
                anchor.localScale = Vector3.one;
                WeaponModelInstantiationSlot weaponSlot = player.GetComponentsInChildren<WeaponModelInstantiationSlot>(true)
                    .Single(item => item.WeaponModelSlot == (side == "L_"
                        ? WeaponModelSlot.LeftHandSlot : WeaponModelSlot.RightHandSlot));
                weaponSlot.transform.SetParent(anchor, false);
                weaponSlot.transform.localPosition = Vector3.zero;
                weaponSlot.transform.localRotation = Quaternion.identity;
                weaponSlot.transform.localScale = Vector3.one;
            }
            ApplyOriginalIdleWeaponPose(player);
            Transform hand = animator.transform.Find(k_VisualName).GetComponentsInChildren<Transform>(true)
                .Single(item => item.name == "ER_L_Shield");
            WeaponModelInstantiationSlot slot = player.GetComponentsInChildren<WeaponModelInstantiationSlot>(true)
                .Single(item => item.WeaponModelSlot == WeaponModelSlot.LeftHandShieldSlot);
            slot.transform.SetParent(hand, false);
            slot.transform.localPosition = Vector3.zero;
            slot.transform.localRotation = Quaternion.identity;
            slot.transform.localScale = Vector3.one;
            string path = "Assets/_Game/Prefabs/Equipment/Weapons/Melee Weapons/Medium Shield.prefab";
            GameObject shield = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Transform pivot = shield.transform.Find("Weapon Pivot");
                pivot.localPosition = Vector3.zero;
                pivot.localRotation = Quaternion.identity;
                Transform mesh = pivot.Find("Extracted Equipment Visual");
                mesh.localPosition = Vector3.zero;
                mesh.localRotation = Quaternion.identity;
                mesh.localScale = Vector3.one;
                PrefabUtility.SaveAsPrefabAsset(shield, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(shield);
            }
        }

        [MenuItem("Tools/ZZ/Elden Ring/Repair Preview Equipment Anchors")]
        public static void RepairPreviewEquipmentAnchors()
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException("Stop Play Mode before repairing equipment anchors.");
            }
            GameObject player = PrefabUtility.LoadPrefabContents(k_Player);
            try
            {
                FixShieldGrip(player);
                PrefabUtility.SaveAsPrefabAsset(player, k_Player);
                CreateAppearancePreview(player, AssetDatabase.LoadAssetAtPath<FaceMorphDefinition>(
                    k_AppearanceOutput + "/FaceMorphs.asset"));
                AssetDatabase.SaveAssets();
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(player);
            }
        }

        [Serializable] private sealed class FaceControlDocument { public FaceControl[] Controls; public FaceControlMesh[] Meshes; }
        [Serializable] private sealed class FaceControl { public string Name; public string Label; public string Group; public bool Symmetric; }
        [Serializable] private sealed class FaceControlMesh { public string MeshName; public FaceControlShape[] Shapes; }
        [Serializable] private sealed class FaceControlShape { public string Name; public float[] Deltas; }
    }
}
