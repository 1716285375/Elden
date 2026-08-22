using System;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ZZ.Editor
{
    public static class WeaponQuickSlotUISetup
    {
        private const string k_PlayerPrefabPath = "Assets/Data/Prefabs/Player.prefab";
        private const string k_PlayerUIPrefabPath =
            "Assets/Data/Prefabs/Word Managers/Player UI Manager.prefab";
        private const string k_QuickSlotPrefabPath =
            "Assets/Data/Prefabs/UI/Quick Slot UI.prefab";
        private const string k_InputActionsPath = "Assets/PlayerControls.inputactions";
        private const string k_UnarmedAssetPath =
            "Assets/Data/Items/Weapons/Melee Weapons/Unarmed.asset";
        private const string k_StraightSwordAssetPath =
            "Assets/Data/Items/Weapons/Melee Weapons/Straight Sword.asset";
        private const string k_BroadswordAssetPath =
            "Assets/Data/Items/Weapons/Melee Weapons/Broadsword.asset";
        private const string k_UnarmedIconPath =
            "Assets/Art/Textures/UI/Items/Hand_Slot_Icon_01.png";
        private const string k_StraightSwordIconPath =
            "Assets/Art/Textures/UI/Items/Straight Sword Icon.png";
        private const string k_BroadswordIconPath =
            "Assets/Art/Textures/UI/Items/Broadsword Icon.png";
        private const string k_QuickSlotsName = "Quick Slots";
        private const string k_LeftWeaponSlotName = "Left Weapon Slot";
        private const string k_RightWeaponSlotName = "Right Weapon Slot";
        private const string k_SpellSlotName = "Spell Slot";
        private const string k_ItemSlotName = "Item Slot";
        private const float k_SlotSize = 72f;
        private const float k_QuickSlotsSize = 216f;

        [MenuItem("Tools/Elden/Configure Weapon Quick Slot UI")]
        public static void ConfigureWeaponQuickSlotUI()
        {
            MeleeWeaponItem unarmed = LoadRequiredAsset<MeleeWeaponItem>(
                k_UnarmedAssetPath);
            MeleeWeaponItem straightSword = LoadRequiredAsset<MeleeWeaponItem>(
                k_StraightSwordAssetPath);
            MeleeWeaponItem broadsword = LoadRequiredAsset<MeleeWeaponItem>(
                k_BroadswordAssetPath);
            ConfigureWeaponIcon(unarmed, k_UnarmedIconPath);
            ConfigureWeaponIcon(straightSword, k_StraightSwordIconPath);
            ConfigureWeaponIcon(broadsword, k_BroadswordIconPath);

            GameObject quickSlotPrefab = ConfigureQuickSlotPrefab();
            ConfigurePlayerUIPrefab(quickSlotPrefab);
            DisablePlayerRootMotion();
            AssetDatabase.SaveAssets();
            ValidateWeaponQuickSlotUI();
            Debug.Log(
                "[WeaponQuickSlotUISetup] Configured reusable bottom-left slots, " +
                "weapon icons, event-driven refresh, and root-motion safety.");
        }

        [MenuItem("Tools/Elden/Validate Weapon Quick Slot UI")]
        public static void ValidateWeaponQuickSlotUI()
        {
            ValidateWeaponIcon(k_UnarmedAssetPath, k_UnarmedIconPath);
            ValidateWeaponIcon(k_StraightSwordAssetPath, k_StraightSwordIconPath);
            ValidateWeaponIcon(k_BroadswordAssetPath, k_BroadswordIconPath);
            ValidateQuickSlotPrefab();
            ValidatePlayerUIPrefab();
            ValidatePlayerNetworkAndAnimator();
            ValidateInputActions();
            ValidateRuntimeBindingContract();
            Debug.Log(
                "[WeaponQuickSlotUIValidation] Icons, reusable slots, HUD anchors, " +
                "network permissions, input, binding events, and root motion are valid.");
        }

        private static void ConfigureWeaponIcon(
            WeaponItem weapon,
            string iconPath)
        {
            ConfigureTextureAsSprite(iconPath);
            SerializedObject serializedWeapon = new SerializedObject(weapon);
            GetRequiredProperty(serializedWeapon, "m_itemIcon").objectReferenceValue =
                LoadRequiredAsset<Sprite>(iconPath);
            serializedWeapon.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(weapon);
        }

        private static void ConfigureTextureAsSprite(string texturePath)
        {
            TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException(
                    $"Could not find a TextureImporter for {texturePath}.");
            }

            bool needsReimport = importer.textureType != TextureImporterType.Sprite ||
                importer.spriteImportMode != SpriteImportMode.Single ||
                importer.mipmapEnabled ||
                !importer.alphaIsTransparency;
            if (!needsReimport)
            {
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        private static GameObject ConfigureQuickSlotPrefab()
        {
            EnsureFolder("Assets/Data/Prefabs/UI");
            GameObject root = CreateUIObject("Quick Slot UI", null);
            try
            {
                RectTransform rootRect = (RectTransform)root.transform;
                SetRect(rootRect, Vector2.zero, Vector2.zero, Vector2.one * 0.5f);
                rootRect.sizeDelta = Vector2.one * k_SlotSize;

                Image background = root.AddComponent<Image>();
                background.color = new Color(0.035f, 0.035f, 0.035f, 0.88f);
                background.raycastTarget = false;

                Image highlight = CreateImage(
                    "Highlight",
                    rootRect,
                    new Color(0.72f, 0.56f, 0.22f, 0.9f));
                SetStretch((RectTransform)highlight.transform, -3f);
                highlight.raycastTarget = false;
                highlight.gameObject.SetActive(false);

                Image icon = CreateImage("Icon", rootRect, Color.white);
                SetStretch((RectTransform)icon.transform, 8f);
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                icon.enabled = false;

                TextMeshProUGUI quantity = CreateQuantityText(rootRect);
                quantity.gameObject.SetActive(false);

                UIQuickSlot quickSlot = root.AddComponent<UIQuickSlot>();
                SetObjectReference(quickSlot, "m_iconImage", icon);
                SetObjectReference(quickSlot, "m_quantityText", quantity);
                SetObjectReference(quickSlot, "m_highlightImage", highlight);

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
                    root,
                    k_QuickSlotPrefabPath);
                return prefab != null
                    ? prefab
                    : throw new InvalidOperationException(
                        $"Could not save {k_QuickSlotPrefabPath}.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static TextMeshProUGUI CreateQuantityText(RectTransform parent)
        {
            GameObject quantityObject = CreateUIObject("Quantity", parent);
            RectTransform quantityRect = (RectTransform)quantityObject.transform;
            quantityRect.anchorMin = new Vector2(0.35f, 0f);
            quantityRect.anchorMax = Vector2.one;
            quantityRect.offsetMin = new Vector2(0f, 2f);
            quantityRect.offsetMax = new Vector2(-5f, -2f);
            TextMeshProUGUI quantity = quantityObject.AddComponent<TextMeshProUGUI>();
            quantity.text = string.Empty;
            quantity.fontSize = 20f;
            quantity.alignment = TextAlignmentOptions.BottomRight;
            quantity.color = Color.white;
            quantity.raycastTarget = false;
            return quantity;
        }

        private static void ConfigurePlayerUIPrefab(GameObject quickSlotPrefab)
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(k_PlayerUIPrefabPath);
            try
            {
                PlayerUIHUDManager hud = prefabRoot.GetComponentInChildren<PlayerUIHUDManager>(
                    true) ?? throw new InvalidOperationException(
                        "Player UI prefab is missing PlayerUIHUDManager.");
                RectTransform quickSlots = GetOrCreateRectTransform(
                    hud.transform,
                    k_QuickSlotsName);
                quickSlots.anchorMin = Vector2.zero;
                quickSlots.anchorMax = Vector2.zero;
                quickSlots.pivot = Vector2.zero;
                quickSlots.anchoredPosition = new Vector2(36f, 36f);
                quickSlots.sizeDelta = Vector2.one * k_QuickSlotsSize;

                UIQuickSlot leftSlot = ConfigureSlotInstance(
                    quickSlots,
                    quickSlotPrefab,
                    k_LeftWeaponSlotName,
                    new Vector2(36f, 108f));
                UIQuickSlot rightSlot = ConfigureSlotInstance(
                    quickSlots,
                    quickSlotPrefab,
                    k_RightWeaponSlotName,
                    new Vector2(180f, 108f));
                UIQuickSlot spellSlot = ConfigureSlotInstance(
                    quickSlots,
                    quickSlotPrefab,
                    k_SpellSlotName,
                    new Vector2(108f, 180f));
                UIQuickSlot itemSlot = ConfigureSlotInstance(
                    quickSlots,
                    quickSlotPrefab,
                    k_ItemSlotName,
                    new Vector2(108f, 36f));

                SetObjectReference(hud, "m_leftWeaponQuickSlot", leftSlot);
                SetObjectReference(hud, "m_rightWeaponQuickSlot", rightSlot);
                SetObjectReference(hud, "m_spellQuickSlot", spellSlot);
                SetObjectReference(hud, "m_itemQuickSlot", itemSlot);
                if (PrefabUtility.SaveAsPrefabAsset(prefabRoot, k_PlayerUIPrefabPath) == null)
                {
                    throw new InvalidOperationException(
                        $"Could not save {k_PlayerUIPrefabPath}.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static UIQuickSlot ConfigureSlotInstance(
            RectTransform parent,
            GameObject quickSlotPrefab,
            string slotName,
            Vector2 position)
        {
            Transform existingSlot = parent.Find(slotName);
            GameObject instance = null;
            if (existingSlot != null &&
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(
                    existingSlot.gameObject) == k_QuickSlotPrefabPath)
            {
                instance = existingSlot.gameObject;
            }

            if (instance == null)
            {
                if (existingSlot != null)
                {
                    UnityEngine.Object.DestroyImmediate(existingSlot.gameObject);
                }

                instance = PrefabUtility.InstantiatePrefab(
                    quickSlotPrefab,
                    parent) as GameObject;
            }

            if (instance == null)
            {
                throw new InvalidOperationException(
                    $"Could not instantiate {k_QuickSlotPrefabPath}.");
            }

            instance.name = slotName;
            RectTransform slotRect = GetRequiredComponent<RectTransform>(instance);
            slotRect.anchorMin = Vector2.zero;
            slotRect.anchorMax = Vector2.zero;
            slotRect.pivot = Vector2.one * 0.5f;
            slotRect.anchoredPosition = position;
            slotRect.sizeDelta = Vector2.one * k_SlotSize;
            return GetRequiredComponent<UIQuickSlot>(instance);
        }

        private static void DisablePlayerRootMotion()
        {
            GameObject playerRoot = PrefabUtility.LoadPrefabContents(k_PlayerPrefabPath);
            try
            {
                Animator animator = playerRoot.GetComponentInChildren<Animator>(true) ??
                    throw new InvalidOperationException(
                        "Player prefab is missing its Animator.");
                if (!animator.applyRootMotion)
                {
                    return;
                }

                animator.applyRootMotion = false;
                if (PrefabUtility.SaveAsPrefabAsset(playerRoot, k_PlayerPrefabPath) == null)
                {
                    throw new InvalidOperationException(
                        $"Could not save {k_PlayerPrefabPath}.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(playerRoot);
            }
        }

        private static void ValidateWeaponIcon(string weaponPath, string iconPath)
        {
            WeaponItem weapon = LoadRequiredAsset<WeaponItem>(weaponPath);
            Sprite icon = LoadRequiredAsset<Sprite>(iconPath);
            if (weapon.ItemIcon != icon)
            {
                throw new InvalidOperationException(
                    $"Weapon {weapon.name} is missing its quick-slot icon.");
            }
        }

        private static void ValidateQuickSlotPrefab()
        {
            GameObject quickSlotPrefab = LoadRequiredAsset<GameObject>(
                k_QuickSlotPrefabPath);
            UIQuickSlot quickSlot = GetRequiredComponent<UIQuickSlot>(quickSlotPrefab);
            ValidateObjectReference(quickSlot, "m_iconImage");
            ValidateObjectReference(quickSlot, "m_quantityText");
            ValidateObjectReference(quickSlot, "m_highlightImage");
            if (quickSlotPrefab.transform.Find("Icon") == null ||
                quickSlotPrefab.transform.Find("Quantity") == null ||
                quickSlotPrefab.transform.Find("Highlight") == null)
            {
                throw new InvalidOperationException(
                    "Quick Slot UI needs icon, quantity, and highlight children.");
            }
        }

        private static void ValidatePlayerUIPrefab()
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(k_PlayerUIPrefabPath);
            try
            {
                PlayerUIHUDManager hud = prefabRoot.GetComponentInChildren<PlayerUIHUDManager>(
                    true) ?? throw new InvalidOperationException(
                        "Player UI prefab is missing PlayerUIHUDManager.");
                RectTransform quickSlots = hud.transform.Find(k_QuickSlotsName)
                    as RectTransform;
                if (quickSlots == null ||
                    quickSlots.anchorMin != Vector2.zero ||
                    quickSlots.anchorMax != Vector2.zero ||
                    quickSlots.pivot != Vector2.zero)
                {
                    throw new InvalidOperationException(
                        "Quick Slots must be anchored to the bottom-left of the HUD.");
                }

                string[] slotNames =
                {
                    k_LeftWeaponSlotName,
                    k_RightWeaponSlotName,
                    k_SpellSlotName,
                    k_ItemSlotName
                };
                foreach (string slotName in slotNames)
                {
                    UIQuickSlot slot = quickSlots.Find(slotName)
                        ?.GetComponent<UIQuickSlot>();
                    if (slot == null ||
                        PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(
                            slot.gameObject) != k_QuickSlotPrefabPath)
                    {
                        throw new InvalidOperationException(
                            $"{slotName} must reuse {k_QuickSlotPrefabPath}.");
                    }
                }

                ValidateObjectReference(hud, "m_leftWeaponQuickSlot");
                ValidateObjectReference(hud, "m_rightWeaponQuickSlot");
                ValidateObjectReference(hud, "m_spellQuickSlot");
                ValidateObjectReference(hud, "m_itemQuickSlot");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static void ValidatePlayerNetworkAndAnimator()
        {
            GameObject playerRoot = PrefabUtility.LoadPrefabContents(k_PlayerPrefabPath);
            try
            {
                PlayerNetworkManager networkManager =
                    GetRequiredComponent<PlayerNetworkManager>(playerRoot);
                Animator animator = playerRoot.GetComponentInChildren<Animator>(true) ??
                    throw new InvalidOperationException(
                        "Player prefab is missing its Animator.");
                if (networkManager.CurrentRightHandWeaponID.ReadPerm !=
                        Unity.Netcode.NetworkVariableReadPermission.Everyone ||
                    networkManager.CurrentRightHandWeaponID.WritePerm !=
                        Unity.Netcode.NetworkVariableWritePermission.Owner ||
                    networkManager.CurrentLeftHandWeaponID.ReadPerm !=
                        Unity.Netcode.NetworkVariableReadPermission.Everyone ||
                    networkManager.CurrentLeftHandWeaponID.WritePerm !=
                        Unity.Netcode.NetworkVariableWritePermission.Owner ||
                    animator.applyRootMotion)
                {
                    throw new InvalidOperationException(
                        "Weapon IDs need owner-write/public-read sync and root motion off.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(playerRoot);
            }
        }

        private static void ValidateInputActions()
        {
            InputActionAsset inputActions = LoadRequiredAsset<InputActionAsset>(
                k_InputActionsPath);
            InputActionMap movementMap = inputActions.FindActionMap(
                "Player Movement",
                true);
            InputAction rightAction = movementMap.FindAction(
                "Switch Right Weapon",
                true);
            InputAction leftAction = movementMap.FindAction(
                "Switch Left Weapon",
                true);
            if (!HasBinding(rightAction, "<Gamepad>/dpad/right") ||
                !HasBinding(rightAction, "<Keyboard>/e") ||
                !HasBinding(leftAction, "<Gamepad>/dpad/left") ||
                !HasBinding(leftAction, "<Keyboard>/q"))
            {
                throw new InvalidOperationException(
                    "Weapon switching needs D-Pad Right/Left and E/Q bindings.");
            }
        }

        private static void ValidateRuntimeBindingContract()
        {
            if (typeof(PlayerInventoryManager).GetEvent("RightHandWeaponChanged") == null ||
                typeof(PlayerInventoryManager).GetEvent("LeftHandWeaponChanged") == null ||
                typeof(PlayerUIHUDManager).GetMethod(
                    "BindQuickSlots",
                    BindingFlags.Instance | BindingFlags.Public) == null ||
                typeof(PlayerUIHUDManager).GetMethod(
                    "UnbindQuickSlots",
                    BindingFlags.Instance | BindingFlags.Public) == null)
            {
                throw new InvalidOperationException(
                    "Inventory-to-HUD weapon change events are missing.");
            }
        }

        private static Image CreateImage(
            string objectName,
            RectTransform parent,
            Color color)
        {
            GameObject imageObject = CreateUIObject(objectName, parent);
            Image image = imageObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private static GameObject CreateUIObject(
            string objectName,
            RectTransform parent)
        {
            GameObject uiObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer));
            uiObject.layer = LayerMask.NameToLayer("UI");
            if (parent != null)
            {
                uiObject.transform.SetParent(parent, false);
            }

            return uiObject;
        }

        private static void SetRect(
            RectTransform rectTransform,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot)
        {
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.pivot = pivot;
            rectTransform.anchoredPosition = Vector2.zero;
        }

        private static void SetStretch(RectTransform rectTransform, float inset)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.one * inset;
            rectTransform.offsetMax = Vector2.one * -inset;
        }

        private static RectTransform GetOrCreateRectTransform(
            Transform parent,
            string objectName)
        {
            Transform existing = parent.Find(objectName);
            if (existing != null)
            {
                return existing as RectTransform ??
                    throw new InvalidOperationException(
                        $"{objectName} must use a RectTransform.");
            }

            return (RectTransform)CreateUIObject(
                objectName,
                parent as RectTransform).transform;
        }

        private static bool HasBinding(InputAction action, string bindingPath)
        {
            return action.bindings.Any(binding => binding.path == bindingPath);
        }

        private static void EnsureFolder(string folderPath)
        {
            string[] segments = folderPath.Split('/');
            string currentPath = segments[0];
            for (int index = 1; index < segments.Length; index++)
            {
                string nextPath = $"{currentPath}/{segments[index]}";
                if (!AssetDatabase.IsValidFolder(nextPath))
                {
                    AssetDatabase.CreateFolder(currentPath, segments[index]);
                }

                currentPath = nextPath;
            }
        }

        private static T LoadRequiredAsset<T>(string assetPath)
            where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            return asset != null
                ? asset
                : throw new InvalidOperationException($"Could not load {assetPath}.");
        }

        private static T GetRequiredComponent<T>(GameObject gameObject)
            where T : Component
        {
            T component = gameObject.GetComponent<T>();
            return component != null
                ? component
                : throw new InvalidOperationException(
                    $"{gameObject.name} is missing {typeof(T).Name}.");
        }

        private static void SetObjectReference(
            UnityEngine.Object target,
            string propertyName,
            UnityEngine.Object value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            GetRequiredProperty(serializedObject, propertyName).objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void ValidateObjectReference(
            UnityEngine.Object target,
            string propertyName)
        {
            if (GetRequiredProperty(
                    new SerializedObject(target),
                    propertyName).objectReferenceValue == null)
            {
                throw new InvalidOperationException(
                    $"{target.name}.{propertyName} is not assigned.");
            }
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
