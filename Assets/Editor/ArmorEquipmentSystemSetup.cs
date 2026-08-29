using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace ZZ.Editor
{
    public static class ArmorEquipmentSystemSetup
    {
        private const string k_PlayerPrefabPath = "Assets/_Game/Prefabs/Characters/Player/Player.prefab";
        private const string k_DatabasePrefabPath =
            "Assets/_Game/Prefabs/World/Managers/World Item Database.prefab";
        private const string k_ArmorFolder = "Assets/_Game/Data/Items/Armor";
        private const string k_ModelFolder = k_ArmorFolder + "/Models";
        private const string k_HeadPath = k_ArmorFolder + "/Starter Hood.asset";
        private const string k_BodyPath = k_ArmorFolder + "/Starter Armor.asset";
        private const string k_HandPath = k_ArmorFolder + "/Starter Gauntlets.asset";
        private const string k_LegPath = k_ArmorFolder + "/Starter Greaves.asset";
        private const int k_HeadID = 4;
        private const int k_BodyID = 5;
        private const int k_HandID = 6;
        private const int k_LegID = 7;

        private static readonly ModelDefinition[] s_headModels =
        {
            new ModelDefinition(
                "Starter Hood",
                EquipmentModelType.HeadCovering,
                "Chr_HeadCoverings_No_Hair_01",
                "Chr_HeadCoverings_No_Hair_01")
        };

        private static readonly ModelDefinition[] s_bodyModels =
        {
            new ModelDefinition(
                "Starter Torso",
                EquipmentModelType.Torso,
                "Chr_Torso_Male_01",
                "Chr_Torso_Female_01"),
            new ModelDefinition(
                "Starter Upper Right Arm",
                EquipmentModelType.UpperRightArm,
                "Chr_ArmUpperRight_Male_01",
                "Chr_ArmUpperRight_Female_01"),
            new ModelDefinition(
                "Starter Upper Left Arm",
                EquipmentModelType.UpperLeftArm,
                "Chr_ArmUpperLeft_Male_01",
                "Chr_ArmUpperLeft_Female_01")
        };

        private static readonly ModelDefinition[] s_handModels =
        {
            new ModelDefinition(
                "Starter Lower Right Arm",
                EquipmentModelType.LowerRightArm,
                "Chr_ArmLowerRight_Male_01",
                "Chr_ArmLowerRight_Female_01"),
            new ModelDefinition(
                "Starter Lower Left Arm",
                EquipmentModelType.LowerLeftArm,
                "Chr_ArmLowerLeft_Male_01",
                "Chr_ArmLowerLeft_Female_01"),
            new ModelDefinition(
                "Starter Right Hand",
                EquipmentModelType.RightHand,
                "Chr_HandRight_Male_01",
                "Chr_HandRight_Female_01"),
            new ModelDefinition(
                "Starter Left Hand",
                EquipmentModelType.LeftHand,
                "Chr_HandLeft_Male_01",
                "Chr_HandLeft_Female_01")
        };

        private static readonly ModelDefinition[] s_legModels =
        {
            new ModelDefinition(
                "Starter Hips",
                EquipmentModelType.Hips,
                "Chr_Hips_Male_01",
                "Chr_Hips_Female_01"),
            new ModelDefinition(
                "Starter Right Leg",
                EquipmentModelType.RightLeg,
                "Chr_LegRight_Male_01",
                "Chr_LegRight_Female_01"),
            new ModelDefinition(
                "Starter Left Leg",
                EquipmentModelType.LeftLeg,
                "Chr_LegLeft_Male_01",
                "Chr_LegLeft_Female_01")
        };

        [MenuItem("Tools/Elden/Configure Armor Equipment System")]
        public static void ConfigureArmorEquipmentSystem()
        {
            EnsureFolder(k_ArmorFolder);
            EnsureFolder(k_ModelFolder);

            HeadEquipmentItem head = ConfigureArmorItem<HeadEquipmentItem>(
                k_HeadPath,
                "Starter Hood",
                k_HeadID,
                2.5f,
                new ArmorValues(3f, 4f, 2f, 3f, 4f, 4f, 4f, 4f, 4f, 2f),
                ConfigureModels(s_headModels));
            SetEnum(head, "m_headEquipmentType", (int)HeadEquipmentType.Hood);
            BodyEquipmentItem body = ConfigureArmorItem<BodyEquipmentItem>(
                k_BodyPath,
                "Starter Armor",
                k_BodyID,
                8f,
                new ArmorValues(12f, 8f, 10f, 6f, 8f, 10f, 12f, 8f, 9f, 8f),
                ConfigureModels(s_bodyModels));
            HandEquipmentItem hands = ConfigureArmorItem<HandEquipmentItem>(
                k_HandPath,
                "Starter Gauntlets",
                k_HandID,
                3.5f,
                new ArmorValues(5f, 4f, 4f, 3f, 4f, 5f, 6f, 4f, 5f, 3f),
                ConfigureModels(s_handModels));
            LegEquipmentItem legs = ConfigureArmorItem<LegEquipmentItem>(
                k_LegPath,
                "Starter Greaves",
                k_LegID,
                5f,
                new ArmorValues(7f, 5f, 6f, 4f, 5f, 7f, 8f, 6f, 7f, 5f),
                ConfigureModels(s_legModels));

            ConfigureDatabase(head, body, hands, legs);
            ConfigurePlayerPrefab(head, body, hands, legs);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ValidateArmorEquipmentSystem();
            Debug.Log(
                "[ArmorEquipmentSystemSetup] Configured armor data, modular models, " +
                "network reconstruction, body type, and save integration.");
        }

        [MenuItem("Tools/Elden/Validate Armor Equipment System")]
        public static void ValidateArmorEquipmentSystem()
        {
            HeadEquipmentItem head = LoadRequiredAsset<HeadEquipmentItem>(k_HeadPath);
            BodyEquipmentItem body = LoadRequiredAsset<BodyEquipmentItem>(k_BodyPath);
            HandEquipmentItem hands = LoadRequiredAsset<HandEquipmentItem>(k_HandPath);
            LegEquipmentItem legs = LoadRequiredAsset<LegEquipmentItem>(k_LegPath);

            ValidateItem(head, k_HeadID, s_headModels.Length);
            ValidateItem(body, k_BodyID, s_bodyModels.Length);
            ValidateItem(hands, k_HandID, s_handModels.Length);
            ValidateItem(legs, k_LegID, s_legModels.Length);
            if (!typeof(EquipmentItem).IsAssignableFrom(typeof(WeaponItem)) ||
                head.HeadEquipmentType != HeadEquipmentType.Hood)
            {
                throw new InvalidOperationException(
                    "Equipment inheritance or head feature behavior is invalid.");
            }

            ValidateDatabase(head, body, hands, legs);
            ValidatePlayerPrefab(head, body, hands, legs);
            Debug.Log(
                "[ArmorEquipmentSystemValidation] Typed IDs, model resolution, gender roots, " +
                "owner-written replication, armor aggregation, and null sentinels are valid.");
        }

        private static T ConfigureArmorItem<T>(
            string assetPath,
            string itemName,
            int itemID,
            float itemWeight,
            ArmorValues values,
            EquipmentModel[] models) where T : ArmorItem
        {
            T item = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(item, assetPath);
            }

            SerializedObject serializedItem = new SerializedObject(item);
            SetString(serializedItem, "m_itemName", itemName);
            SetString(
                serializedItem,
                "m_itemDescription",
                $"A dependable {itemName.ToLowerInvariant()} fitted to a wandering warrior.");
            SetInt(serializedItem, "m_itemID", itemID);
            SetFloat(serializedItem, "m_itemWeight", itemWeight);
            SetFloat(serializedItem, "m_physicalAbsorption", values.Physical);
            SetFloat(serializedItem, "m_magicAbsorption", values.Magic);
            SetFloat(serializedItem, "m_fireAbsorption", values.Fire);
            SetFloat(serializedItem, "m_lightningAbsorption", values.Lightning);
            SetFloat(serializedItem, "m_holyAbsorption", values.Holy);
            SetFloat(serializedItem, "m_immunity", values.Immunity);
            SetFloat(serializedItem, "m_robustness", values.Robustness);
            SetFloat(serializedItem, "m_focus", values.Focus);
            SetFloat(serializedItem, "m_vitality", values.Vitality);
            SetFloat(serializedItem, "m_poise", values.Poise);
            SetObjectArray(serializedItem, "m_equipmentModels", models);
            serializedItem.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
            return item;
        }

        private static EquipmentModel[] ConfigureModels(ModelDefinition[] definitions)
        {
            EquipmentModel[] models = new EquipmentModel[definitions.Length];
            for (int modelIndex = 0; modelIndex < definitions.Length; modelIndex++)
            {
                ModelDefinition definition = definitions[modelIndex];
                string assetPath = $"{k_ModelFolder}/{definition.AssetName}.asset";
                EquipmentModel model =
                    AssetDatabase.LoadAssetAtPath<EquipmentModel>(assetPath);
                if (model == null)
                {
                    model = ScriptableObject.CreateInstance<EquipmentModel>();
                    AssetDatabase.CreateAsset(model, assetPath);
                }

                SerializedObject serializedModel = new SerializedObject(model);
                SetEnum(
                    serializedModel,
                    "m_equipmentModelType",
                    (int)definition.ModelType);
                SetString(serializedModel, "m_maleModelName", definition.MaleModelName);
                SetString(serializedModel, "m_femaleModelName", definition.FemaleModelName);
                serializedModel.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(model);
                models[modelIndex] = model;
            }

            return models;
        }

        private static void ConfigureDatabase(
            HeadEquipmentItem head,
            BodyEquipmentItem body,
            HandEquipmentItem hands,
            LegEquipmentItem legs)
        {
            GameObject databaseRoot = PrefabUtility.LoadPrefabContents(k_DatabasePrefabPath);
            try
            {
                WorldItemDatabase database = GetRequiredComponent<WorldItemDatabase>(databaseRoot);
                SerializedObject serializedDatabase = new SerializedObject(database);
                SerializedProperty items = GetRequiredProperty(serializedDatabase, "m_items");
                if (items.arraySize < k_HeadID)
                {
                    throw new InvalidOperationException(
                        "World Item Database must contain weapon IDs 0 through 3.");
                }

                UnityEngine.Object[] allItems = new UnityEngine.Object[k_LegID + 1];
                for (int itemIndex = 0; itemIndex < k_HeadID; itemIndex++)
                {
                    allItems[itemIndex] =
                        items.GetArrayElementAtIndex(itemIndex).objectReferenceValue;
                }

                allItems[k_HeadID] = head;
                allItems[k_BodyID] = body;
                allItems[k_HandID] = hands;
                allItems[k_LegID] = legs;
                SetObjectArray(serializedDatabase, "m_items", allItems);
                SetObjectArray(serializedDatabase, "m_headEquipment", new[] { head });
                SetObjectArray(serializedDatabase, "m_bodyEquipment", new[] { body });
                SetObjectArray(serializedDatabase, "m_handEquipment", new[] { hands });
                SetObjectArray(serializedDatabase, "m_legEquipment", new[] { legs });
                serializedDatabase.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(databaseRoot, k_DatabasePrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(databaseRoot);
            }
        }

        private static void ConfigurePlayerPrefab(
            HeadEquipmentItem head,
            BodyEquipmentItem body,
            HandEquipmentItem hands,
            LegEquipmentItem legs)
        {
            GameObject playerRoot = PrefabUtility.LoadPrefabContents(k_PlayerPrefabPath);
            try
            {
                PlayerBodyManager bodyManager = GetOrAddComponent<PlayerBodyManager>(playerRoot);
                PlayerInventoryManager inventory =
                    GetRequiredComponent<PlayerInventoryManager>(playerRoot);
                GetRequiredComponent<PlayerEquipmentManager>(playerRoot);
                Transform modularRoot = FindRequiredDescendant(
                    playerRoot.transform,
                    "Modular_Characters");
                SetObjectReference(bodyManager, "m_modularCharacterRoot", modularRoot);
                SetObjectReference(inventory, "m_startingHeadEquipment", head);
                SetObjectReference(inventory, "m_startingBodyEquipment", body);
                SetObjectReference(inventory, "m_startingHandEquipment", hands);
                SetObjectReference(inventory, "m_startingLegEquipment", legs);

                FindRequiredDescendant(modularRoot, "Male_Parts").gameObject.SetActive(true);
                FindRequiredDescendant(modularRoot, "Female_Parts").gameObject.SetActive(false);
                if (PrefabUtility.SaveAsPrefabAsset(playerRoot, k_PlayerPrefabPath) == null)
                {
                    throw new InvalidOperationException("Could not save the configured Player prefab.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(playerRoot);
            }
        }

        private static void ValidateDatabase(
            HeadEquipmentItem head,
            BodyEquipmentItem body,
            HandEquipmentItem hands,
            LegEquipmentItem legs)
        {
            GameObject databasePrefab = LoadRequiredAsset<GameObject>(k_DatabasePrefabPath);
            WorldItemDatabase database = GetRequiredComponent<WorldItemDatabase>(databasePrefab);
            if (database.Items.Count != k_LegID + 1 ||
                database.GetHeadEquipmentByID(k_HeadID) != head ||
                database.GetBodyEquipmentByID(k_BodyID) != body ||
                database.GetHandEquipmentByID(k_HandID) != hands ||
                database.GetLegEquipmentByID(k_LegID) != legs ||
                database.GetHeadEquipmentByID(-1) != null)
            {
                throw new InvalidOperationException(
                    "World Item Database armor lists or stable IDs are invalid.");
            }
        }

        private static void ValidatePlayerPrefab(
            HeadEquipmentItem head,
            BodyEquipmentItem body,
            HandEquipmentItem hands,
            LegEquipmentItem legs)
        {
            GameObject playerRoot = PrefabUtility.LoadPrefabContents(k_PlayerPrefabPath);
            try
            {
                PlayerManager player = GetRequiredComponent<PlayerManager>(playerRoot);
                PlayerBodyManager bodyManager = GetRequiredComponent<PlayerBodyManager>(playerRoot);
                PlayerEquipmentManager equipment =
                    GetRequiredComponent<PlayerEquipmentManager>(playerRoot);
                PlayerNetworkManager network =
                    GetRequiredComponent<PlayerNetworkManager>(playerRoot);
                PlayerStatsManager stats = GetRequiredComponent<PlayerStatsManager>(playerRoot);
                Transform modularRoot = FindRequiredDescendant(
                    playerRoot.transform,
                    "Modular_Characters");
                ValidateObjectReference(bodyManager, "m_modularCharacterRoot", modularRoot);
                ValidateModelDefinitions(modularRoot, s_headModels);
                ValidateModelDefinitions(modularRoot, s_bodyModels);
                ValidateModelDefinitions(modularRoot, s_handModels);
                ValidateModelDefinitions(modularRoot, s_legModels);
                ValidateNetworkVariable(network.CurrentHeadEquipmentID, -1);
                ValidateNetworkVariable(network.CurrentBodyEquipmentID, -1);
                ValidateNetworkVariable(network.CurrentHandEquipmentID, -1);
                ValidateNetworkVariable(network.CurrentLegEquipmentID, -1);
                ValidateNetworkVariable(network.IsMale, true);

                bodyManager.InitializeBodyModels();
                equipment.InitializeArmorModels();
                foreach (EquipmentModel model in head.EquipmentModels
                             .Concat(body.EquipmentModels)
                             .Concat(hands.EquipmentModels)
                             .Concat(legs.EquipmentModels))
                {
                    string maleName = model.MaleModelName;
                    string femaleName = model.FemaleModelName;
                    if (!equipment.LoadArmorModel(model.EquipmentModelType, maleName) ||
                        !equipment.LoadArmorModel(model.EquipmentModelType, femaleName))
                    {
                        throw new InvalidOperationException(
                            $"Equipment model {model.name} cannot resolve both body types.");
                    }
                }

                float blockingPhysical = stats.BlockingPhysicalAbsorption;
                stats.CalculateTotalArmorValues(head, body, hands, legs);
                if (!Mathf.Approximately(stats.ArmorPhysicalAbsorption, 27f) ||
                    !Mathf.Approximately(stats.ArmorMagicAbsorption, 21f) ||
                    !Mathf.Approximately(stats.ArmorPoiseDefense, 18f) ||
                    !Mathf.Approximately(stats.BlockingPhysicalAbsorption, blockingPhysical) ||
                    !Mathf.Approximately(stats.BasePoiseDefense, 68f))
                {
                    throw new InvalidOperationException(
                        "Armor aggregation changed blocking values or produced invalid totals.");
                }

                if (player == null)
                {
                    throw new InvalidOperationException("Player manager validation failed.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(playerRoot);
            }
        }

        private static void ValidateItem(ArmorItem item, int itemID, int modelCount)
        {
            if (item.ItemID != itemID ||
                item.ItemWeight <= 0f ||
                item.EquipmentModels.Length != modelCount ||
                item.PhysicalAbsorption <= 0f ||
                item.Poise <= 0f)
            {
                throw new InvalidOperationException(
                    $"Armor asset {item.name} has invalid identity or defense data.");
            }
        }

        private static void ValidateModelDefinitions(
            Transform modularRoot,
            IEnumerable<ModelDefinition> definitions)
        {
            HashSet<string> modelNames = modularRoot
                .GetComponentsInChildren<Transform>(true)
                .Select(transform => transform.name)
                .ToHashSet(StringComparer.Ordinal);
            foreach (ModelDefinition definition in definitions)
            {
                if (!modelNames.Contains(definition.MaleModelName) ||
                    !modelNames.Contains(definition.FemaleModelName))
                {
                    throw new InvalidOperationException(
                        $"Player prefab is missing models for {definition.AssetName}.");
                }
            }
        }

        private static void ValidateNetworkVariable<T>(NetworkVariable<T> variable, T value)
            where T : unmanaged
        {
            if (variable.ReadPerm != NetworkVariableReadPermission.Everyone ||
                variable.WritePerm != NetworkVariableWritePermission.Owner ||
                !EqualityComparer<T>.Default.Equals(variable.Value, value))
            {
                throw new InvalidOperationException(
                    $"Network variable {typeof(T).Name} has invalid permissions or defaults.");
            }
        }

        private static void EnsureFolder(string folderPath)
        {
            string[] segments = folderPath.Split('/');
            string currentPath = segments[0];
            for (int segmentIndex = 1; segmentIndex < segments.Length; segmentIndex++)
            {
                string nextPath = $"{currentPath}/{segments[segmentIndex]}";
                if (!AssetDatabase.IsValidFolder(nextPath))
                {
                    AssetDatabase.CreateFolder(currentPath, segments[segmentIndex]);
                }

                currentPath = nextPath;
            }
        }

        private static Transform FindRequiredDescendant(Transform root, string objectName)
        {
            Transform result = root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(candidate => candidate.name == objectName);
            return result != null
                ? result
                : throw new InvalidOperationException($"Could not find {objectName}.");
        }

        private static T LoadRequiredAsset<T>(string assetPath) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            return asset != null
                ? asset
                : throw new InvalidOperationException($"Could not load {assetPath}.");
        }

        private static T GetRequiredComponent<T>(GameObject root) where T : Component
        {
            T component = root.GetComponent<T>();
            return component != null
                ? component
                : throw new InvalidOperationException($"{root.name} is missing {typeof(T).Name}.");
        }

        private static T GetOrAddComponent<T>(GameObject root) where T : Component
        {
            T component = root.GetComponent<T>();
            return component != null ? component : root.AddComponent<T>();
        }

        private static void SetObjectReference(
            UnityEngine.Object target,
            string propertyName,
            UnityEngine.Object value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            GetRequiredProperty(serializedObject, propertyName).objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObjectArray(
            SerializedObject serializedObject,
            string propertyName,
            UnityEngine.Object[] values)
        {
            SerializedProperty property = GetRequiredProperty(serializedObject, propertyName);
            property.arraySize = values.Length;
            for (int valueIndex = 0; valueIndex < values.Length; valueIndex++)
            {
                property.GetArrayElementAtIndex(valueIndex).objectReferenceValue =
                    values[valueIndex];
            }
        }

        private static void SetEnum(
            UnityEngine.Object target,
            string propertyName,
            int value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SetEnum(serializedObject, propertyName, value);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetEnum(
            SerializedObject serializedObject,
            string propertyName,
            int value)
        {
            GetRequiredProperty(serializedObject, propertyName).enumValueIndex = value;
        }

        private static void SetString(
            SerializedObject serializedObject,
            string propertyName,
            string value)
        {
            GetRequiredProperty(serializedObject, propertyName).stringValue = value;
        }

        private static void SetInt(
            SerializedObject serializedObject,
            string propertyName,
            int value)
        {
            GetRequiredProperty(serializedObject, propertyName).intValue = value;
        }

        private static void SetFloat(
            SerializedObject serializedObject,
            string propertyName,
            float value)
        {
            GetRequiredProperty(serializedObject, propertyName).floatValue = value;
        }

        private static void ValidateObjectReference(
            UnityEngine.Object target,
            string propertyName,
            UnityEngine.Object expectedValue)
        {
            SerializedProperty property = GetRequiredProperty(
                new SerializedObject(target),
                propertyName);
            if (property.objectReferenceValue != expectedValue)
            {
                throw new InvalidOperationException(
                    $"{target.name}.{propertyName} is not assigned correctly.");
            }
        }

        private static SerializedProperty GetRequiredProperty(
            SerializedObject serializedObject,
            string propertyName)
        {
            return serializedObject.FindProperty(propertyName) ??
                throw new InvalidOperationException(
                    $"Could not find {serializedObject.targetObject.GetType().Name}.{propertyName}.");
        }

        private readonly struct ModelDefinition
        {
            public ModelDefinition(
                string assetName,
                EquipmentModelType modelType,
                string maleModelName,
                string femaleModelName)
            {
                AssetName = assetName;
                ModelType = modelType;
                MaleModelName = maleModelName;
                FemaleModelName = femaleModelName;
            }

            public string AssetName { get; }
            public EquipmentModelType ModelType { get; }
            public string MaleModelName { get; }
            public string FemaleModelName { get; }
        }

        private readonly struct ArmorValues
        {
            public ArmorValues(
                float physical,
                float magic,
                float fire,
                float lightning,
                float holy,
                float immunity,
                float robustness,
                float focus,
                float vitality,
                float poise)
            {
                Physical = physical;
                Magic = magic;
                Fire = fire;
                Lightning = lightning;
                Holy = holy;
                Immunity = immunity;
                Robustness = robustness;
                Focus = focus;
                Vitality = vitality;
                Poise = poise;
            }

            public float Physical { get; }
            public float Magic { get; }
            public float Fire { get; }
            public float Lightning { get; }
            public float Holy { get; }
            public float Immunity { get; }
            public float Robustness { get; }
            public float Focus { get; }
            public float Vitality { get; }
            public float Poise { get; }
        }
    }
}
