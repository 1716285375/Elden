using System;
using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Owns player quick slots and per-character runtime weapon item copies.
    /// </summary>
    [RequireComponent(typeof(PlayerManager))]
    [RequireComponent(typeof(PlayerEquipmentManager))]
    public class PlayerInventoryManager : CharacterInventoryManager
    {
        private const int k_QuickSlotCount = 3;

        [Header("Fallback Weapon")]
        [SerializeField] private WeaponItem m_unarmedWeapon;

        [Header("Right Hand Quick Slots")]
        [SerializeField] private WeaponItem[] m_weaponsInRightHandSlots =
            new WeaponItem[k_QuickSlotCount];

        [Header("Left Hand Quick Slots")]
        [SerializeField] private WeaponItem[] m_weaponsInLeftHandSlots =
            new WeaponItem[k_QuickSlotCount];

        private PlayerManager m_player;
        private PlayerEquipmentManager m_equipmentManager;
        private WeaponItem m_currentRightHandWeapon;
        private WeaponItem m_currentLeftHandWeapon;
        private int m_rightHandWeaponIndex;
        private int m_leftHandWeaponIndex;

        /// <summary>Gets the runtime copy currently selected for the right hand.</summary>
        public WeaponItem CurrentRightHandWeapon => m_currentRightHandWeapon;

        /// <summary>Gets the runtime copy currently selected for the left hand.</summary>
        public WeaponItem CurrentLeftHandWeapon => m_currentLeftHandWeapon;

        /// <summary>Raised after the right-hand runtime weapon and model are refreshed.</summary>
        public event Action<WeaponItem> RightHandWeaponChanged;

        /// <summary>Raised after the left-hand runtime weapon and model are refreshed.</summary>
        public event Action<WeaponItem> LeftHandWeaponChanged;

        /// <summary>Resolves one currently equipped runtime weapon for animation updates.</summary>
        public WeaponItem GetEquippedWeaponByID(int weaponID)
        {
            if (m_currentRightHandWeapon?.ItemID == weaponID)
            {
                return m_currentRightHandWeapon;
            }

            return m_currentLeftHandWeapon?.ItemID == weaponID
                ? m_currentLeftHandWeapon
                : null;
        }

        protected override void Awake()
        {
            base.Awake();
            m_player = GetComponent<PlayerManager>();
            m_equipmentManager = GetComponent<PlayerEquipmentManager>();
        }

        private void OnDestroy()
        {
            DestroyRuntimeWeapon(m_currentRightHandWeapon);
            DestroyRuntimeWeapon(m_currentLeftHandWeapon);
        }

        /// <summary>
        /// Selects the next right-hand quick-slot weapon for the locally owned player.
        /// </summary>
        public void SwitchRightWeapon()
        {
            if (!CanSwitchWeapons())
            {
                return;
            }

            WeaponItem nextWeapon = SelectNextWeapon(
                m_weaponsInRightHandSlots,
                m_currentRightHandWeapon,
                ref m_rightHandWeaponIndex);
            if (nextWeapon == null ||
                nextWeapon.ItemID == m_player.PlayerNetworkManager.CurrentRightHandWeaponID.Value)
            {
                return;
            }

            m_player.PlayerNetworkManager.CurrentRightHandWeaponID.Value = nextWeapon.ItemID;
        }

        /// <summary>
        /// Selects the next left-hand quick-slot weapon for the locally owned player.
        /// </summary>
        public void SwitchLeftWeapon()
        {
            if (!CanSwitchWeapons())
            {
                return;
            }

            WeaponItem nextWeapon = SelectNextWeapon(
                m_weaponsInLeftHandSlots,
                m_currentLeftHandWeapon,
                ref m_leftHandWeaponIndex);
            if (nextWeapon == null ||
                nextWeapon.ItemID == m_player.PlayerNetworkManager.CurrentLeftHandWeaponID.Value)
            {
                return;
            }

            m_player.PlayerNetworkManager.CurrentLeftHandWeaponID.Value = nextWeapon.ItemID;
        }

        /// <summary>
        /// Reconstructs the initial right-hand weapon without playing a swap animation.
        /// </summary>
        public void InitializeRightWeaponFromID(int weaponID)
        {
            TryEquipRightWeaponFromID(weaponID);
        }

        /// <summary>
        /// Reconstructs a changed right-hand weapon and presents the swap animation.
        /// </summary>
        public void EquipRightWeaponFromID(int weaponID)
        {
            if (TryEquipRightWeaponFromID(weaponID))
            {
                m_player.PlayerAnimatorManager?.PlayWeaponSwapAnimation(
                    WeaponModelSlot.RightHandSlot);
            }
        }

        /// <summary>
        /// Reconstructs the initial left-hand weapon without playing a swap animation.
        /// </summary>
        public void InitializeLeftWeaponFromID(int weaponID)
        {
            TryEquipLeftWeaponFromID(weaponID);
        }

        /// <summary>
        /// Reconstructs a changed left-hand weapon and presents the swap animation.
        /// </summary>
        public void EquipLeftWeaponFromID(int weaponID)
        {
            if (TryEquipLeftWeaponFromID(weaponID))
            {
                m_player.PlayerAnimatorManager?.PlayWeaponSwapAnimation(
                    WeaponModelSlot.LeftHandSlot);
            }
        }

        private bool TryEquipRightWeaponFromID(int weaponID)
        {
            WeaponItem runtimeWeapon = CreateRuntimeWeapon(weaponID);
            if (runtimeWeapon == null)
            {
                return false;
            }

            DestroyRuntimeWeapon(m_currentRightHandWeapon);
            m_currentRightHandWeapon = runtimeWeapon;
            SynchronizeQuickSlotIndex(
                m_weaponsInRightHandSlots,
                runtimeWeapon.ItemID,
                ref m_rightHandWeaponIndex);
            m_equipmentManager.LoadRightWeapon(runtimeWeapon);
            RightHandWeaponChanged?.Invoke(runtimeWeapon);
            return true;
        }

        private bool TryEquipLeftWeaponFromID(int weaponID)
        {
            WeaponItem runtimeWeapon = CreateRuntimeWeapon(weaponID);
            if (runtimeWeapon == null)
            {
                return false;
            }

            DestroyRuntimeWeapon(m_currentLeftHandWeapon);
            m_currentLeftHandWeapon = runtimeWeapon;
            SynchronizeQuickSlotIndex(
                m_weaponsInLeftHandSlots,
                runtimeWeapon.ItemID,
                ref m_leftHandWeaponIndex);
            m_equipmentManager.LoadLeftWeapon(runtimeWeapon);
            LeftHandWeaponChanged?.Invoke(runtimeWeapon);
            return true;
        }

        private bool CanSwitchWeapons()
        {
            return m_player != null &&
                m_player.IsSpawned &&
                m_player.IsOwner &&
                !m_player.IsDead &&
                m_player.PlayerNetworkManager != null;
        }

        private WeaponItem CreateRuntimeWeapon(int weaponID)
        {
            WeaponItem template = ResolveWeaponTemplate(weaponID) ?? m_unarmedWeapon;
            if (template == null)
            {
                Debug.LogError(
                    $"Could not resolve weapon ID {weaponID} and no Unarmed fallback is assigned.",
                    this);
                return null;
            }

            WeaponItem runtimeWeapon = Instantiate(template);
            runtimeWeapon.name = $"{template.name} (Runtime)";
            runtimeWeapon.hideFlags = HideFlags.DontSave;
            return runtimeWeapon;
        }

        private WeaponItem ResolveWeaponTemplate(int weaponID)
        {
            WeaponItem databaseWeapon = WorldItemDatabase.Instance?.GetWeaponByID(weaponID);
            if (databaseWeapon != null)
            {
                return databaseWeapon;
            }

            WeaponItem quickSlotWeapon = FindWeaponByID(
                m_weaponsInRightHandSlots,
                weaponID) ?? FindWeaponByID(m_weaponsInLeftHandSlots, weaponID);
            if (quickSlotWeapon != null)
            {
                Debug.LogWarning(
                    $"WorldItemDatabase is unavailable; resolved weapon ID {weaponID} from the local inventory.",
                    this);
                return quickSlotWeapon;
            }

            return m_unarmedWeapon != null && m_unarmedWeapon.ItemID == weaponID
                ? m_unarmedWeapon
                : null;
        }

        private WeaponItem SelectNextWeapon(
            WeaponItem[] quickSlots,
            WeaponItem currentWeapon,
            ref int currentIndex)
        {
            int realWeaponCount = CountUniqueRealWeapons(quickSlots);
            if (realWeaponCount == 0)
            {
                return m_unarmedWeapon;
            }

            if (realWeaponCount == 1)
            {
                WeaponItem realWeapon = FindFirstRealWeapon(quickSlots, out int realWeaponIndex);
                currentIndex = realWeaponIndex;
                return currentWeapon != null && currentWeapon.ItemID == realWeapon.ItemID
                    ? m_unarmedWeapon
                    : realWeapon;
            }

            for (int slotOffset = 1; slotOffset <= quickSlots.Length; slotOffset++)
            {
                int slotIndex = (currentIndex + slotOffset) % quickSlots.Length;
                WeaponItem candidate = quickSlots[slotIndex];
                if (candidate == null || candidate.IsUnarmed)
                {
                    continue;
                }

                currentIndex = slotIndex;
                return candidate;
            }

            return m_unarmedWeapon;
        }

        private static int CountUniqueRealWeapons(WeaponItem[] quickSlots)
        {
            int weaponCount = 0;
            for (int slotIndex = 0; slotIndex < quickSlots.Length; slotIndex++)
            {
                WeaponItem candidate = quickSlots[slotIndex];
                if (candidate == null || candidate.IsUnarmed)
                {
                    continue;
                }

                bool isDuplicate = false;
                for (int previousIndex = 0; previousIndex < slotIndex; previousIndex++)
                {
                    WeaponItem previousWeapon = quickSlots[previousIndex];
                    if (previousWeapon != null && previousWeapon.ItemID == candidate.ItemID)
                    {
                        isDuplicate = true;
                        break;
                    }
                }

                if (!isDuplicate)
                {
                    weaponCount++;
                }
            }

            return weaponCount;
        }

        private static WeaponItem FindFirstRealWeapon(
            WeaponItem[] quickSlots,
            out int weaponIndex)
        {
            for (int slotIndex = 0; slotIndex < quickSlots.Length; slotIndex++)
            {
                WeaponItem candidate = quickSlots[slotIndex];
                if (candidate != null && !candidate.IsUnarmed)
                {
                    weaponIndex = slotIndex;
                    return candidate;
                }
            }

            weaponIndex = 0;
            return null;
        }

        private static WeaponItem FindWeaponByID(WeaponItem[] quickSlots, int weaponID)
        {
            foreach (WeaponItem weapon in quickSlots)
            {
                if (weapon != null && weapon.ItemID == weaponID)
                {
                    return weapon;
                }
            }

            return null;
        }

        private static void SynchronizeQuickSlotIndex(
            WeaponItem[] quickSlots,
            int weaponID,
            ref int currentIndex)
        {
            for (int slotIndex = 0; slotIndex < quickSlots.Length; slotIndex++)
            {
                WeaponItem weapon = quickSlots[slotIndex];
                if (weapon != null && weapon.ItemID == weaponID)
                {
                    currentIndex = slotIndex;
                    return;
                }
            }
        }

        private static void DestroyRuntimeWeapon(WeaponItem weapon)
        {
            if (weapon != null && (weapon.hideFlags & HideFlags.DontSave) != 0)
            {
                Destroy(weapon);
            }
        }
    }
}
