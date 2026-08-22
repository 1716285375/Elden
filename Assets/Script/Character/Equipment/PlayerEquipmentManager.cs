using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Discovers player hand slots and presents the weapons selected by inventory state.
    /// </summary>
    [RequireComponent(typeof(PlayerManager))]
    [RequireComponent(typeof(PlayerBodyManager))]
    public class PlayerEquipmentManager : CharacterEquipmentManager
    {
        private WeaponModelInstantiationSlot m_rightHandSlot;
        private WeaponModelInstantiationSlot m_leftHandWeaponSlot;
        private WeaponModelInstantiationSlot m_leftHandShieldSlot;
        private WeaponModelInstantiationSlot m_backSlot;
        private WeaponModelInstantiationSlot m_hipSlot;
        private PlayerManager m_player;
        private CharacterSoundFXManager m_characterSoundFXManager;
        private PlayerBodyManager m_playerBodyManager;
        private readonly Dictionary<EquipmentModelType, Dictionary<string, GameObject>>
            m_armorModels = new();
        private HeadEquipmentItem m_loadedHeadEquipment;
        private BodyEquipmentItem m_loadedBodyEquipment;
        private HandEquipmentItem m_loadedHandEquipment;
        private LegEquipmentItem m_loadedLegEquipment;

        /// <summary>Gets the weapon manager of the currently loaded right-hand weapon model.</summary>
        public WeaponManager CurrentRightHandWeaponManager { get; private set; }

        /// <summary>Gets the weapon manager of the currently loaded left-hand weapon model.</summary>
        public WeaponManager CurrentLeftHandWeaponManager { get; private set; }

        /// <summary>Gets the weapon manager instantiated in the animation-compatible right hand.</summary>
        public WeaponManager CurrentTwoHandWeaponManager { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            m_player = GetComponent<PlayerManager>();
            m_characterSoundFXManager =
                GetComponentInChildren<CharacterSoundFXManager>(true);
            m_playerBodyManager = GetComponent<PlayerBodyManager>();
            m_playerBodyManager?.InitializeBodyModels();
            DiscoverWeaponSlots();
            InitializeArmorModels();
        }

        /// <summary>Discovers every embedded modular armor model by category and object name.</summary>
        public void InitializeArmorModels()
        {
            m_armorModels.Clear();
            m_playerBodyManager ??= GetComponent<PlayerBodyManager>();
            m_playerBodyManager?.InitializeBodyModels();
            Transform modularRoot = m_playerBodyManager?.ModularCharacterRoot;
            if (modularRoot == null)
            {
                return;
            }

            foreach (Transform candidate in modularRoot.GetComponentsInChildren<Transform>(true))
            {
                if (!TryGetEquipmentModelType(candidate.name, out EquipmentModelType modelType))
                {
                    continue;
                }

                if (!m_armorModels.TryGetValue(modelType, out Dictionary<string, GameObject> models))
                {
                    models = new Dictionary<string, GameObject>(StringComparer.Ordinal);
                    m_armorModels.Add(modelType, models);
                }

                models[candidate.name] = candidate.gameObject;
            }
        }

        /// <summary>Loads one named embedded armor model and unloads its previous category.</summary>
        public bool LoadArmorModel(EquipmentModelType modelType, string modelName)
        {
            DisableArmorModelType(modelType);
            if (string.IsNullOrWhiteSpace(modelName) ||
                !m_armorModels.TryGetValue(modelType, out Dictionary<string, GameObject> models) ||
                !models.TryGetValue(modelName, out GameObject model))
            {
                RestoreDefaultArmorModel(modelType);
                Debug.LogWarning(
                    $"Could not resolve {modelType} armor model '{modelName}'.",
                    this);
                return false;
            }

            model.SetActive(true);
            return true;
        }

        /// <summary>Loads a head item after removing its prior models and feature rules.</summary>
        public void LoadHeadEquipment(HeadEquipmentItem equipment)
        {
            UnloadArmorItem(m_loadedHeadEquipment);
            m_playerBodyManager?.ResetHeadFeatures();
            m_loadedHeadEquipment = equipment;
            LoadArmorItem(equipment);
            if (equipment != null)
            {
                m_playerBodyManager?.ApplyHeadEquipmentType(equipment.HeadEquipmentType);
            }

            RecalculateArmorValues();
        }

        /// <summary>Loads a body item after removing its prior modular models.</summary>
        public void LoadBodyEquipment(BodyEquipmentItem equipment)
        {
            UnloadArmorItem(m_loadedBodyEquipment);
            m_loadedBodyEquipment = equipment;
            LoadArmorItem(equipment);
            RecalculateArmorValues();
        }

        /// <summary>Loads a hand item after removing its prior modular models.</summary>
        public void LoadHandEquipment(HandEquipmentItem equipment)
        {
            UnloadArmorItem(m_loadedHandEquipment);
            m_loadedHandEquipment = equipment;
            LoadArmorItem(equipment);
            RecalculateArmorValues();
        }

        /// <summary>Loads a leg item after removing its prior modular models.</summary>
        public void LoadLegEquipment(LegEquipmentItem equipment)
        {
            UnloadArmorItem(m_loadedLegEquipment);
            m_loadedLegEquipment = equipment;
            LoadArmorItem(equipment);
            RecalculateArmorValues();
        }

        /// <summary>Writes a head item ID into owner-authoritative replicated equipment state.</summary>
        public void EquipHeadEquipment(HeadEquipmentItem equipment)
        {
            if (CanWriteEquipment())
            {
                m_player.PlayerNetworkManager.CurrentHeadEquipmentID.Value =
                    equipment?.ItemID ?? -1;
            }
        }

        /// <summary>Writes a body item ID into owner-authoritative replicated equipment state.</summary>
        public void EquipBodyEquipment(BodyEquipmentItem equipment)
        {
            if (CanWriteEquipment())
            {
                m_player.PlayerNetworkManager.CurrentBodyEquipmentID.Value =
                    equipment?.ItemID ?? -1;
            }
        }

        /// <summary>Writes a hand item ID into owner-authoritative replicated equipment state.</summary>
        public void EquipHandEquipment(HandEquipmentItem equipment)
        {
            if (CanWriteEquipment())
            {
                m_player.PlayerNetworkManager.CurrentHandEquipmentID.Value =
                    equipment?.ItemID ?? -1;
            }
        }

        /// <summary>Writes a leg item ID into owner-authoritative replicated equipment state.</summary>
        public void EquipLegEquipment(LegEquipmentItem equipment)
        {
            if (CanWriteEquipment())
            {
                m_player.PlayerNetworkManager.CurrentLegEquipmentID.Value =
                    equipment?.ItemID ?? -1;
            }
        }

        /// <summary>Rebuilds gender roots and all equipped armor models for late join or body changes.</summary>
        public void RefreshArmorPresentation(bool isMale)
        {
            m_playerBodyManager?.ToggleBodyType(isMale);
            foreach (EquipmentModelType modelType in
                     Enum.GetValues(typeof(EquipmentModelType)))
            {
                DisableArmorModelType(modelType);
                RestoreDefaultArmorModel(modelType);
            }

            ReloadArmorItem(m_loadedHeadEquipment);
            ReloadArmorItem(m_loadedBodyEquipment);
            ReloadArmorItem(m_loadedHandEquipment);
            ReloadArmorItem(m_loadedLegEquipment);
            if (m_loadedHeadEquipment != null)
            {
                m_playerBodyManager?.ApplyHeadEquipmentType(
                    m_loadedHeadEquipment.HeadEquipmentType);
            }
        }

        /// <summary>
        /// Loads both currently selected inventory weapons into their independent hand slots.
        /// </summary>
        public void LoadWeaponsOnBothHands()
        {
            PlayerInventoryManager inventoryManager = GetComponent<PlayerInventoryManager>();
            LoadRightWeapon(inventoryManager?.CurrentRightHandWeapon);
            LoadLeftWeapon(inventoryManager?.CurrentLeftHandWeapon);
        }

        /// <summary>
        /// Loads the selected weapon into the right-hand model slot.
        /// </summary>
        public void LoadRightWeapon(WeaponItem weapon)
        {
            if (IsTwoHanding())
            {
                RefreshTwoHandingPresentation();
                return;
            }

            LoadRightWeaponInHand(weapon);
        }

        private void LoadRightWeaponInHand(WeaponItem weapon)
        {
            if (m_rightHandSlot == null)
            {
                Debug.LogError("The player prefab is missing a right-hand weapon slot.", this);
                return;
            }

            m_rightHandSlot.LoadWeaponModel(weapon, Character);
            CurrentRightHandWeaponManager = m_rightHandSlot.CurrentWeaponManager;
            if (m_player?.CharacterNetworkManager?.IsBlocking.Value != true)
            {
                m_player?.PlayerAnimatorManager?.UpdateAnimatorController(weapon);
            }
        }

        /// <summary>
        /// Loads the selected weapon into the left-hand model slot.
        /// </summary>
        public void LoadLeftWeapon(WeaponItem weapon)
        {
            if (IsTwoHanding())
            {
                RefreshTwoHandingPresentation();
                return;
            }

            LoadLeftWeaponInHand(weapon);
        }

        private void LoadLeftWeaponInHand(WeaponItem weapon)
        {
            WeaponModelInstantiationSlot targetSlot = weapon?.WeaponModelType ==
                    WeaponModelType.Shield
                ? m_leftHandShieldSlot
                : m_leftHandWeaponSlot;
            if (targetSlot == null)
            {
                Debug.LogError(
                    $"The player prefab is missing the {weapon?.WeaponModelType} left-hand slot.",
                    this);
                return;
            }

            m_leftHandWeaponSlot?.UnloadWeaponModel();
            m_leftHandShieldSlot?.UnloadWeaponModel();
            targetSlot.LoadWeaponModel(weapon, Character);
            CurrentLeftHandWeaponManager = targetSlot.CurrentWeaponManager;
            if (m_player?.PlayerNetworkManager?.IsUsingLeftHand.Value == true ||
                m_player?.CharacterNetworkManager?.IsBlocking.Value == true)
            {
                m_player.PlayerAnimatorManager?.UpdateAnimatorController(weapon);
                m_player.PlayerStatsManager?.SetBlockingStats(weapon);
            }
        }

        /// <summary>Presents the right-hand weapon in hand and stores the left-hand model.</summary>
        public void TwoHandRightWeapon()
        {
            PlayerInventoryManager inventory = m_player?.InventoryManager;
            if (inventory == null)
            {
                return;
            }

            UnloadAllWeaponSlots();
            LoadRightWeaponModel(inventory.CurrentRightHandWeapon);
            PlaceWeaponModelInUnequippedSlot(inventory.CurrentLeftHandWeapon);
            CurrentTwoHandWeaponManager = m_rightHandSlot?.CurrentWeaponManager;
            CurrentTwoHandWeaponManager?.SetWeaponDamage();
        }

        /// <summary>Presents the left weapon in the right hand and stores the right-hand model.</summary>
        public void TwoHandLeftWeapon()
        {
            PlayerInventoryManager inventory = m_player?.InventoryManager;
            if (inventory == null)
            {
                return;
            }

            UnloadAllWeaponSlots();
            LoadRightWeaponModel(inventory.CurrentLeftHandWeapon);
            PlaceWeaponModelInUnequippedSlot(inventory.CurrentRightHandWeapon);
            CurrentTwoHandWeaponManager = m_rightHandSlot?.CurrentWeaponManager;
            CurrentTwoHandWeaponManager?.SetWeaponDamage();
        }

        /// <summary>Restores both normal hand slots after leaving the two-hand stance.</summary>
        public void UnTwoHandWeapon()
        {
            PlayerInventoryManager inventory = m_player?.InventoryManager;
            UnloadAllWeaponSlots();
            CurrentTwoHandWeaponManager = null;
            if (inventory == null)
            {
                return;
            }

            LoadRightWeaponInHand(inventory.CurrentRightHandWeapon);
            LoadLeftWeaponInHand(inventory.CurrentLeftHandWeapon);
            CurrentRightHandWeaponManager?.SetWeaponDamage();
            CurrentLeftHandWeaponManager?.SetWeaponDamage();
            if (m_player?.CharacterNetworkManager?.IsBlocking.Value != true)
            {
                m_player?.PlayerAnimatorManager?.UpdateAnimatorController(
                    inventory.CurrentRightHandWeapon);
            }
        }

        /// <summary>Loads an unequipped weapon into the back or hip slot selected by class.</summary>
        public void PlaceWeaponModelInUnequippedSlot(WeaponItem weapon)
        {
            if (weapon == null || weapon.IsUnarmed)
            {
                return;
            }

            WeaponModelInstantiationSlot targetSlot = weapon.WeaponClass == WeaponClass.Dagger
                ? m_hipSlot
                : m_backSlot;
            if (targetSlot == null)
            {
                Debug.LogError(
                    $"The player prefab is missing a storage slot for {weapon.WeaponClass}.",
                    this);
                return;
            }

            GetUnequippedPlacement(
                weapon.WeaponClass,
                out Vector3 localPosition,
                out Vector3 localEulerRotation);
            targetSlot.LoadWeaponModelAtPlacement(
                weapon,
                Character,
                localPosition,
                localEulerRotation,
                weapon.WeaponPivotScale);
        }

        /// <summary>Replays the synchronized side selection after equipment arrives.</summary>
        public void RefreshTwoHandingPresentation()
        {
            PlayerNetworkManager networkManager = m_player?.PlayerNetworkManager;
            if (networkManager?.IsTwoHandingRightWeapon.Value == true)
            {
                TwoHandRightWeapon();
            }
            else if (networkManager?.IsTwoHandingLeftWeapon.Value == true)
            {
                TwoHandLeftWeapon();
            }
        }

        /// <summary>
        /// Enables the current action-hand weapon's damage window on the locally owned player.
        /// </summary>
        public void OpenDamageCollider()
        {
            WeaponManager weaponManager = GetCurrentWeaponManager();
            m_characterSoundFXManager?.PlayWeaponWhoosh(weaponManager?.Weapon);
            if (m_player == null || !m_player.IsOwner)
            {
                return;
            }

            if (weaponManager == null)
            {
                return;
            }

            weaponManager.SetAttackType(m_player.PlayerCombatManager.CurrentAttackType);
            weaponManager.OpenDamageCollider();
        }

        /// <summary>
        /// Ends the current action-hand weapon's damage window.
        /// </summary>
        public void CloseDamageCollider()
        {
            GetCurrentWeaponManager()?.CloseDamageCollider();
        }

        private WeaponManager GetCurrentWeaponManager()
        {
            if (IsTwoHanding())
            {
                return CurrentTwoHandWeaponManager;
            }

            bool isUsingRightHand = m_player?.PlayerNetworkManager == null ||
                m_player.PlayerNetworkManager.IsUsingRightHand.Value;
            return isUsingRightHand
                ? CurrentRightHandWeaponManager
                : CurrentLeftHandWeaponManager;
        }

        private void DiscoverWeaponSlots()
        {
            foreach (WeaponModelInstantiationSlot slot in
                     GetComponentsInChildren<WeaponModelInstantiationSlot>(true))
            {
                if (slot.WeaponModelSlot == WeaponModelSlot.RightHandSlot)
                {
                    m_rightHandSlot = slot;
                }
                else if (slot.WeaponModelSlot == WeaponModelSlot.LeftHandSlot)
                {
                    m_leftHandWeaponSlot = slot;
                }
                else if (slot.WeaponModelSlot == WeaponModelSlot.LeftHandShieldSlot)
                {
                    m_leftHandShieldSlot = slot;
                }
                else if (slot.WeaponModelSlot == WeaponModelSlot.BackSlot)
                {
                    m_backSlot = slot;
                }
                else if (slot.WeaponModelSlot == WeaponModelSlot.HipSlot)
                {
                    m_hipSlot = slot;
                }
            }
        }

        private void LoadArmorItem(ArmorItem armorItem)
        {
            if (armorItem == null)
            {
                return;
            }

            armorItem.OnItemEquipped(Character);
            foreach (EquipmentModel equipmentModel in armorItem.EquipmentModels)
            {
                equipmentModel?.LoadModel(m_player, m_playerBodyManager?.IsMale != false);
            }
        }

        private void UnloadArmorItem(ArmorItem armorItem)
        {
            if (armorItem == null)
            {
                return;
            }

            armorItem.OnItemUnequipped(Character);
            foreach (EquipmentModel equipmentModel in armorItem.EquipmentModels)
            {
                if (equipmentModel == null)
                {
                    continue;
                }

                DisableArmorModelType(equipmentModel.EquipmentModelType);
                RestoreDefaultArmorModel(equipmentModel.EquipmentModelType);
            }
        }

        private void ReloadArmorItem(ArmorItem armorItem)
        {
            if (armorItem == null)
            {
                return;
            }

            foreach (EquipmentModel equipmentModel in armorItem.EquipmentModels)
            {
                equipmentModel?.LoadModel(m_player, m_playerBodyManager?.IsMale != false);
            }
        }

        private void RecalculateArmorValues()
        {
            PlayerInventoryManager inventory = m_player?.InventoryManager;
            m_player?.PlayerStatsManager?.CalculateTotalArmorValues(
                inventory?.CurrentHeadEquipment,
                inventory?.CurrentBodyEquipment,
                inventory?.CurrentHandEquipment,
                inventory?.CurrentLegEquipment);
        }

        private void DisableArmorModelType(EquipmentModelType modelType)
        {
            if (!m_armorModels.TryGetValue(modelType, out Dictionary<string, GameObject> models))
            {
                return;
            }

            foreach (GameObject model in models.Values)
            {
                model.SetActive(false);
            }
        }

        private void RestoreDefaultArmorModel(EquipmentModelType modelType)
        {
            if (modelType == EquipmentModelType.HeadCovering ||
                !m_armorModels.TryGetValue(modelType, out Dictionary<string, GameObject> models))
            {
                return;
            }

            bool isMale = m_playerBodyManager?.IsMale != false;
            foreach (KeyValuePair<string, GameObject> pair in models)
            {
                bool matchesBodyType = isMale
                    ? pair.Key.Contains("_Male_", StringComparison.Ordinal)
                    : pair.Key.Contains("_Female_", StringComparison.Ordinal);
                pair.Value.SetActive(
                    matchesBodyType && pair.Key.EndsWith("_00", StringComparison.Ordinal));
            }
        }

        private bool CanWriteEquipment()
        {
            return m_player != null &&
                m_player.IsSpawned &&
                m_player.IsOwner &&
                m_player.PlayerNetworkManager != null;
        }

        private static bool TryGetEquipmentModelType(
            string modelName,
            out EquipmentModelType modelType)
        {
            if (modelName.StartsWith("Chr_HeadCoverings_", StringComparison.Ordinal))
            {
                modelType = EquipmentModelType.HeadCovering;
                return true;
            }

            (string Prefix, EquipmentModelType Type)[] mappings =
            {
                ("Chr_Torso_", EquipmentModelType.Torso),
                ("Chr_ArmUpperRight_", EquipmentModelType.UpperRightArm),
                ("Chr_ArmUpperLeft_", EquipmentModelType.UpperLeftArm),
                ("Chr_ArmLowerRight_", EquipmentModelType.LowerRightArm),
                ("Chr_ArmLowerLeft_", EquipmentModelType.LowerLeftArm),
                ("Chr_HandRight_", EquipmentModelType.RightHand),
                ("Chr_HandLeft_", EquipmentModelType.LeftHand),
                ("Chr_Hips_", EquipmentModelType.Hips),
                ("Chr_LegRight_", EquipmentModelType.RightLeg),
                ("Chr_LegLeft_", EquipmentModelType.LeftLeg)
            };
            foreach ((string prefix, EquipmentModelType type) in mappings)
            {
                if (modelName.StartsWith(prefix, StringComparison.Ordinal))
                {
                    modelType = type;
                    return true;
                }
            }

            modelType = default;
            return false;
        }

        private void LoadRightWeaponModel(WeaponItem weapon)
        {
            if (m_rightHandSlot == null || weapon == null)
            {
                return;
            }

            m_rightHandSlot.LoadWeaponModel(weapon, Character);
            CurrentRightHandWeaponManager = m_rightHandSlot.CurrentWeaponManager;
        }

        private void UnloadAllWeaponSlots()
        {
            m_rightHandSlot?.UnloadWeaponModel();
            m_leftHandWeaponSlot?.UnloadWeaponModel();
            m_leftHandShieldSlot?.UnloadWeaponModel();
            m_backSlot?.UnloadWeaponModel();
            m_hipSlot?.UnloadWeaponModel();
            CurrentRightHandWeaponManager = null;
            CurrentLeftHandWeaponManager = null;
        }

        private bool IsTwoHanding()
        {
            return m_player?.PlayerNetworkManager?.IsTwoHandingWeapon.Value == true;
        }

        private static void GetUnequippedPlacement(
            WeaponClass weaponClass,
            out Vector3 localPosition,
            out Vector3 localEulerRotation)
        {
            if (weaponClass == WeaponClass.Dagger)
            {
                localPosition = new Vector3(0.12f, -0.08f, 0.04f);
                localEulerRotation = new Vector3(10f, 0f, 185f);
                return;
            }

            localPosition = weaponClass == WeaponClass.Shield
                ? new Vector3(0f, 0.02f, -0.14f)
                : new Vector3(0.12f, 0.03f, -0.08f);
            localEulerRotation = weaponClass == WeaponClass.Shield
                ? new Vector3(0f, 180f, 0f)
                : new Vector3(0f, 45f, 90f);
        }
    }
}
