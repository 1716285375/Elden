using System;
using System.Collections.Generic;
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

        [Header("Armor Equipment")]
        [SerializeField] private HeadEquipmentItem m_startingHeadEquipment;
        [SerializeField] private BodyEquipmentItem m_startingBodyEquipment;
        [SerializeField] private HandEquipmentItem m_startingHandEquipment;
        [SerializeField] private LegEquipmentItem m_startingLegEquipment;

        [Header("Spell Slot")]
        [SerializeField] private SpellItem m_startingSpell;

        [Header("Projectile Slots")]
        [SerializeField] private RangedProjectileItem m_startingMainProjectile;
        [SerializeField] private RangedProjectileItem m_startingSecondaryProjectile;

        [Header("Quick Slot Item")]
        [SerializeField] private QuickSlotItem m_startingQuickSlotItem;
        [SerializeField] private QuickSlotItem[] m_quickSlotItemsInQuickSlots =
            new QuickSlotItem[k_QuickSlotCount];
        [SerializeField, Range(0, k_QuickSlotCount - 1)]
        private int m_quickSlotItemIndex;

        [Header("Runtime Inventory")]
        [SerializeField] private List<Item> m_itemsInInventory = new();

        private PlayerManager m_player;
        private PlayerEquipmentManager m_equipmentManager;
        private WeaponItem m_currentRightHandWeapon;
        private WeaponItem m_currentLeftHandWeapon;
        private WeaponItem m_currentTwoHandWeapon;
        private HeadEquipmentItem m_currentHeadEquipment;
        private BodyEquipmentItem m_currentBodyEquipment;
        private HandEquipmentItem m_currentHandEquipment;
        private LegEquipmentItem m_currentLegEquipment;
        private SpellItem m_currentSpell;
        private RangedProjectileItem m_mainProjectile;
        private RangedProjectileItem m_secondaryProjectile;
        private QuickSlotItem m_currentQuickSlotItem;
        private int m_rightHandWeaponIndex;
        private int m_leftHandWeaponIndex;

        /// <summary>Gets the runtime copy currently selected for the right hand.</summary>
        public WeaponItem CurrentRightHandWeapon => m_currentRightHandWeapon;

        /// <summary>Gets the runtime copy currently selected for the left hand.</summary>
        public WeaponItem CurrentLeftHandWeapon => m_currentLeftHandWeapon;

        /// <summary>Gets the equipped runtime item currently presented in the two-hand stance.</summary>
        public WeaponItem CurrentTwoHandWeapon => m_currentTwoHandWeapon;

        /// <summary>Gets the runtime armor copy currently equipped in the head slot.</summary>
        public HeadEquipmentItem CurrentHeadEquipment => m_currentHeadEquipment;

        /// <summary>Gets the runtime armor copy currently equipped in the body slot.</summary>
        public BodyEquipmentItem CurrentBodyEquipment => m_currentBodyEquipment;

        /// <summary>Gets the runtime armor copy currently equipped in the hand slot.</summary>
        public HandEquipmentItem CurrentHandEquipment => m_currentHandEquipment;

        /// <summary>Gets the runtime armor copy currently equipped in the leg slot.</summary>
        public LegEquipmentItem CurrentLegEquipment => m_currentLegEquipment;

        /// <summary>Gets the spell currently occupying the player's single spell slot.</summary>
        public SpellItem CurrentSpell => m_currentSpell;

        /// <summary>Gets the per-player runtime ammunition in the primary slot.</summary>
        public RangedProjectileItem MainProjectile => m_mainProjectile;

        /// <summary>Gets the per-player runtime ammunition in the secondary slot.</summary>
        public RangedProjectileItem SecondaryProjectile => m_secondaryProjectile;

        /// <summary>Gets the item currently assigned to the gameplay quick slot.</summary>
        public QuickSlotItem CurrentQuickSlotItem => m_currentQuickSlotItem;

        /// <summary>Gets the three equipped gameplay item slots.</summary>
        public IReadOnlyList<QuickSlotItem> QuickSlotItemsInQuickSlots =>
            m_quickSlotItemsInQuickSlots;

        /// <summary>Gets the item assets collected during the current play session.</summary>
        public IReadOnlyList<Item> ItemsInInventory => m_itemsInInventory;

        /// <summary>Gets the selected right-hand quick-slot index.</summary>
        public int RightHandWeaponIndex => m_rightHandWeaponIndex;

        /// <summary>Gets the selected left-hand quick-slot index.</summary>
        public int LeftHandWeaponIndex => m_leftHandWeaponIndex;

        /// <summary>Gets the selected gameplay item slot index.</summary>
        public int QuickSlotItemIndex => m_quickSlotItemIndex;

        /// <summary>Raised after the right-hand runtime weapon and model are refreshed.</summary>
        public event Action<WeaponItem> RightHandWeaponChanged;

        /// <summary>Raised after the left-hand runtime weapon and model are refreshed.</summary>
        public event Action<WeaponItem> LeftHandWeaponChanged;

        /// <summary>Raised after synchronized spell selection is reconstructed.</summary>
        public event Action<SpellItem> CurrentSpellChanged;

        /// <summary>Raised after synchronized gameplay quick-slot selection is rebuilt.</summary>
        public event Action<QuickSlotItem> CurrentQuickSlotItemChanged;

        /// <summary>Raised after the primary ammunition item or amount changes.</summary>
        public event Action<RangedProjectileItem> MainProjectileChanged;

        /// <summary>Raised after the secondary ammunition item or amount changes.</summary>
        public event Action<RangedProjectileItem> SecondaryProjectileChanged;

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

        /// <summary>Sets the two-hand pointer without cloning or taking ownership of the item.</summary>
        public void SetCurrentTwoHandWeapon(WeaponItem weapon)
        {
            m_currentTwoHandWeapon = weapon;
        }

        /// <summary>Clears the non-owning pointer used by two-hand combat and presentation.</summary>
        public void ClearCurrentTwoHandWeapon()
        {
            m_currentTwoHandWeapon = null;
        }

        /// <summary>Adds one authored item to the runtime inventory.</summary>
        public bool AddItemToInventory(Item item)
        {
            if (item == null)
            {
                return false;
            }

            m_itemsInInventory ??= new List<Item>();
            m_itemsInInventory.Add(item);
            return true;
        }

        /// <summary>Removes one matching item and clears stale references.</summary>
        public bool RemoveItemFromInventory(Item item)
        {
            m_itemsInInventory ??= new List<Item>();
            m_itemsInInventory.RemoveAll(candidate => candidate == null);
            return item != null && m_itemsInInventory.Remove(item);
        }

        /// <summary>Gets the item currently occupying one Character Menu equipment slot.</summary>
        public Item GetEquipmentSlotItem(EquipmentSlotType equipmentSlot)
        {
            if (TryGetWeaponSlot(
                    equipmentSlot,
                    out WeaponItem[] weaponSlots,
                    out int slotIndex,
                    out _))
            {
                return slotIndex < weaponSlots.Length
                    ? weaponSlots[slotIndex]
                    : null;
            }

            if (TryGetQuickSlotItemIndex(equipmentSlot, out int quickSlotIndex))
            {
                EnsureQuickSlotItemArray();
                return m_quickSlotItemsInQuickSlots[quickSlotIndex];
            }

            return equipmentSlot switch
            {
                EquipmentSlotType.Head => m_currentHeadEquipment,
                EquipmentSlotType.Body => m_currentBodyEquipment,
                EquipmentSlotType.Leg => m_currentLegEquipment,
                EquipmentSlotType.Hand => m_currentHandEquipment,
                EquipmentSlotType.MainProjectile => m_mainProjectile,
                EquipmentSlotType.SecondaryProjectile => m_secondaryProjectile,
                _ => null
            };
        }

        /// <summary>
        /// Transfers one compatible inventory item into an equipment slot and synchronizes it.
        /// </summary>
        public bool EquipItemInSlot(EquipmentSlotType equipmentSlot, Item item)
        {
            m_itemsInInventory ??= new List<Item>();
            m_itemsInInventory.RemoveAll(candidate => candidate == null);
            if (item == null || !m_itemsInInventory.Contains(item))
            {
                return false;
            }

            if (TryGetWeaponSlot(
                    equipmentSlot,
                    out WeaponItem[] weaponSlots,
                    out int slotIndex,
                    out bool isRightHand))
            {
                return item is WeaponItem weapon &&
                    !weapon.IsUnarmed &&
                    EquipWeaponInSlot(
                        weaponSlots,
                        slotIndex,
                        isRightHand,
                        weapon);
            }

            if (item is RangedProjectileItem projectile &&
                IsProjectileSlot(equipmentSlot))
            {
                return EquipProjectileInSlot(equipmentSlot, projectile);
            }

            if (item is QuickSlotItem quickSlotItem &&
                TryGetQuickSlotItemIndex(equipmentSlot, out int quickSlotIndex))
            {
                return EquipQuickSlotItem(quickSlotIndex, quickSlotItem);
            }

            return equipmentSlot switch
            {
                EquipmentSlotType.Head when item is HeadEquipmentItem head =>
                    EquipArmorItem(
                        m_currentHeadEquipment,
                        head,
                        SetHeadEquipmentID),
                EquipmentSlotType.Body when item is BodyEquipmentItem body =>
                    EquipArmorItem(
                        m_currentBodyEquipment,
                        body,
                        SetBodyEquipmentID),
                EquipmentSlotType.Leg when item is LegEquipmentItem leg =>
                    EquipArmorItem(
                        m_currentLegEquipment,
                        leg,
                        SetLegEquipmentID),
                EquipmentSlotType.Hand when item is HandEquipmentItem hand =>
                    EquipArmorItem(
                        m_currentHandEquipment,
                        hand,
                        SetHandEquipmentID),
                _ => false
            };
        }

        /// <summary>
        /// Returns one equipped item to inventory and restores Unarmed or null for its slot.
        /// </summary>
        public bool UnequipItemInSlot(EquipmentSlotType equipmentSlot)
        {
            if (TryGetWeaponSlot(
                    equipmentSlot,
                    out WeaponItem[] weaponSlots,
                    out int slotIndex,
                    out bool isRightHand))
            {
                return UnequipWeaponInSlot(
                    weaponSlots,
                    slotIndex,
                    isRightHand);
            }

            if (IsProjectileSlot(equipmentSlot))
            {
                return UnequipProjectileInSlot(equipmentSlot);
            }

            if (TryGetQuickSlotItemIndex(equipmentSlot, out int quickSlotIndex))
            {
                return UnequipQuickSlotItem(quickSlotIndex);
            }

            return equipmentSlot switch
            {
                EquipmentSlotType.Head => UnequipArmorItem(
                    m_currentHeadEquipment,
                    SetHeadEquipmentID),
                EquipmentSlotType.Body => UnequipArmorItem(
                    m_currentBodyEquipment,
                    SetBodyEquipmentID),
                EquipmentSlotType.Leg => UnequipArmorItem(
                    m_currentLegEquipment,
                    SetLegEquipmentID),
                EquipmentSlotType.Hand => UnequipArmorItem(
                    m_currentHandEquipment,
                    SetHandEquipmentID),
                _ => false
            };
        }

        protected override void Awake()
        {
            base.Awake();
            m_player = GetComponent<PlayerManager>();
            m_equipmentManager = GetComponent<PlayerEquipmentManager>();
            m_currentSpell = m_startingSpell;
            EnsureQuickSlotItemArray();
            if (m_quickSlotItemsInQuickSlots[0] == null &&
                m_quickSlotItemsInQuickSlots[1] == null &&
                m_quickSlotItemsInQuickSlots[2] == null)
            {
                m_quickSlotItemsInQuickSlots[0] = m_startingQuickSlotItem;
            }

            m_quickSlotItemIndex = Mathf.Clamp(
                m_quickSlotItemIndex,
                0,
                k_QuickSlotCount - 1);
            m_currentQuickSlotItem =
                m_quickSlotItemsInQuickSlots[m_quickSlotItemIndex];
        }

        /// <summary>Returns the per-player ammunition copy selected by an input slot.</summary>
        public RangedProjectileItem GetProjectile(ProjectileSlot projectileSlot)
        {
            return projectileSlot == ProjectileSlot.Main
                ? m_mainProjectile
                : m_secondaryProjectile;
        }

        /// <summary>Reconstructs the primary ammunition slot from replicated state.</summary>
        public void InitializeMainProjectileFromID(
            int projectileID,
            int currentAmount = -1)
        {
            ReplaceRuntimeProjectile(
                ref m_mainProjectile,
                projectileID,
                currentAmount,
                m_startingMainProjectile);
            MainProjectileChanged?.Invoke(m_mainProjectile);
        }

        /// <summary>Reconstructs the secondary ammunition slot from replicated state.</summary>
        public void InitializeSecondaryProjectileFromID(
            int projectileID,
            int currentAmount = -1)
        {
            ReplaceRuntimeProjectile(
                ref m_secondaryProjectile,
                projectileID,
                currentAmount,
                m_startingSecondaryProjectile);
            SecondaryProjectileChanged?.Invoke(m_secondaryProjectile);
        }

        /// <summary>Notifies local UI after one equipped ammunition stack changes.</summary>
        public void NotifyProjectileAmountChanged(ProjectileSlot projectileSlot)
        {
            if (projectileSlot == ProjectileSlot.Main)
            {
                MainProjectileChanged?.Invoke(m_mainProjectile);
                return;
            }

            SecondaryProjectileChanged?.Invoke(m_secondaryProjectile);
        }

        /// <summary>Reconstructs the synchronized gameplay quick slot from its stable ID.</summary>
        public void InitializeCurrentQuickSlotItemFromID(int quickSlotItemID)
        {
            EnsureQuickSlotItemArray();
            QuickSlotItem quickSlotItem = m_player?.IsOwner == true
                ? ResolveOwnedQuickSlotItem(quickSlotItemID)
                : null;
            quickSlotItem ??= quickSlotItemID >= 0
                ? WorldItemDatabase.Instance?.GetQuickSlotItemByID(
                    quickSlotItemID)
                : null;
            quickSlotItem ??= FindQuickSlotItemByID(quickSlotItemID);
            if (quickSlotItem == null &&
                m_startingQuickSlotItem?.ItemID == quickSlotItemID)
            {
                quickSlotItem = m_startingQuickSlotItem;
            }

            m_currentQuickSlotItem = quickSlotItem;
            if (quickSlotItem != null)
            {
                SynchronizeQuickSlotItemIndex(
                    m_quickSlotItemsInQuickSlots,
                    quickSlotItem.ItemID,
                    ref m_quickSlotItemIndex);
            }

            CurrentQuickSlotItemChanged?.Invoke(m_currentQuickSlotItem);
        }

        /// <summary>Reconstructs the synchronized single spell slot from its stable ID.</summary>
        public void InitializeCurrentSpellFromID(int spellID)
        {
            SpellItem spell = WorldItemDatabase.Instance?.GetSpellByID(spellID);
            if (spell == null && m_startingSpell?.ItemID == spellID)
            {
                spell = m_startingSpell;
            }

            m_currentSpell = spell;
            CurrentSpellChanged?.Invoke(m_currentSpell);
        }

        /// <summary>Writes a new spell selection through owner-authoritative network state.</summary>
        public bool EquipCurrentSpell(SpellItem spell)
        {
            if (spell == null || m_player?.PlayerNetworkManager == null)
            {
                return false;
            }

            if (m_player.PlayerNetworkManager.IsSpawned && m_player.IsOwner)
            {
                m_player.PlayerNetworkManager.CurrentSpellID.Value = spell.ItemID;
                return true;
            }

            m_currentSpell = spell;
            CurrentSpellChanged?.Invoke(m_currentSpell);
            return true;
        }

        private void OnDestroy()
        {
            m_currentTwoHandWeapon = null;
            DestroyRuntimeWeapon(m_currentRightHandWeapon);
            DestroyRuntimeWeapon(m_currentLeftHandWeapon);
            DestroyRuntimeItem(m_currentHeadEquipment);
            DestroyRuntimeItem(m_currentBodyEquipment);
            DestroyRuntimeItem(m_currentHandEquipment);
            DestroyRuntimeItem(m_currentLegEquipment);
            DestroyRuntimeItem(m_mainProjectile);
            DestroyRuntimeItem(m_secondaryProjectile);
            DestroyRuntimeItems(m_weaponsInRightHandSlots);
            DestroyRuntimeItems(m_weaponsInLeftHandSlots);
            DestroyRuntimeItems(m_quickSlotItemsInQuickSlots);
            if (m_itemsInInventory == null)
            {
                return;
            }

            foreach (Item item in m_itemsInInventory)
            {
                DestroyRuntimeItem(item);
            }
        }

        /// <summary>Returns the stable item ID saved for one right-hand quick slot.</summary>
        public int GetRightHandQuickSlotItemID(int slotIndex)
        {
            return GetQuickSlotItemID(m_weaponsInRightHandSlots, slotIndex);
        }

        /// <summary>Returns one right-hand runtime weapon slot.</summary>
        public WeaponItem GetRightHandQuickSlotItem(int slotIndex)
        {
            return GetQuickSlotItem(m_weaponsInRightHandSlots, slotIndex);
        }

        /// <summary>Returns the stable item ID saved for one left-hand quick slot.</summary>
        public int GetLeftHandQuickSlotItemID(int slotIndex)
        {
            return GetQuickSlotItemID(m_weaponsInLeftHandSlots, slotIndex);
        }

        /// <summary>Returns one left-hand runtime weapon slot.</summary>
        public WeaponItem GetLeftHandQuickSlotItem(int slotIndex)
        {
            return GetQuickSlotItem(m_weaponsInLeftHandSlots, slotIndex);
        }

        /// <summary>Returns one gameplay quick-slot runtime item.</summary>
        public QuickSlotItem GetQuickSlotItem(int slotIndex)
        {
            EnsureQuickSlotItemArray();
            return slotIndex >= 0 && slotIndex < m_quickSlotItemsInQuickSlots.Length
                ? m_quickSlotItemsInQuickSlots[slotIndex]
                : null;
        }

        /// <summary>Restores all weapon quick slots, selected indices, and equipped weapons.</summary>
        public void RestoreWeaponLoadout(
            int[] rightHandItemIDs,
            int[] leftHandItemIDs,
            int rightHandIndex,
            int leftHandIndex)
        {
            RestoreWeaponLoadout(
                ConvertWeaponIDs(rightHandItemIDs),
                ConvertWeaponIDs(leftHandItemIDs),
                rightHandIndex,
                leftHandIndex);
        }

        /// <summary>
        /// Restores private weapon slots and selects the saved owner instances.
        /// </summary>
        public void RestoreWeaponLoadout(
            SerializableWeapon[] rightHandWeapons,
            SerializableWeapon[] leftHandWeapons,
            int rightHandIndex,
            int leftHandIndex)
        {
            RestoreQuickSlots(m_weaponsInRightHandSlots, rightHandWeapons);
            RestoreQuickSlots(m_weaponsInLeftHandSlots, leftHandWeapons);
            m_rightHandWeaponIndex = Mathf.Clamp(rightHandIndex, 0, k_QuickSlotCount - 1);
            m_leftHandWeaponIndex = Mathf.Clamp(leftHandIndex, 0, k_QuickSlotCount - 1);

            if (m_player?.PlayerNetworkManager == null || !m_player.IsOwner)
            {
                return;
            }

            m_player.PlayerNetworkManager.CurrentRightHandWeaponID.Value =
                GetQuickSlotItemID(m_weaponsInRightHandSlots, m_rightHandWeaponIndex);
            m_player.PlayerNetworkManager.CurrentLeftHandWeaponID.Value =
                GetQuickSlotItemID(m_weaponsInLeftHandSlots, m_leftHandWeaponIndex);
            InitializeRightWeaponFromID(
                m_player.PlayerNetworkManager.CurrentRightHandWeaponID.Value);
            InitializeLeftWeaponFromID(
                m_player.PlayerNetworkManager.CurrentLeftHandWeaponID.Value);
        }

        /// <summary>Restores both private ammunition instances and their network IDs.</summary>
        public void RestoreProjectileLoadout(
            SerializableRangeProjectile mainProjectile,
            SerializableRangeProjectile secondaryProjectile)
        {
            PlayerNetworkManager networkManager = m_player?.PlayerNetworkManager;
            if (networkManager != null && m_player.IsOwner)
            {
                networkManager.MainProjectileID.Value =
                    mainProjectile?.ItemID ?? -1;
                networkManager.SecondaryProjectileID.Value =
                    secondaryProjectile?.ItemID ?? -1;
            }

            ReplaceRuntimeProjectileFromSerializedData(
                ref m_mainProjectile,
                mainProjectile);
            ReplaceRuntimeProjectileFromSerializedData(
                ref m_secondaryProjectile,
                secondaryProjectile);
            MainProjectileChanged?.Invoke(m_mainProjectile);
            SecondaryProjectileChanged?.Invoke(m_secondaryProjectile);
        }

        /// <summary>
        /// Restores all gameplay quick slots before selecting and publishing the current item.
        /// </summary>
        public void RestoreQuickSlotLoadout(
            SerializableQuickSlotItem[] quickSlotItems,
            int quickSlotItemIndex)
        {
            EnsureQuickSlotItemArray();
            WorldItemDatabase database = WorldItemDatabase.Instance;
            for (int slotIndex = 0; slotIndex < k_QuickSlotCount; slotIndex++)
            {
                DestroyRuntimeItem(m_quickSlotItemsInQuickSlots[slotIndex]);
                SerializableQuickSlotItem savedItem =
                    quickSlotItems != null && slotIndex < quickSlotItems.Length
                        ? quickSlotItems[slotIndex]
                        : null;
                m_quickSlotItemsInQuickSlots[slotIndex] =
                    database?.GetQuickSlotItemFromSerializedData(savedItem);
            }

            m_quickSlotItemIndex = Mathf.Clamp(
                quickSlotItemIndex,
                0,
                k_QuickSlotCount - 1);
            int currentItemID = m_quickSlotItemsInQuickSlots[
                m_quickSlotItemIndex]?.ItemID ?? -1;
            PlayerNetworkManager networkManager = m_player?.PlayerNetworkManager;
            if (networkManager != null && m_player.IsOwner)
            {
                networkManager.CurrentQuickSlotItemID.Value = currentItemID;
            }

            InitializeCurrentQuickSlotItemFromID(currentItemID);
        }

        /// <summary>Clears and reconstructs the complete unequipped runtime inventory.</summary>
        public void RestoreInventory(CharacterSaveData saveData)
        {
            ClearRuntimeInventory();
            if (saveData == null || WorldItemDatabase.Instance == null)
            {
                return;
            }

            WorldItemDatabase database = WorldItemDatabase.Instance;
            AddRuntimeItems(saveData.WeaponsInInventory,
                database.GetWeaponFromSerializedData);
            AddRuntimeItems(saveData.ProjectilesInInventory,
                database.GetProjectileFromSerializedData);
            AddRuntimeItems(saveData.QuickSlotItemsInInventory,
                database.GetQuickSlotItemFromSerializedData);
            AddRuntimeItems(saveData.HeadEquipmentInInventory,
                database.GetRuntimeHeadEquipmentByID);
            AddRuntimeItems(saveData.BodyEquipmentInInventory,
                database.GetRuntimeBodyEquipmentByID);
            AddRuntimeItems(saveData.HandEquipmentInInventory,
                database.GetRuntimeHandEquipmentByID);
            AddRuntimeItems(saveData.LegEquipmentInInventory,
                database.GetRuntimeLegEquipmentByID);
        }

        /// <summary>Destroys every owned inventory instance and empties the list.</summary>
        public void ClearRuntimeInventory()
        {
            m_itemsInInventory ??= new List<Item>();
            foreach (Item item in m_itemsInInventory)
            {
                DestroyRuntimeItem(item);
            }

            m_itemsInInventory.Clear();
        }

        /// <summary>Reconstructs the initial armor presentation from synchronized IDs.</summary>
        public void InitializeArmorFromIDs(
            int headEquipmentID,
            int bodyEquipmentID,
            int handEquipmentID,
            int legEquipmentID)
        {
            EquipHeadEquipmentFromID(headEquipmentID);
            EquipBodyEquipmentFromID(bodyEquipmentID);
            EquipHandEquipmentFromID(handEquipmentID);
            EquipLegEquipmentFromID(legEquipmentID);
        }

        /// <summary>Reconstructs the runtime head item and its modular models.</summary>
        public void EquipHeadEquipmentFromID(int itemID)
        {
            HeadEquipmentItem previousItem = m_currentHeadEquipment;
            m_currentHeadEquipment = CreateRuntimeArmor(
                itemID,
                database => database.GetHeadEquipmentByID(itemID),
                m_startingHeadEquipment);
            m_equipmentManager?.LoadHeadEquipment(m_currentHeadEquipment);
            DestroyRuntimeItem(previousItem);
        }

        /// <summary>Reconstructs the runtime body item and its modular models.</summary>
        public void EquipBodyEquipmentFromID(int itemID)
        {
            BodyEquipmentItem previousItem = m_currentBodyEquipment;
            m_currentBodyEquipment = CreateRuntimeArmor(
                itemID,
                database => database.GetBodyEquipmentByID(itemID),
                m_startingBodyEquipment);
            m_equipmentManager?.LoadBodyEquipment(m_currentBodyEquipment);
            DestroyRuntimeItem(previousItem);
        }

        /// <summary>Reconstructs the runtime hand item and its modular models.</summary>
        public void EquipHandEquipmentFromID(int itemID)
        {
            HandEquipmentItem previousItem = m_currentHandEquipment;
            m_currentHandEquipment = CreateRuntimeArmor(
                itemID,
                database => database.GetHandEquipmentByID(itemID),
                m_startingHandEquipment);
            m_equipmentManager?.LoadHandEquipment(m_currentHandEquipment);
            DestroyRuntimeItem(previousItem);
        }

        /// <summary>Reconstructs the runtime leg item and its modular models.</summary>
        public void EquipLegEquipmentFromID(int itemID)
        {
            LegEquipmentItem previousItem = m_currentLegEquipment;
            m_currentLegEquipment = CreateRuntimeArmor(
                itemID,
                database => database.GetLegEquipmentByID(itemID),
                m_startingLegEquipment);
            m_equipmentManager?.LoadLegEquipment(m_currentLegEquipment);
            DestroyRuntimeItem(previousItem);
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

            m_player.PlayerNetworkManager.CancelTwoHanding();

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

            m_player.PlayerNetworkManager.CancelTwoHanding();

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

        /// <summary>Cycles the three gameplay item slots with empty-slot semantics.</summary>
        public void SwitchQuickSlotItem()
        {
            if (!CanSwitchQuickSlotItems())
            {
                return;
            }

            EnsureQuickSlotItemArray();
            QuickSlotItem nextItem = SelectNextQuickSlotItem(
                m_quickSlotItemsInQuickSlots,
                m_currentQuickSlotItem,
                ref m_quickSlotItemIndex);
            int nextItemID = nextItem?.ItemID ?? -1;
            if (nextItemID ==
                m_player.PlayerNetworkManager.CurrentQuickSlotItemID.Value)
            {
                return;
            }

            m_player.PlayerNetworkManager.CurrentQuickSlotItemID.Value = nextItemID;
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
                PlayWeaponSwapAnimationIfOneHanded(WeaponModelSlot.RightHandSlot);
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
                PlayWeaponSwapAnimationIfOneHanded(WeaponModelSlot.LeftHandSlot);
            }
        }

        private bool TryEquipRightWeaponFromID(int weaponID)
        {
            WeaponItem runtimeWeapon = CreateRuntimeWeapon(weaponID, true);
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
            RefreshTwoHandPointer(runtimeWeapon, true);
            RightHandWeaponChanged?.Invoke(runtimeWeapon);
            return true;
        }

        private bool TryEquipLeftWeaponFromID(int weaponID)
        {
            WeaponItem runtimeWeapon = CreateRuntimeWeapon(weaponID, false);
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
            RefreshTwoHandPointer(runtimeWeapon, false);
            LeftHandWeaponChanged?.Invoke(runtimeWeapon);
            return true;
        }

        private bool EquipWeaponInSlot(
            WeaponItem[] weaponSlots,
            int slotIndex,
            bool isRightHand,
            WeaponItem weapon)
        {
            if (weaponSlots == null ||
                slotIndex < 0 ||
                slotIndex >= weaponSlots.Length)
            {
                return false;
            }

            WeaponItem previousWeapon = weaponSlots[slotIndex];
            if (previousWeapon != null && !previousWeapon.IsUnarmed)
            {
                AddItemToInventory(previousWeapon);
            }

            weaponSlots[slotIndex] = weapon;
            if (!RemoveItemFromInventory(weapon))
            {
                weaponSlots[slotIndex] = previousWeapon;
                RemoveItemFromInventory(previousWeapon);
                return false;
            }

            int selectedIndex = isRightHand
                ? m_rightHandWeaponIndex
                : m_leftHandWeaponIndex;
            if (slotIndex == selectedIndex)
            {
                SetEquippedWeaponID(isRightHand, weapon.ItemID);
            }

            return true;
        }

        private bool UnequipWeaponInSlot(
            WeaponItem[] weaponSlots,
            int slotIndex,
            bool isRightHand)
        {
            if (weaponSlots == null ||
                slotIndex < 0 ||
                slotIndex >= weaponSlots.Length)
            {
                return false;
            }

            WeaponItem previousWeapon = weaponSlots[slotIndex];
            if (previousWeapon == null || previousWeapon.IsUnarmed)
            {
                return false;
            }

            AddItemToInventory(previousWeapon);
            weaponSlots[slotIndex] = m_unarmedWeapon;
            int selectedIndex = isRightHand
                ? m_rightHandWeaponIndex
                : m_leftHandWeaponIndex;
            if (slotIndex == selectedIndex)
            {
                SetEquippedWeaponID(
                    isRightHand,
                    m_unarmedWeapon?.ItemID ?? 0);
            }

            return true;
        }

        private bool EquipArmorItem<T>(
            T previousEquipment,
            T newEquipment,
            Action<int> setEquipmentID)
            where T : ArmorItem
        {
            Item previousTemplate = ResolveInventoryTemplate(previousEquipment);
            if (previousEquipment != null && previousTemplate == null)
            {
                Debug.LogWarning(
                    $"Could not return equipped item {previousEquipment.ItemID} to inventory.",
                    this);
                return false;
            }

            if (previousTemplate != null)
            {
                AddItemToInventory(previousTemplate);
            }

            if (!RemoveItemFromInventory(newEquipment))
            {
                RemoveItemFromInventory(previousTemplate);
                return false;
            }

            setEquipmentID(newEquipment.ItemID);
            return true;
        }

        private bool UnequipArmorItem<T>(
            T currentEquipment,
            Action<int> setEquipmentID)
            where T : ArmorItem
        {
            if (currentEquipment == null)
            {
                return false;
            }

            Item template = ResolveInventoryTemplate(currentEquipment);
            if (template == null || !AddItemToInventory(template))
            {
                return false;
            }

            setEquipmentID(-1);
            return true;
        }

        private Item ResolveInventoryTemplate(Item runtimeItem)
        {
            if (runtimeItem == null)
            {
                return null;
            }

            WorldItemDatabase database = WorldItemDatabase.Instance;
            return runtimeItem switch
            {
                WeaponItem => ResolveWeaponTemplate(runtimeItem.ItemID),
                HeadEquipmentItem =>
                    database?.GetHeadEquipmentByID(runtimeItem.ItemID) ??
                    GetMatchingFallback(m_startingHeadEquipment, runtimeItem.ItemID),
                BodyEquipmentItem =>
                    database?.GetBodyEquipmentByID(runtimeItem.ItemID) ??
                    GetMatchingFallback(m_startingBodyEquipment, runtimeItem.ItemID),
                LegEquipmentItem =>
                    database?.GetLegEquipmentByID(runtimeItem.ItemID) ??
                    GetMatchingFallback(m_startingLegEquipment, runtimeItem.ItemID),
                HandEquipmentItem =>
                    database?.GetHandEquipmentByID(runtimeItem.ItemID) ??
                    GetMatchingFallback(m_startingHandEquipment, runtimeItem.ItemID),
                _ => null
            };
        }

        private void SetEquippedWeaponID(bool isRightHand, int weaponID)
        {
            PlayerNetworkManager networkManager = m_player?.PlayerNetworkManager;
            if (networkManager?.IsSpawned == true && networkManager.IsOwner)
            {
                if (isRightHand)
                {
                    networkManager.CurrentRightHandWeaponID.Value = weaponID;
                }
                else
                {
                    networkManager.CurrentLeftHandWeaponID.Value = weaponID;
                }

                return;
            }

            if (isRightHand)
            {
                EquipRightWeaponFromID(weaponID);
            }
            else
            {
                EquipLeftWeaponFromID(weaponID);
            }
        }

        private void SetHeadEquipmentID(int itemID)
        {
            SetArmorEquipmentID(
                itemID,
                network => network.CurrentHeadEquipmentID.Value = itemID,
                EquipHeadEquipmentFromID);
        }

        private void SetBodyEquipmentID(int itemID)
        {
            SetArmorEquipmentID(
                itemID,
                network => network.CurrentBodyEquipmentID.Value = itemID,
                EquipBodyEquipmentFromID);
        }

        private void SetLegEquipmentID(int itemID)
        {
            SetArmorEquipmentID(
                itemID,
                network => network.CurrentLegEquipmentID.Value = itemID,
                EquipLegEquipmentFromID);
        }

        private void SetHandEquipmentID(int itemID)
        {
            SetArmorEquipmentID(
                itemID,
                network => network.CurrentHandEquipmentID.Value = itemID,
                EquipHandEquipmentFromID);
        }

        private void SetArmorEquipmentID(
            int itemID,
            Action<PlayerNetworkManager> setNetworkID,
            Action<int> applyLocally)
        {
            PlayerNetworkManager networkManager = m_player?.PlayerNetworkManager;
            if (networkManager?.IsSpawned == true && networkManager.IsOwner)
            {
                setNetworkID(networkManager);
                return;
            }

            applyLocally(itemID);
        }

        private bool TryGetWeaponSlot(
            EquipmentSlotType equipmentSlot,
            out WeaponItem[] weaponSlots,
            out int slotIndex,
            out bool isRightHand)
        {
            int rawSlot = (int)equipmentSlot;
            if (rawSlot >= (int)EquipmentSlotType.RightWeapon01 &&
                rawSlot <= (int)EquipmentSlotType.RightWeapon03)
            {
                weaponSlots = m_weaponsInRightHandSlots;
                slotIndex = rawSlot - (int)EquipmentSlotType.RightWeapon01;
                isRightHand = true;
                return true;
            }

            if (rawSlot >= (int)EquipmentSlotType.LeftWeapon01 &&
                rawSlot <= (int)EquipmentSlotType.LeftWeapon03)
            {
                weaponSlots = m_weaponsInLeftHandSlots;
                slotIndex = rawSlot - (int)EquipmentSlotType.LeftWeapon01;
                isRightHand = false;
                return true;
            }

            weaponSlots = null;
            slotIndex = -1;
            isRightHand = false;
            return false;
        }

        private static T GetMatchingFallback<T>(T fallback, int itemID)
            where T : Item
        {
            return fallback != null && fallback.ItemID == itemID
                ? fallback
                : null;
        }

        private bool CanSwitchWeapons()
        {
            return m_player != null &&
                m_player.IsSpawned &&
                m_player.IsOwner &&
                !m_player.IsDead &&
                !m_player.IsPerformingAction &&
                m_player.PlayerCombatManager?.IsUsingItem != true &&
                m_player.PlayerNetworkManager != null;
        }

        private bool CanSwitchQuickSlotItems()
        {
            return m_player != null &&
                m_player.IsSpawned &&
                m_player.IsOwner &&
                !m_player.IsDead &&
                !m_player.IsPerformingAction &&
                m_player.PlayerCombatManager?.IsUsingItem != true &&
                PlayerUIManager.Instance?.IsMenuWindowOpen != true &&
                m_player.PlayerNetworkManager != null;
        }

        private bool EquipQuickSlotItem(
            int quickSlotIndex,
            QuickSlotItem newItem)
        {
            EnsureQuickSlotItemArray();
            QuickSlotItem previousItem =
                m_quickSlotItemsInQuickSlots[quickSlotIndex];
            if (previousItem != null)
            {
                AddItemToInventory(previousItem);
            }

            if (!RemoveItemFromInventory(newItem))
            {
                RemoveItemFromInventory(previousItem);
                return false;
            }

            m_quickSlotItemsInQuickSlots[quickSlotIndex] = newItem;
            if (quickSlotIndex == m_quickSlotItemIndex)
            {
                SetCurrentQuickSlotItemID(newItem.ItemID);
            }

            return true;
        }

        private bool UnequipQuickSlotItem(int quickSlotIndex)
        {
            EnsureQuickSlotItemArray();
            QuickSlotItem previousItem =
                m_quickSlotItemsInQuickSlots[quickSlotIndex];
            if (previousItem == null || !AddItemToInventory(previousItem))
            {
                return false;
            }

            m_quickSlotItemsInQuickSlots[quickSlotIndex] = null;
            if (quickSlotIndex == m_quickSlotItemIndex)
            {
                SetCurrentQuickSlotItemID(-1);
            }

            return true;
        }

        private void SetCurrentQuickSlotItemID(int quickSlotItemID)
        {
            PlayerNetworkManager networkManager = m_player?.PlayerNetworkManager;
            if (networkManager?.IsSpawned == true && networkManager.IsOwner)
            {
                if (networkManager.CurrentQuickSlotItemID.Value == quickSlotItemID)
                {
                    InitializeCurrentQuickSlotItemFromID(quickSlotItemID);
                    return;
                }

                networkManager.CurrentQuickSlotItemID.Value = quickSlotItemID;
                return;
            }

            InitializeCurrentQuickSlotItemFromID(quickSlotItemID);
        }

        private bool EquipProjectileInSlot(
            EquipmentSlotType equipmentSlot,
            RangedProjectileItem newProjectile)
        {
            RangedProjectileItem currentProjectile = equipmentSlot ==
                    EquipmentSlotType.MainProjectile
                ? m_mainProjectile
                : m_secondaryProjectile;
            RangedProjectileItem returnedProjectile =
                CreateInventoryProjectileCopy(currentProjectile);
            if (returnedProjectile != null)
            {
                AddItemToInventory(returnedProjectile);
            }

            if (!RemoveItemFromInventory(newProjectile))
            {
                RemoveItemFromInventory(returnedProjectile);
                DestroyRuntimeItem(returnedProjectile);
                return false;
            }

            int currentAmount = newProjectile.CurrentAmmoAmount;
            SetProjectileID(equipmentSlot, newProjectile.ItemID, currentAmount);
            DestroyRuntimeItem(newProjectile);
            return true;
        }

        private bool UnequipProjectileInSlot(EquipmentSlotType equipmentSlot)
        {
            RangedProjectileItem currentProjectile = equipmentSlot ==
                    EquipmentSlotType.MainProjectile
                ? m_mainProjectile
                : m_secondaryProjectile;
            RangedProjectileItem returnedProjectile =
                CreateInventoryProjectileCopy(currentProjectile);
            if (returnedProjectile == null ||
                !AddItemToInventory(returnedProjectile))
            {
                DestroyRuntimeItem(returnedProjectile);
                return false;
            }

            SetProjectileID(equipmentSlot, -1, 0);
            return true;
        }

        private void SetProjectileID(
            EquipmentSlotType equipmentSlot,
            int projectileID,
            int currentAmount)
        {
            bool isMainProjectile = equipmentSlot ==
                EquipmentSlotType.MainProjectile;
            PlayerNetworkManager networkManager = m_player?.PlayerNetworkManager;
            if (networkManager?.IsSpawned == true && networkManager.IsOwner)
            {
                if (isMainProjectile)
                {
                    networkManager.MainProjectileID.Value = projectileID;
                }
                else
                {
                    networkManager.SecondaryProjectileID.Value = projectileID;
                }
            }

            if (isMainProjectile)
            {
                InitializeMainProjectileFromID(projectileID, currentAmount);
                return;
            }

            InitializeSecondaryProjectileFromID(projectileID, currentAmount);
        }

        private static RangedProjectileItem CreateInventoryProjectileCopy(
            RangedProjectileItem projectile)
        {
            if (projectile == null)
            {
                return null;
            }

            RangedProjectileItem runtimeProjectile = Instantiate(projectile);
            runtimeProjectile.name = $"{projectile.name} (Inventory Runtime)";
            runtimeProjectile.hideFlags = HideFlags.DontSave;
            return runtimeProjectile;
        }

        private static bool IsProjectileSlot(EquipmentSlotType equipmentSlot)
        {
            return equipmentSlot == EquipmentSlotType.MainProjectile ||
                equipmentSlot == EquipmentSlotType.SecondaryProjectile;
        }

        private static bool TryGetQuickSlotItemIndex(
            EquipmentSlotType equipmentSlot,
            out int quickSlotIndex)
        {
            int rawSlot = (int)equipmentSlot;
            quickSlotIndex = rawSlot - (int)EquipmentSlotType.QuickSlot01;
            return quickSlotIndex >= 0 && quickSlotIndex < k_QuickSlotCount;
        }

        private void EnsureQuickSlotItemArray()
        {
            if (m_quickSlotItemsInQuickSlots == null ||
                m_quickSlotItemsInQuickSlots.Length != k_QuickSlotCount)
            {
                QuickSlotItem[] previousItems = m_quickSlotItemsInQuickSlots;
                m_quickSlotItemsInQuickSlots =
                    new QuickSlotItem[k_QuickSlotCount];
                if (previousItems != null)
                {
                    int copyCount = Mathf.Min(
                        previousItems.Length,
                        m_quickSlotItemsInQuickSlots.Length);
                    Array.Copy(
                        previousItems,
                        m_quickSlotItemsInQuickSlots,
                        copyCount);
                }
            }

        }

        private QuickSlotItem FindQuickSlotItemByID(int quickSlotItemID)
        {
            if (quickSlotItemID < 0)
            {
                return null;
            }

            foreach (QuickSlotItem quickSlotItem in m_quickSlotItemsInQuickSlots)
            {
                if (quickSlotItem?.ItemID == quickSlotItemID)
                {
                    return quickSlotItem;
                }
            }

            return null;
        }

        private QuickSlotItem ResolveOwnedQuickSlotItem(int quickSlotItemID)
        {
            if (m_quickSlotItemIndex >= 0 &&
                m_quickSlotItemIndex < m_quickSlotItemsInQuickSlots.Length &&
                m_quickSlotItemsInQuickSlots[m_quickSlotItemIndex]?.ItemID ==
                    quickSlotItemID)
            {
                return m_quickSlotItemsInQuickSlots[m_quickSlotItemIndex];
            }

            return FindQuickSlotItemByID(quickSlotItemID);
        }

        private WeaponItem CreateRuntimeWeapon(int weaponID, bool isRightHand)
        {
            WeaponItem template = ResolveOwnedWeaponSlot(
                weaponID,
                isRightHand) ?? ResolveWeaponTemplate(weaponID) ??
                m_unarmedWeapon;
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

        private WeaponItem ResolveOwnedWeaponSlot(
            int weaponID,
            bool isRightHand)
        {
            if (m_player?.IsOwner != true)
            {
                return null;
            }

            WeaponItem[] weaponSlots = isRightHand
                ? m_weaponsInRightHandSlots
                : m_weaponsInLeftHandSlots;
            int weaponIndex = isRightHand
                ? m_rightHandWeaponIndex
                : m_leftHandWeaponIndex;
            if (weaponSlots != null &&
                weaponIndex >= 0 &&
                weaponIndex < weaponSlots.Length &&
                weaponSlots[weaponIndex]?.ItemID == weaponID)
            {
                return weaponSlots[weaponIndex];
            }

            return FindWeaponByID(weaponSlots, weaponID);
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

        private void ReplaceRuntimeProjectile(
            ref RangedProjectileItem currentProjectile,
            int projectileID,
            int currentAmount,
            RangedProjectileItem fallback)
        {
            RangedProjectileItem template = WorldItemDatabase.Instance
                ?.GetProjectileByID(projectileID);
            if (template == null && fallback?.ItemID == projectileID)
            {
                template = fallback;
            }

            RangedProjectileItem previousProjectile = currentProjectile;
            currentProjectile = null;
            if (template != null)
            {
                currentProjectile = Instantiate(template);
                currentProjectile.name = $"{template.name} (Runtime)";
                currentProjectile.hideFlags = HideFlags.DontSave;
                currentProjectile.SetCurrentAmmoAmount(
                    currentAmount >= 0
                        ? currentAmount
                        : template.CurrentAmmoAmount);
            }

            DestroyRuntimeItem(previousProjectile);
        }

        private T CreateRuntimeArmor<T>(
            int itemID,
            Func<WorldItemDatabase, T> databaseResolver,
            T localFallback) where T : ArmorItem
        {
            if (itemID < 0)
            {
                return null;
            }

            WorldItemDatabase database = WorldItemDatabase.Instance;
            T template = database != null ? databaseResolver(database) : null;
            if (template == null && localFallback != null && localFallback.ItemID == itemID)
            {
                template = localFallback;
            }

            if (template == null)
            {
                Debug.LogWarning($"Could not resolve armor item ID {itemID}.", this);
                return null;
            }

            T runtimeItem = Instantiate(template);
            runtimeItem.name = $"{template.name} (Runtime)";
            runtimeItem.hideFlags = HideFlags.DontSave;
            return runtimeItem;
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

        private static QuickSlotItem SelectNextQuickSlotItem(
            QuickSlotItem[] quickSlots,
            QuickSlotItem currentItem,
            ref int currentIndex)
        {
            int equippedItemCount = 0;
            QuickSlotItem onlyItem = null;
            for (int slotIndex = 0; slotIndex < quickSlots.Length; slotIndex++)
            {
                if (quickSlots[slotIndex] == null)
                {
                    continue;
                }

                equippedItemCount++;
                onlyItem = quickSlots[slotIndex];
            }

            if (equippedItemCount == 0)
            {
                currentIndex = 0;
                return null;
            }

            if (equippedItemCount == 1)
            {
                if (currentItem?.ItemID == onlyItem.ItemID)
                {
                    return null;
                }

                currentIndex = Array.IndexOf(quickSlots, onlyItem);
                return onlyItem;
            }

            for (int slotOffset = 1; slotOffset <= quickSlots.Length; slotOffset++)
            {
                int slotIndex = (currentIndex + slotOffset) % quickSlots.Length;
                QuickSlotItem candidate = quickSlots[slotIndex];
                if (candidate == null)
                {
                    continue;
                }

                currentIndex = slotIndex;
                return candidate;
            }

            return null;
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

        private static void SynchronizeQuickSlotItemIndex(
            QuickSlotItem[] quickSlots,
            int quickSlotItemID,
            ref int currentIndex)
        {
            for (int slotIndex = 0; slotIndex < quickSlots.Length; slotIndex++)
            {
                if (quickSlots[slotIndex]?.ItemID == quickSlotItemID)
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

        private SerializableWeapon[] ConvertWeaponIDs(int[] itemIDs)
        {
            SerializableWeapon[] savedWeapons =
                new SerializableWeapon[k_QuickSlotCount];
            for (int slotIndex = 0; slotIndex < k_QuickSlotCount; slotIndex++)
            {
                int itemID = itemIDs != null && slotIndex < itemIDs.Length
                    ? itemIDs[slotIndex]
                    : m_unarmedWeapon?.ItemID ?? 0;
                WeaponItem template = ResolveWeaponTemplate(itemID) ??
                    m_unarmedWeapon;
                savedWeapons[slotIndex] = WorldSaveGameManager
                    .GetSerializableWeaponFromWeaponItem(template);
            }

            return savedWeapons;
        }

        private void RestoreQuickSlots(
            WeaponItem[] quickSlots,
            SerializableWeapon[] savedWeapons)
        {
            WorldItemDatabase database = WorldItemDatabase.Instance;
            for (int slotIndex = 0; slotIndex < k_QuickSlotCount; slotIndex++)
            {
                DestroyRuntimeItem(quickSlots[slotIndex]);
                SerializableWeapon savedWeapon = savedWeapons != null &&
                    slotIndex < savedWeapons.Length
                        ? savedWeapons[slotIndex]
                        : null;
                WeaponItem runtimeWeapon =
                    database?.GetWeaponFromSerializedData(savedWeapon);
                if (runtimeWeapon == null && m_unarmedWeapon != null)
                {
                    runtimeWeapon = Instantiate(m_unarmedWeapon);
                    runtimeWeapon.name =
                        $"{m_unarmedWeapon.name} (Weapon Runtime)";
                    runtimeWeapon.hideFlags = HideFlags.DontSave;
                }

                quickSlots[slotIndex] = runtimeWeapon;
            }
        }

        private static int GetQuickSlotItemID(WeaponItem[] quickSlots, int slotIndex)
        {
            if (quickSlots == null || slotIndex < 0 || slotIndex >= quickSlots.Length)
            {
                return 0;
            }

            return quickSlots[slotIndex]?.ItemID ?? 0;
        }

        private static WeaponItem GetQuickSlotItem(
            WeaponItem[] quickSlots,
            int slotIndex)
        {
            if (quickSlots == null ||
                slotIndex < 0 ||
                slotIndex >= quickSlots.Length)
            {
                return null;
            }

            return quickSlots[slotIndex];
        }

        private void ReplaceRuntimeProjectileFromSerializedData(
            ref RangedProjectileItem currentProjectile,
            SerializableRangeProjectile savedProjectile)
        {
            RangedProjectileItem previousProjectile = currentProjectile;
            currentProjectile = WorldItemDatabase.Instance
                ?.GetProjectileFromSerializedData(savedProjectile);
            DestroyRuntimeItem(previousProjectile);
        }

        private void AddRuntimeItems<TData, TItem>(
            IEnumerable<TData> savedItems,
            Func<TData, TItem> createRuntimeItem)
            where TItem : Item
        {
            if (savedItems == null || createRuntimeItem == null)
            {
                return;
            }

            foreach (TData savedItem in savedItems)
            {
                TItem runtimeItem = createRuntimeItem(savedItem);
                if (runtimeItem != null)
                {
                    AddItemToInventory(runtimeItem);
                }
            }
        }

        private static void DestroyRuntimeItem(Item item)
        {
            if (item != null && (item.hideFlags & HideFlags.DontSave) != 0)
            {
                Destroy(item);
            }
        }

        private static void DestroyRuntimeItems<T>(IEnumerable<T> items)
            where T : Item
        {
            if (items == null)
            {
                return;
            }

            foreach (T item in items)
            {
                DestroyRuntimeItem(item);
            }
        }

        private void RefreshTwoHandPointer(WeaponItem runtimeWeapon, bool isRightHand)
        {
            PlayerNetworkManager networkManager = m_player?.PlayerNetworkManager;
            if (networkManager == null || !networkManager.IsTwoHandingWeapon.Value)
            {
                return;
            }

            if ((isRightHand && networkManager.IsTwoHandingRightWeapon.Value) ||
                (!isRightHand && networkManager.IsTwoHandingLeftWeapon.Value))
            {
                m_currentTwoHandWeapon = runtimeWeapon;
            }
        }

        private void PlayWeaponSwapAnimationIfOneHanded(WeaponModelSlot weaponSlot)
        {
            if (m_player?.PlayerNetworkManager?.IsTwoHandingWeapon.Value != true)
            {
                m_player?.PlayerAnimatorManager?.PlayWeaponSwapAnimation(weaponSlot);
            }
        }
    }
}
