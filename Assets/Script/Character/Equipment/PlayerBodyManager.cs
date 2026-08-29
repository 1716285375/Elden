using System;
using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Owns gender roots and restores default modular body features around armor changes.
    /// </summary>
    [RequireComponent(typeof(PlayerManager))]
    public class PlayerBodyManager : MonoBehaviour
    {
        private const string k_MalePartsName = "Male_Parts";
        private const string k_FemalePartsName = "Female_Parts";
        private const string k_HairGroupName = "All_01_Hair";
        private const string k_MaleHeadGroupName = "Male_00_Head";
        private const string k_FemaleHeadGroupName = "Female_00_Head";
        private const string k_FacialHairGroupName = "Male_02_FacialHair";

        [SerializeField] private Transform m_modularCharacterRoot;
        [SerializeField] private GameObject[] m_hairObjects = Array.Empty<GameObject>();

        private Transform m_maleParts;
        private Transform m_femaleParts;
        private GameObject m_defaultMaleHead;
        private GameObject m_defaultFemaleHead;
        private GameObject m_defaultFacialHair;
        private GameObject[] m_defaultMaleBody = Array.Empty<GameObject>();
        private GameObject[] m_defaultFemaleBody = Array.Empty<GameObject>();
        private GameObject[] m_defaultMaleArms = Array.Empty<GameObject>();
        private GameObject[] m_defaultFemaleArms = Array.Empty<GameObject>();
        private GameObject[] m_defaultMaleLegs = Array.Empty<GameObject>();
        private GameObject[] m_defaultFemaleLegs = Array.Empty<GameObject>();
        private bool m_isMale = true;
        private bool m_isHairVisible = true;
        private int m_hairstyleID;
        private Color32 m_hairColor = new(79, 53, 35, 255);

        /// <summary>Gets the root containing all embedded modular character meshes.</summary>
        public Transform ModularCharacterRoot => m_modularCharacterRoot;

        /// <summary>Gets whether the male body hierarchy is currently selected.</summary>
        public bool IsMale => m_isMale;

        /// <summary>Gets the currently selected hairstyle, where zero represents bald.</summary>
        public int HairstyleID => m_hairstyleID;

        /// <summary>Gets the selected hair tint stored with byte-accurate channels.</summary>
        public Color32 HairColor => m_hairColor;

        /// <summary>Gets the number of selectable hairstyles including bald.</summary>
        public int HairstyleCount => m_hairObjects?.Length ?? 0;

        private void Awake()
        {
            InitializeBodyModels();
        }

        /// <summary>Discovers body roots and default face, hair, and facial-hair models.</summary>
        public void InitializeBodyModels()
        {
            if (m_modularCharacterRoot == null)
            {
                m_modularCharacterRoot = FindDescendant(transform, "Modular_Characters");
            }

            if (m_modularCharacterRoot == null)
            {
                Debug.LogError("The player is missing its Modular_Characters hierarchy.", this);
                return;
            }

            m_maleParts = FindDescendant(m_modularCharacterRoot, k_MalePartsName);
            m_femaleParts = FindDescendant(m_modularCharacterRoot, k_FemalePartsName);
            m_defaultMaleHead = FindDefaultChild(
                FindDescendant(m_modularCharacterRoot, k_MaleHeadGroupName));
            m_defaultFemaleHead = FindDefaultChild(
                FindDescendant(m_modularCharacterRoot, k_FemaleHeadGroupName));
            InitializeHairObjects(
                FindDescendant(m_modularCharacterRoot, k_HairGroupName));
            m_defaultFacialHair = FindDefaultChild(
                FindDescendant(m_modularCharacterRoot, k_FacialHairGroupName));
            m_defaultMaleBody = FindDefaultChildren(
                m_modularCharacterRoot,
                "Male_03_Torso");
            m_defaultFemaleBody = FindDefaultChildren(
                m_modularCharacterRoot,
                "Female_03_Torso");
            m_defaultMaleArms = FindDefaultChildren(
                m_modularCharacterRoot,
                "Male_04_Arm_Upper_Right",
                "Male_05_Arm_Upper_Left",
                "Male_06_Arm_Lower_Right",
                "Male_07_Arm_Lower_Left",
                "Male_08_Hand_Right",
                "Male_09_Hand_Left");
            m_defaultFemaleArms = FindDefaultChildren(
                m_modularCharacterRoot,
                "Female_04_Arm_Upper_Right",
                "Female_05_Arm_Upper_Left",
                "Female_06_Arm_Lower_Right",
                "Female_07_Arm_Lower_Left",
                "Female_08_Hand_Right",
                "Female_09_Hand_Left");
            m_defaultMaleLegs = FindDefaultChildren(
                m_modularCharacterRoot,
                "Male_10_Hips",
                "Male_11_Leg_Right",
                "Male_12_Leg_Left");
            m_defaultFemaleLegs = FindDefaultChildren(
                m_modularCharacterRoot,
                "Female_10_Hips",
                "Female_11_Leg_Right",
                "Female_12_Leg_Left");
        }

        /// <summary>Switches the active master body hierarchy and restores default features.</summary>
        public void ToggleBodyType(bool isMale)
        {
            m_isMale = isMale;
            m_maleParts?.gameObject.SetActive(isMale);
            m_femaleParts?.gameObject.SetActive(!isMale);
            EnableBody(true);
            EnableArms(true);
            EnableLegs(true);
            ResetHeadFeatures();
        }

        /// <summary>Restores the selected body's default head, hair, and facial hair.</summary>
        public void ResetHeadFeatures()
        {
            EnableHead(true);
            EnableHair(true);
            EnableFacialHair(m_isMale);
        }

        /// <summary>Applies visibility rules associated with one equipped head item.</summary>
        public void ApplyHeadEquipmentType(HeadEquipmentType equipmentType)
        {
            ResetHeadFeatures();
            switch (equipmentType)
            {
                case HeadEquipmentType.FullHelmet:
                    EnableHead(false);
                    EnableHair(false);
                    break;
                case HeadEquipmentType.Hood:
                    EnableHair(false);
                    break;
                case HeadEquipmentType.FaceCover:
                    EnableFacialHair(false);
                    break;
                default:
                    break;
            }
        }

        /// <summary>Enables or disables the selected body's default head mesh.</summary>
        public void EnableHead(bool isEnabled)
        {
            m_defaultMaleHead?.SetActive(isEnabled && m_isMale);
            m_defaultFemaleHead?.SetActive(isEnabled && !m_isMale);
        }

        /// <summary>Enables or disables the selected hairstyle without changing its selection.</summary>
        public void EnableHair(bool isEnabled)
        {
            m_isHairVisible = isEnabled;
            RefreshHairPresentation();
        }

        /// <summary>Selects a hairstyle and refreshes its replicated presentation.</summary>
        public void SetHairstyle(int hairstyleID)
        {
            int maximumIndex = Mathf.Max(0, HairstyleCount - 1);
            m_hairstyleID = Mathf.Clamp(hairstyleID, 0, maximumIndex);
            RefreshHairPresentation();
        }

        /// <summary>Applies byte color channels to every renderer in the active hairstyle.</summary>
        public void SetHairColor(int red, int green, int blue)
        {
            m_hairColor = new Color32(
                (byte)Mathf.Clamp(red, 0, 255),
                (byte)Mathf.Clamp(green, 0, 255),
                (byte)Mathf.Clamp(blue, 0, 255),
                255);
            ApplyHairColor();
        }

        /// <summary>Enables or disables the male default facial-hair mesh.</summary>
        public void EnableFacialHair(bool isEnabled)
        {
            m_defaultFacialHair?.SetActive(isEnabled && m_isMale);
        }

        /// <summary>Enables or disables the selected body's default torso mesh.</summary>
        public void EnableBody(bool isEnabled)
        {
            SetBodyTypeModelsActive(
                m_defaultMaleBody,
                m_defaultFemaleBody,
                isEnabled);
        }

        /// <summary>Enables or disables the selected body's default arm and hand meshes.</summary>
        public void EnableArms(bool isEnabled)
        {
            SetBodyTypeModelsActive(
                m_defaultMaleArms,
                m_defaultFemaleArms,
                isEnabled);
        }

        /// <summary>Enables or disables the selected body's default hips and leg meshes.</summary>
        public void EnableLegs(bool isEnabled)
        {
            SetBodyTypeModelsActive(
                m_defaultMaleLegs,
                m_defaultFemaleLegs,
                isEnabled);
        }

        private static Transform FindDescendant(Transform root, string objectName)
        {
            if (root == null)
            {
                return null;
            }

            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name == objectName)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static GameObject FindDefaultChild(Transform group)
        {
            if (group == null)
            {
                return null;
            }

            Transform activeFallback = null;
            foreach (Transform child in group)
            {
                if (child.name.EndsWith("_00", StringComparison.Ordinal))
                {
                    return child.gameObject;
                }

                if (activeFallback == null && child.gameObject.activeSelf)
                {
                    activeFallback = child;
                }
            }

            return activeFallback != null ? activeFallback.gameObject : null;
        }

        private static GameObject[] FindDefaultChildren(
            Transform root,
            params string[] groupNames)
        {
            GameObject[] defaultModels = new GameObject[groupNames.Length];
            for (int groupIndex = 0; groupIndex < groupNames.Length; groupIndex++)
            {
                defaultModels[groupIndex] = FindDefaultChild(
                    FindDescendant(root, groupNames[groupIndex]));
            }

            return defaultModels;
        }

        private void InitializeHairObjects(Transform hairGroup)
        {
            if (m_hairObjects != null && m_hairObjects.Length > 1)
            {
                return;
            }

            if (hairGroup == null)
            {
                m_hairObjects = new GameObject[] { null };
                return;
            }

            m_hairObjects = new GameObject[hairGroup.childCount + 1];
            for (int childIndex = 0; childIndex < hairGroup.childCount; childIndex++)
            {
                m_hairObjects[childIndex + 1] = hairGroup.GetChild(childIndex).gameObject;
            }
        }

        private void RefreshHairPresentation()
        {
            if (m_hairObjects == null)
            {
                return;
            }

            for (int hairstyleIndex = 0;
                 hairstyleIndex < m_hairObjects.Length;
                 hairstyleIndex++)
            {
                GameObject hairObject = m_hairObjects[hairstyleIndex];
                if (hairObject == null)
                {
                    continue;
                }

                hairObject.SetActive(
                    m_isHairVisible &&
                    hairstyleIndex > 0 &&
                    hairstyleIndex == m_hairstyleID);
            }

            ApplyHairColor();
        }

        private void ApplyHairColor()
        {
            if (m_hairObjects == null ||
                m_hairstyleID <= 0 ||
                m_hairstyleID >= m_hairObjects.Length ||
                m_hairObjects[m_hairstyleID] == null)
            {
                return;
            }

            foreach (Renderer hairRenderer in
                     m_hairObjects[m_hairstyleID].GetComponentsInChildren<Renderer>(true))
            {
                Material sharedMaterial = hairRenderer.sharedMaterial;
                if (sharedMaterial == null)
                {
                    continue;
                }

                string colorProperty = GetHairColorProperty(sharedMaterial);
                if (string.IsNullOrEmpty(colorProperty))
                {
                    continue;
                }

                MaterialPropertyBlock propertyBlock = new();
                hairRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(colorProperty, m_hairColor);
                hairRenderer.SetPropertyBlock(propertyBlock);
            }
        }

        private static string GetHairColorProperty(Material material)
        {
            if (material.HasProperty("_BaseColor"))
            {
                return "_BaseColor";
            }

            if (material.HasProperty("_Color"))
            {
                return "_Color";
            }

            if (material.HasProperty("_Base_Color"))
            {
                return "_Base_Color";
            }

            return string.Empty;
        }

        private void SetBodyTypeModelsActive(
            GameObject[] maleModels,
            GameObject[] femaleModels,
            bool isEnabled)
        {
            SetModelsActive(maleModels, isEnabled && m_isMale);
            SetModelsActive(femaleModels, isEnabled && !m_isMale);
        }

        private static void SetModelsActive(GameObject[] models, bool isEnabled)
        {
            foreach (GameObject model in models)
            {
                model?.SetActive(isEnabled);
            }
        }
    }
}
