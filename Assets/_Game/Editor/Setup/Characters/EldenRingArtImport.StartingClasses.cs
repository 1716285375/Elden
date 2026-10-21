using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ZZ.Editor
{
    public static partial class EldenRingArtImport
    {
        private const string k_StartingSource = "F:/MyProject/RE/EldenRing/Converted/StartingClasses";
        private const string k_StartingOutput = "Assets/_Game/Art/Characters/EldenRing/StartingClasses";

        [MenuItem("Tools/ZZ/Elden Ring/Repair Face Visibility")]
        public static void RepairFaceVisibility()
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException("Stop Play Mode before repairing body masks.");
            }
            foreach (string path in new[] { k_Player, "Assets/_Game/Resources/UI/CharacterPreview.prefab" })
            {
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    PlayerSkinnedEquipment skin = root.GetComponent<PlayerSkinnedEquipment>();
                    var serialized = new SerializedObject(skin);
                    SerializedProperty parts = serialized.FindProperty("m_parts");
                    for (int index = 0; index < parts.arraySize; index++)
                    {
                        SerializedProperty entry = parts.GetArrayElementAtIndex(index);
                        Object renderer = entry.FindPropertyRelative("Renderer").objectReferenceValue;
                        if (renderer != null && renderer.name.StartsWith("FG_A_0100_", StringComparison.Ordinal))
                        {
                            entry.FindPropertyRelative("Mask").intValue = -1;
                        }
                    }
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    skin.Refresh(true, -1, -1, -1, -1);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            RepairStartingCatalog();
        }

        [MenuItem("Tools/ZZ/Elden Ring/Repair Starting Catalog")]
        public static void RepairStartingCatalog()
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException("Stop Play Mode before changing the item catalog.");
            }
            InstallStartingItems(JsonUtility.FromJson<StartingDocument>(File.ReadAllText(k_StartingSource + "/classes.json")));
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tools/ZZ/Elden Ring/Bake Origin Portraits")]
        public static void BakeOriginPortraits()
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException("Stop Play Mode before baking portraits.");
            }
            TitleScreenManager title = Object.FindFirstObjectByType<TitleScreenManager>();
            GameObject player = PrefabUtility.LoadPrefabContents(k_Player);
            Directory.CreateDirectory("Assets/_Game/Resources/UI/Origins");
            Directory.CreateDirectory(k_StartingSource + "/Portraits");
            try
            {
                Animator animator = player.GetComponentInChildren<Animator>(true);
                animator.fireEvents = false;
                string clipPath = "Assets/_Game/Art/Characters/EldenRing/PlayerActions/Humanoid/Clips/a000_000000.anim";
                AnimationMode.StartAnimationMode();
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(animator.gameObject,
                    AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath), 0.2f);
                AnimationMode.EndSampling();
                PlayerSkinnedEquipment skin = player.GetComponent<PlayerSkinnedEquipment>();
                foreach (CharacterClass origin in title.StartingClasses)
                {
                    foreach (bool male in new[] { true, false })
                    {
                        skin.Refresh(male, origin.HeadEquipment?.ItemID ?? -1, origin.BodyEquipment?.ItemID ?? -1,
                            origin.HandEquipment?.ItemID ?? -1, origin.LegEquipment?.ItemID ?? -1);
                        string file = "Origin" + origin.OriginId + (male ? "A" : "B") + ".png";
                        Capture(player, file, k_ModularRoot, 448, 880);
                        string source = Path.GetDirectoryName(k_Report) + "/" + file;
                        File.Copy(source, k_StartingSource + "/Portraits/" + file, true);
                        File.Copy(source, "Assets/_Game/Resources/UI/Origins/" + file, true);
                    }
                }
            }
            finally
            {
                AnimationMode.StopAnimationMode();
                PrefabUtility.UnloadPrefabContents(player);
            }
            AssetDatabase.Refresh();
            File.WriteAllText("Docs/StartingClasses/portraits.txt", "BAKED: 10 origins x 2 body types, 448x880.\n");
        }

        [MenuItem("Tools/ZZ/Elden Ring/Install Original Starting Classes")]
        public static void InstallOriginalStartingClasses()
        {
            if (Application.isPlaying || EditorSceneManager.GetSceneManagerSetup().Any(entry => entry.isLoaded &&
                UnityEngine.SceneManagement.SceneManager.GetSceneByPath(entry.path).isDirty))
            {
                throw new InvalidOperationException("Stop Play Mode and save open scenes before importing classes.");
            }
            var data = JsonUtility.FromJson<StartingDocument>(File.ReadAllText(k_StartingSource + "/classes.json"));
            if (data.Classes.Length != 10 || data.Armor.Length != 34)
            {
                throw new InvalidDataException("Original starting class export is incomplete.");
            }
            foreach (string name in new[] { "Meshes", "Materials", "Textures", "Items" })
            {
                Directory.CreateDirectory(k_StartingOutput + "/" + name);
            }
            foreach (string path in Directory.GetFiles(k_StartingSource + "/Textures", "*.png"))
            {
                File.Copy(path, k_StartingOutput + "/Textures/" + Path.GetFileName(path), true);
            }
            AssetDatabase.Refresh();
            ConfigureTextures(k_StartingOutput);
            var items = InstallStartingItems(data);
            GameObject player = PrefabUtility.LoadPrefabContents(k_Player);
            try
            {
                InstallStartingMeshes(player, data, items);
                player.GetComponent<PlayerSkinnedEquipment>().Refresh(true, -1, -1, -1, -1);
                PrefabUtility.SaveAsPrefabAsset(player, k_Player);
                CreateAppearancePreview(player, player.GetComponent<CharacterAppearancePresenter>().Definition);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(player);
            }
            var scene = EditorSceneManager.OpenScene(WorldScenePathLayout.MainMenuScenePath);
            TitleScreenManager title = scene.GetRootGameObjects().SelectMany(root =>
                root.GetComponentsInChildren<TitleScreenManager>(true)).Single();
            var serialized = new SerializedObject(title);
            SerializedProperty classes = serialized.FindProperty("m_startingClasses");
            classes.arraySize = data.Classes.Length;
            GameObject databasePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Prefabs/World/Managers/World Item Database.prefab");
            var database = new SerializedObject(databasePrefab.GetComponent<WorldItemDatabase>());
            SerializedProperty catalog = database.FindProperty("m_items");
            for (int index = 0; index < data.Classes.Length; index++)
            {
                StartingRecord source = data.Classes[index];
                SerializedProperty entry = classes.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("m_className").stringValue = source.Name;
                entry.FindPropertyRelative("m_level").intValue = source.Level;
                entry.FindPropertyRelative("m_originId").intValue = source.Id;
                string[] attributes = { "m_vitality", "m_mind", "m_endurance", "m_strength",
                    "m_dexterity", "m_intelligence", "m_faith", "m_arcane" };
                for (int stat = 0; stat < attributes.Length; stat++)
                {
                    entry.FindPropertyRelative(attributes[stat]).intValue = source.Stats[stat];
                }
                string[] armorFields = { "m_headEquipment", "m_bodyEquipment", "m_handEquipment", "m_legEquipment" };
                for (int slot = 0; slot < armorFields.Length; slot++)
                {
                    entry.FindPropertyRelative(armorFields[slot]).objectReferenceValue =
                        source.Armor[slot] >= 0 ? items[source.Armor[slot]] : null;
                }
                // Existing weapon behaviours are retained until their original weapon-specific motions are calibrated.
                SetStartingSlots(entry.FindPropertyRelative("m_rightHandWeapons"), catalog, index == 9 ? 0 : 1);
                SetStartingSlots(entry.FindPropertyRelative("m_leftHandWeapons"), catalog, index == 9 ? 0 : 3);
                SerializedProperty quick = entry.FindPropertyRelative("m_quickSlotItems");
                quick.arraySize = 3;
                quick.GetArrayElementAtIndex(0).objectReferenceValue = catalog.GetArrayElementAtIndex(14).objectReferenceValue;
                quick.GetArrayElementAtIndex(1).objectReferenceValue = catalog.GetArrayElementAtIndex(15).objectReferenceValue;
                quick.GetArrayElementAtIndex(2).objectReferenceValue = null;
                SerializedProperty amounts = entry.FindPropertyRelative("m_quickSlotItemAmounts");
                amounts.arraySize = 3;
                for (int slot = 0; slot < 3; slot++)
                {
                    amounts.GetArrayElementAtIndex(slot).intValue = slot == 0 ? 3 : slot == 1 ? 1 : 0;
                }
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Docs/StartingClasses");
            File.WriteAllText("Docs/StartingClasses/import.txt", "IMPORTED: 10 origins; 34 original armor items; " +
                "40 gendered model records; preview and gameplay prefab start without armor.\n" +
                "LIMIT: weapon-specific loadouts and original skill behaviours remain under calibration.\n");
        }

        private static Dictionary<int, ArmorItem> InstallStartingItems(StartingDocument data)
        {
            var result = new Dictionary<int, ArmorItem>();
            var icons = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/_Game/Art/UI/EldenRing/Icons" })
                .SelectMany(guid => AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)).OfType<Sprite>())
                .GroupBy(sprite => sprite.name).ToDictionary(group => group.Key, group => group.First());
            const string path = "Assets/_Game/Prefabs/World/Managers/World Item Database.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                WorldItemDatabase manager = root.GetComponent<WorldItemDatabase>();
                var serialized = new SerializedObject(manager);
                SerializedProperty catalog = serialized.FindProperty("m_items");
                var names = File.ReadAllLines(k_StartingSource + "/armor-names.txt")
                    .Where(line => line.Contains(' ')).Select(line => line.Split(new[] { ' ' }, 2))
                    .Where(pair => int.TryParse(pair[0], out _)).GroupBy(pair => int.Parse(pair[0]))
                    .ToDictionary(group => group.Key, group => group.First()[1]);
                foreach (StartingArmor record in data.Armor)
                {
                    int slot = record.Id % 1000 / 100;
                    Type type = slot == 0 ? typeof(HeadEquipmentItem) : slot == 1 ? typeof(BodyEquipmentItem) :
                        slot == 2 ? typeof(HandEquipmentItem) : typeof(LegEquipmentItem);
                    string assetPath = k_StartingOutput + "/Items/ER_" + record.Id + ".asset";
                    ArmorItem item = AssetDatabase.LoadAssetAtPath<ArmorItem>(assetPath);
                    if (item == null)
                    {
                        item = (ArmorItem)ScriptableObject.CreateInstance(type);
                        AssetDatabase.CreateAsset(item, assetPath);
                    }
                    var fields = new SerializedObject(item);
                    fields.FindProperty("m_itemName").stringValue = names[record.Id];
                    if (icons.TryGetValue("MENU_ItemIcon_" + record.Icon.ToString("D5"), out Sprite icon))
                    {
                        fields.FindProperty("m_itemIcon").objectReferenceValue = icon;
                    }
                    fields.FindProperty("m_equipmentModels").arraySize = 0;
                    fields.FindProperty("m_itemWeight").floatValue = record.Weight;
                    string[] absorption = { "m_physicalAbsorption", "m_magicAbsorption", "m_fireAbsorption",
                        "m_lightningAbsorption", "m_holyAbsorption" };
                    for (int channel = 0; channel < absorption.Length; channel++)
                    {
                        fields.FindProperty(absorption[channel]).floatValue = record.Absorption[channel];
                    }
                    string[] resistance = { "m_immunity", "m_robustness", "m_focus", "m_vitality" };
                    for (int channel = 0; channel < resistance.Length; channel++)
                    {
                        fields.FindProperty(resistance[channel]).floatValue = record.Resistance[channel];
                    }
                    if (item is HeadEquipmentItem)
                    {
                        fields.FindProperty("m_headEquipmentType").enumValueIndex =
                            (int)(record.HiddenMasks.Contains(32) ? HeadEquipmentType.Hood : HeadEquipmentType.Hat);
                    }
                    fields.ApplyModifiedPropertiesWithoutUndo();
                    bool registered = Enumerable.Range(0, catalog.arraySize).Any(index =>
                        catalog.GetArrayElementAtIndex(index).objectReferenceValue == item);
                    if (!registered)
                    {
                        catalog.InsertArrayElementAtIndex(catalog.arraySize);
                        catalog.GetArrayElementAtIndex(catalog.arraySize - 1).objectReferenceValue = item;
                    }
                    SerializedProperty typed = serialized.FindProperty(slot == 0 ? "m_headEquipment" :
                        slot == 1 ? "m_bodyEquipment" : slot == 2 ? "m_handEquipment" : "m_legEquipment");
                    if (!Enumerable.Range(0, typed.arraySize).Any(index =>
                        typed.GetArrayElementAtIndex(index).objectReferenceValue == item))
                    {
                        typed.InsertArrayElementAtIndex(typed.arraySize);
                        typed.GetArrayElementAtIndex(typed.arraySize - 1).objectReferenceValue = item;
                    }
                    result.Add(record.Id, item);
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
                manager.RefreshItemCatalog();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            return result;
        }

        private static void InstallStartingMeshes(GameObject player, StartingDocument data, Dictionary<int, ArmorItem> items)
        {
            PlayerSkinnedEquipment skin = player.GetComponent<PlayerSkinnedEquipment>();
            var serialized = new SerializedObject(skin);
            SerializedProperty parts = serialized.FindProperty("m_parts");
            SerializedProperty masks = serialized.FindProperty("m_armorMasks");
            SkinnedMeshRenderer template = player.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .First(renderer => renderer.name == "FC_M_0100_3");
            Transform parent = template.transform.parent;
            var targets = template.bones.ToDictionary(bone => bone.name.Replace("ER_Set1100Avatar_", "")
                .Replace("ER_", ""), bone => bone);
            ModelRecord[] models = Directory.GetFiles(k_StartingSource, "??_?_*.json")
                .SelectMany(path => JsonUtility.FromJson<ExportDocument>(File.ReadAllText(path)).Models).ToArray();
            foreach (ModelRecord model in models)
            {
                string prefix = model.Name.Substring(0, 2);
                int modelId = int.Parse(model.Name.Substring(5));
                int slot = prefix == "HD" ? 0 : prefix == "BD" ? 1 : prefix == "AM" ? 2 : 3;
                StartingArmor armor = data.Armor.Single(record => record.Model == modelId && record.Id % 1000 / 100 == slot);
                Material[] materials = BuildMaterials(model, k_StartingOutput);
                var sourceBones = BuildSourceBones(new ExportDocument { Models = new[] { model } });
                bool hasFemale = models.Any(other => other.Name == model.Name.Replace("_M_", "_F_"));
                for (int meshIndex = 0; meshIndex < model.Meshes.Length; meshIndex++)
                {
                    string name = model.Name + "_" + meshIndex;
                    Transform child = parent.Find(name);
                    if (child == null)
                    {
                        child = new GameObject(name).transform;
                        child.SetParent(parent, false);
                        child.gameObject.AddComponent<SkinnedMeshRenderer>();
                    }
                    SkinnedMeshRenderer renderer = child.GetComponent<SkinnedMeshRenderer>();
                    ImportMesh(model, model.Meshes[meshIndex], sourceBones, targets, renderer, meshIndex, k_StartingOutput);
                    renderer.sharedMesh.bindposes = template.sharedMesh.bindposes;
                    EditorUtility.SetDirty(renderer.sharedMesh);
                    renderer.rootBone = template.rootBone;
                    renderer.localBounds = new Bounds(Vector3.up, new Vector3(4f, 3f, 3f));
                    renderer.sharedMaterial = materials[model.Meshes[meshIndex].Material];
                    int partIndex = Enumerable.Range(0, parts.arraySize).Where(index =>
                        parts.GetArrayElementAtIndex(index).FindPropertyRelative("Renderer").objectReferenceValue == renderer)
                        .DefaultIfEmpty(-1).First();
                    if (partIndex < 0)
                    {
                        partIndex = parts.arraySize++;
                    }
                    SerializedProperty entry = parts.GetArrayElementAtIndex(partIndex);
                    entry.FindPropertyRelative("Renderer").objectReferenceValue = renderer;
                    entry.FindPropertyRelative("ItemId").intValue = items[armor.Id].ItemID;
                    entry.FindPropertyRelative("BodyType").intValue = model.Name.Contains("_F_") ? 2 : hasFemale ? 1 : 0;
                    entry.FindPropertyRelative("Mask").intValue = ParseDrawMask(model.Materials[model.Meshes[meshIndex].Material].Name);
                }
            }
            masks.arraySize = 4 + data.Armor.Length;
            for (int index = 0; index < data.Armor.Length; index++)
            {
                SerializedProperty entry = masks.GetArrayElementAtIndex(4 + index);
                entry.FindPropertyRelative("ItemId").intValue = items[data.Armor[index].Id].ItemID;
                SerializedProperty hidden = entry.FindPropertyRelative("HiddenMasks");
                hidden.arraySize = data.Armor[index].HiddenMasks.Length;
                for (int mask = 0; mask < hidden.arraySize; mask++)
                {
                    hidden.GetArrayElementAtIndex(mask).intValue = data.Armor[index].HiddenMasks[mask];
                }
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetStartingSlots(SerializedProperty slots, SerializedProperty catalog, int first)
        {
            slots.arraySize = 3;
            for (int index = 0; index < 3; index++)
            {
                slots.GetArrayElementAtIndex(index).objectReferenceValue =
                    catalog.GetArrayElementAtIndex(index == 0 ? first : 0).objectReferenceValue;
            }
        }

        [Serializable] private sealed class StartingDocument { public StartingRecord[] Classes; public StartingArmor[] Armor; }
        [Serializable] private sealed class StartingRecord
        {
            public int Id; public string Name; public int Level; public int[] Stats; public int[] Armor;
        }
        [Serializable] private sealed class StartingArmor
        {
            public int Id; public int Model; public int Icon; public int[] HiddenMasks;
            public float Weight; public float[] Absorption; public float[] Resistance;
        }
    }
}
