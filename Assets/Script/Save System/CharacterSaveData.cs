using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Contains the stable, serializable state required to restore one character.
    /// </summary>
    [Serializable]
    public class CharacterSaveData
    {
        private const int k_DefaultAttributeLevel = 10;
        private const float k_DefaultCurrentHealth = 150f;
        private const float k_DefaultCurrentStamina = 100f;
        private const float k_DefaultCurrentFocusPoints = 100f;
        private const int k_DefaultSpellID = 10;
        private const int k_AttributeDataVersion = 1;
        private const int k_EquipmentDataVersion = 4;
        private const int k_WorldLootDataVersion = 5;
        private const int k_FocusPointsDataVersion = 6;
        private const int k_ProjectileDataVersion = 7;
        private const int k_ComplexItemDataVersion = 8;
        private const int k_CharacterCreationDataVersion = 9;
        private const int k_LevelUpDataVersion = 10;
        private const int k_DeadSpotDataVersion = 11;
        private const int k_CurrentDataVersion = 11;

        [SerializeField, Min(0)] private int m_dataVersion = k_CurrentDataVersion;
        [SerializeField] private string m_characterName = string.Empty;
        [SerializeField, Min(0f)] private float m_secondsPlayed;
        [SerializeField] private float m_xPosition;
        [SerializeField] private float m_yPosition;
        [SerializeField] private float m_zPosition;
        [SerializeField] private int m_sceneIndex;
        [SerializeField, Min(0)] private int m_vitality = k_DefaultAttributeLevel;
        [SerializeField, Min(0)] private int m_endurance = k_DefaultAttributeLevel;
        [SerializeField, Min(0)] private int m_mind = k_DefaultAttributeLevel;
        [SerializeField, Min(0)] private int m_strength = k_DefaultAttributeLevel;
        [SerializeField, Min(0)] private int m_dexterity = k_DefaultAttributeLevel;
        [SerializeField, Min(0)] private int m_intelligence = k_DefaultAttributeLevel;
        [SerializeField, Min(0)] private int m_faith = k_DefaultAttributeLevel;
        [SerializeField, Min(0)] private int m_runes;
        [SerializeField] private bool m_hasDeadSpot;
        [SerializeField] private float m_deadSpotPositionX;
        [SerializeField] private float m_deadSpotPositionY;
        [SerializeField] private float m_deadSpotPositionZ;
        [SerializeField, Min(0)] private int m_deadSpotRuneCount;
        [SerializeField, Min(0)] private int m_lastSiteOfGraceRestedAt;
        [SerializeField, Min(0f)] private float m_currentHealth = k_DefaultCurrentHealth;
        [SerializeField, Min(0f)] private float m_currentStamina = k_DefaultCurrentStamina;
        [SerializeField, Min(0f)] private float m_currentFocusPoints =
            k_DefaultCurrentFocusPoints;
        [SerializeField, Min(0)] private int m_currentHealthFlasksRemaining = 3;
        [SerializeField, Min(0)] private int m_currentFocusPointFlasksRemaining = 1;
        [SerializeField] private int m_currentSpellID = k_DefaultSpellID;

        [Header("Complex Equipment State")]
        [SerializeField] private SerializableWeapon m_rightHandWeaponSlot01 =
            new(1, -1);
        [SerializeField] private SerializableWeapon m_rightHandWeaponSlot02 =
            new(2, -1);
        [SerializeField] private SerializableWeapon m_rightHandWeaponSlot03 =
            new(0, -1);
        [SerializeField] private SerializableWeapon m_leftHandWeaponSlot01 =
            new(3, 8);
        [SerializeField] private SerializableWeapon m_leftHandWeaponSlot02 =
            new(2, -1);
        [SerializeField] private SerializableWeapon m_leftHandWeaponSlot03 =
            new(0, -1);
        [SerializeField] private SerializableRangeProjectile m_mainProjectile =
            new(12, 30);
        [SerializeField] private SerializableRangeProjectile m_secondaryProjectile =
            new(13, 30);
        [SerializeField] private SerializableQuickSlotItem m_quickSlotItem01 =
            new(14, 3);
        [SerializeField] private SerializableQuickSlotItem m_quickSlotItem02 =
            new(15, 1);
        [SerializeField] private SerializableQuickSlotItem m_quickSlotItem03 =
            new(-1, 0);
        [SerializeField, Range(0, 2)] private int m_quickSlotItemIndex;

        [Header("Runtime Inventory State")]
        [SerializeField] private List<SerializableWeapon> m_weaponsInInventory = new();
        [SerializeField] private List<SerializableRangeProjectile>
            m_projectilesInInventory = new();
        [SerializeField] private List<SerializableQuickSlotItem>
            m_quickSlotItemsInInventory = new();
        [SerializeField] private List<int> m_headEquipmentInInventory = new();
        [SerializeField] private List<int> m_bodyEquipmentInInventory = new();
        [SerializeField] private List<int> m_handEquipmentInInventory = new();
        [SerializeField] private List<int> m_legEquipmentInInventory = new();

        // Retained solely so version-7 and earlier JSON can be migrated safely.
        [SerializeField] private int m_mainProjectileID = 12;
        [SerializeField] private int m_secondaryProjectileID = 13;
        [SerializeField, Min(0)] private int m_mainProjectileAmount = 30;
        [SerializeField, Min(0)] private int m_secondaryProjectileAmount = 30;
        [SerializeField] private int m_headEquipmentID = -1;
        [SerializeField] private int m_bodyEquipmentID = -1;
        [SerializeField] private int m_handEquipmentID = -1;
        [SerializeField] private int m_legEquipmentID = -1;
        [SerializeField] private int m_rightHandWeaponSlot01ID = 1;
        [SerializeField] private int m_rightHandWeaponSlot02ID = 2;
        [SerializeField] private int m_rightHandWeaponSlot03ID;
        [SerializeField] private int m_leftHandWeaponSlot01ID = 3;
        [SerializeField] private int m_leftHandWeaponSlot02ID = 2;
        [SerializeField] private int m_leftHandWeaponSlot03ID;
        [SerializeField, Range(0, 2)] private int m_rightHandWeaponIndex;
        [SerializeField, Range(0, 2)] private int m_leftHandWeaponIndex;
        [SerializeField] private bool m_isMale = true;
        [SerializeField, Min(0)] private int m_hairstyleID;
        [SerializeField, Range(0, 255)] private int m_hairColorRed = 79;
        [SerializeField, Range(0, 255)] private int m_hairColorGreen = 53;
        [SerializeField, Range(0, 255)] private int m_hairColorBlue = 35;
        [SerializeField] private List<BossSaveData> m_bosses = new();
        [SerializeField] private List<SiteOfGraceSaveData> m_sitesOfGrace = new();
        [SerializeField] private WorldItemLootDictionary m_worldItemsLooted = new();

        public string CharacterName
        {
            get => m_characterName;
            set => m_characterName = value ?? string.Empty;
        }

        public float SecondsPlayed
        {
            get => m_secondsPlayed;
            set => m_secondsPlayed = Mathf.Max(0f, value);
        }

        public float XPosition
        {
            get => m_xPosition;
            set => m_xPosition = value;
        }

        public float YPosition
        {
            get => m_yPosition;
            set => m_yPosition = value;
        }

        public float ZPosition
        {
            get => m_zPosition;
            set => m_zPosition = value;
        }

        public int SceneIndex
        {
            get => m_sceneIndex;
            set => m_sceneIndex = value;
        }

        public int Vitality
        {
            get => m_vitality;
            set => m_vitality = Mathf.Max(0, value);
        }

        public int Endurance
        {
            get => m_endurance;
            set => m_endurance = Mathf.Max(0, value);
        }

        public int Mind
        {
            get => m_mind;
            set => m_mind = Mathf.Max(0, value);
        }

        /// <summary>Gets or sets the saved Strength attribute.</summary>
        public int Strength
        {
            get => m_strength;
            set => m_strength = Mathf.Max(0, value);
        }

        /// <summary>Gets or sets the saved Dexterity attribute.</summary>
        public int Dexterity
        {
            get => m_dexterity;
            set => m_dexterity = Mathf.Max(0, value);
        }

        /// <summary>Gets or sets the saved Intelligence attribute.</summary>
        public int Intelligence
        {
            get => m_intelligence;
            set => m_intelligence = Mathf.Max(0, value);
        }

        /// <summary>Gets or sets the saved Faith attribute.</summary>
        public int Faith
        {
            get => m_faith;
            set => m_faith = Mathf.Max(0, value);
        }

        /// <summary>Gets or sets the saved private Rune balance.</summary>
        public int Runes
        {
            get => m_runes;
            set => m_runes = Mathf.Max(0, value);
        }

        /// <summary>Gets or sets whether an unclaimed Rune recovery point exists.</summary>
        public bool HasDeadSpot
        {
            get => m_hasDeadSpot;
            set => m_hasDeadSpot = value;
        }

        /// <summary>Gets or sets the saved Dead Spot X coordinate.</summary>
        public float DeadSpotPositionX
        {
            get => m_deadSpotPositionX;
            set => m_deadSpotPositionX = value;
        }

        /// <summary>Gets or sets the saved Dead Spot Y coordinate.</summary>
        public float DeadSpotPositionY
        {
            get => m_deadSpotPositionY;
            set => m_deadSpotPositionY = value;
        }

        /// <summary>Gets or sets the saved Dead Spot Z coordinate.</summary>
        public float DeadSpotPositionZ
        {
            get => m_deadSpotPositionZ;
            set => m_deadSpotPositionZ = value;
        }

        /// <summary>Gets or sets the Rune count held by the saved Dead Spot.</summary>
        public int DeadSpotRuneCount
        {
            get => m_deadSpotRuneCount;
            set => m_deadSpotRuneCount = Mathf.Max(0, value);
        }

        /// <summary>Gets or sets the last Site of Grace used as a checkpoint.</summary>
        public int LastSiteOfGraceRestedAt
        {
            get => m_lastSiteOfGraceRestedAt;
            set => m_lastSiteOfGraceRestedAt = Mathf.Max(0, value);
        }

        public float CurrentHealth
        {
            get => m_currentHealth;
            set => m_currentHealth = Mathf.Max(0f, value);
        }

        public float CurrentStamina
        {
            get => m_currentStamina;
            set => m_currentStamina = Mathf.Max(0f, value);
        }

        public float CurrentFocusPoints
        {
            get => m_currentFocusPoints;
            set => m_currentFocusPoints = Mathf.Max(0f, value);
        }

        public int CurrentSpellID
        {
            get => m_currentSpellID;
            set => m_currentSpellID = Mathf.Max(-1, value);
        }

        /// <summary>Gets or sets the owner's remaining Health Flask uses.</summary>
        public int CurrentHealthFlasksRemaining
        {
            get => m_currentHealthFlasksRemaining;
            set => m_currentHealthFlasksRemaining = Mathf.Max(0, value);
        }

        /// <summary>Gets or sets the owner's remaining Focus Point Flask uses.</summary>
        public int CurrentFocusPointFlasksRemaining
        {
            get => m_currentFocusPointFlasksRemaining;
            set => m_currentFocusPointFlasksRemaining = Mathf.Max(0, value);
        }

        /// <summary>Gets or sets the first serialized right-hand weapon.</summary>
        public SerializableWeapon RightHandWeaponSlot01
        {
            get => m_rightHandWeaponSlot01 ??= new SerializableWeapon(0, -1);
            set => m_rightHandWeaponSlot01 = value ?? new SerializableWeapon(0, -1);
        }

        /// <summary>Gets or sets the second serialized right-hand weapon.</summary>
        public SerializableWeapon RightHandWeaponSlot02
        {
            get => m_rightHandWeaponSlot02 ??= new SerializableWeapon(0, -1);
            set => m_rightHandWeaponSlot02 = value ?? new SerializableWeapon(0, -1);
        }

        /// <summary>Gets or sets the third serialized right-hand weapon.</summary>
        public SerializableWeapon RightHandWeaponSlot03
        {
            get => m_rightHandWeaponSlot03 ??= new SerializableWeapon(0, -1);
            set => m_rightHandWeaponSlot03 = value ?? new SerializableWeapon(0, -1);
        }

        /// <summary>Gets or sets the first serialized left-hand weapon.</summary>
        public SerializableWeapon LeftHandWeaponSlot01
        {
            get => m_leftHandWeaponSlot01 ??= new SerializableWeapon(0, -1);
            set => m_leftHandWeaponSlot01 = value ?? new SerializableWeapon(0, -1);
        }

        /// <summary>Gets or sets the second serialized left-hand weapon.</summary>
        public SerializableWeapon LeftHandWeaponSlot02
        {
            get => m_leftHandWeaponSlot02 ??= new SerializableWeapon(0, -1);
            set => m_leftHandWeaponSlot02 = value ?? new SerializableWeapon(0, -1);
        }

        /// <summary>Gets or sets the third serialized left-hand weapon.</summary>
        public SerializableWeapon LeftHandWeaponSlot03
        {
            get => m_leftHandWeaponSlot03 ??= new SerializableWeapon(0, -1);
            set => m_leftHandWeaponSlot03 = value ?? new SerializableWeapon(0, -1);
        }

        /// <summary>Gets or sets primary ammunition instance state.</summary>
        public SerializableRangeProjectile MainProjectile
        {
            get => m_mainProjectile ??= new SerializableRangeProjectile(-1, 0);
            set => m_mainProjectile = value ?? new SerializableRangeProjectile(-1, 0);
        }

        /// <summary>Gets or sets secondary ammunition instance state.</summary>
        public SerializableRangeProjectile SecondaryProjectile
        {
            get => m_secondaryProjectile ??= new SerializableRangeProjectile(-1, 0);
            set => m_secondaryProjectile = value ?? new SerializableRangeProjectile(-1, 0);
        }

        /// <summary>Gets or sets the first gameplay quick-slot item.</summary>
        public SerializableQuickSlotItem QuickSlotItem01
        {
            get => m_quickSlotItem01 ??= new SerializableQuickSlotItem(-1, 0);
            set => m_quickSlotItem01 = value ?? new SerializableQuickSlotItem(-1, 0);
        }

        /// <summary>Gets or sets the second gameplay quick-slot item.</summary>
        public SerializableQuickSlotItem QuickSlotItem02
        {
            get => m_quickSlotItem02 ??= new SerializableQuickSlotItem(-1, 0);
            set => m_quickSlotItem02 = value ?? new SerializableQuickSlotItem(-1, 0);
        }

        /// <summary>Gets or sets the third gameplay quick-slot item.</summary>
        public SerializableQuickSlotItem QuickSlotItem03
        {
            get => m_quickSlotItem03 ??= new SerializableQuickSlotItem(-1, 0);
            set => m_quickSlotItem03 = value ?? new SerializableQuickSlotItem(-1, 0);
        }

        /// <summary>Gets or sets the selected gameplay quick-slot index.</summary>
        public int QuickSlotItemIndex
        {
            get => m_quickSlotItemIndex;
            set => m_quickSlotItemIndex = Mathf.Clamp(value, 0, 2);
        }

        /// <summary>Gets saved unequipped weapon instances.</summary>
        public List<SerializableWeapon> WeaponsInInventory =>
            m_weaponsInInventory ??= new List<SerializableWeapon>();

        /// <summary>Gets saved unequipped ammunition stacks.</summary>
        public List<SerializableRangeProjectile> ProjectilesInInventory =>
            m_projectilesInInventory ??=
                new List<SerializableRangeProjectile>();

        /// <summary>Gets saved unequipped gameplay quick-slot items.</summary>
        public List<SerializableQuickSlotItem> QuickSlotItemsInInventory =>
            m_quickSlotItemsInInventory ??=
                new List<SerializableQuickSlotItem>();

        /// <summary>Gets saved unequipped head-equipment identifiers.</summary>
        public List<int> HeadEquipmentInInventory =>
            m_headEquipmentInInventory ??= new List<int>();

        /// <summary>Gets saved unequipped body-equipment identifiers.</summary>
        public List<int> BodyEquipmentInInventory =>
            m_bodyEquipmentInInventory ??= new List<int>();

        /// <summary>Gets saved unequipped hand-equipment identifiers.</summary>
        public List<int> HandEquipmentInInventory =>
            m_handEquipmentInInventory ??= new List<int>();

        /// <summary>Gets saved unequipped leg-equipment identifiers.</summary>
        public List<int> LegEquipmentInInventory =>
            m_legEquipmentInInventory ??= new List<int>();

        public int MainProjectileID
        {
            get => MainProjectile.ItemID;
            set
            {
                m_mainProjectileID = Mathf.Max(-1, value);
                MainProjectile.ItemID = value;
            }
        }

        public int SecondaryProjectileID
        {
            get => SecondaryProjectile.ItemID;
            set
            {
                m_secondaryProjectileID = Mathf.Max(-1, value);
                SecondaryProjectile.ItemID = value;
            }
        }

        public int MainProjectileAmount
        {
            get => MainProjectile.ItemAmount;
            set
            {
                m_mainProjectileAmount = Mathf.Max(0, value);
                MainProjectile.ItemAmount = value;
            }
        }

        public int SecondaryProjectileAmount
        {
            get => SecondaryProjectile.ItemAmount;
            set
            {
                m_secondaryProjectileAmount = Mathf.Max(0, value);
                SecondaryProjectile.ItemAmount = value;
            }
        }

        public int HeadEquipmentID
        {
            get => m_headEquipmentID;
            set => m_headEquipmentID = Mathf.Max(-1, value);
        }

        public int BodyEquipmentID
        {
            get => m_bodyEquipmentID;
            set => m_bodyEquipmentID = Mathf.Max(-1, value);
        }

        public int HandEquipmentID
        {
            get => m_handEquipmentID;
            set => m_handEquipmentID = Mathf.Max(-1, value);
        }

        public int LegEquipmentID
        {
            get => m_legEquipmentID;
            set => m_legEquipmentID = Mathf.Max(-1, value);
        }

        public int RightHandWeaponSlot01ID
        {
            get => RightHandWeaponSlot01.ItemID;
            set
            {
                m_rightHandWeaponSlot01ID = Mathf.Max(0, value);
                RightHandWeaponSlot01.ItemID = Mathf.Max(0, value);
            }
        }

        public int RightHandWeaponSlot02ID
        {
            get => RightHandWeaponSlot02.ItemID;
            set
            {
                m_rightHandWeaponSlot02ID = Mathf.Max(0, value);
                RightHandWeaponSlot02.ItemID = Mathf.Max(0, value);
            }
        }

        public int RightHandWeaponSlot03ID
        {
            get => RightHandWeaponSlot03.ItemID;
            set
            {
                m_rightHandWeaponSlot03ID = Mathf.Max(0, value);
                RightHandWeaponSlot03.ItemID = Mathf.Max(0, value);
            }
        }

        public int LeftHandWeaponSlot01ID
        {
            get => LeftHandWeaponSlot01.ItemID;
            set
            {
                m_leftHandWeaponSlot01ID = Mathf.Max(0, value);
                LeftHandWeaponSlot01.ItemID = Mathf.Max(0, value);
            }
        }

        public int LeftHandWeaponSlot02ID
        {
            get => LeftHandWeaponSlot02.ItemID;
            set
            {
                m_leftHandWeaponSlot02ID = Mathf.Max(0, value);
                LeftHandWeaponSlot02.ItemID = Mathf.Max(0, value);
            }
        }

        public int LeftHandWeaponSlot03ID
        {
            get => LeftHandWeaponSlot03.ItemID;
            set
            {
                m_leftHandWeaponSlot03ID = Mathf.Max(0, value);
                LeftHandWeaponSlot03.ItemID = Mathf.Max(0, value);
            }
        }

        public int RightHandWeaponIndex
        {
            get => m_rightHandWeaponIndex;
            set => m_rightHandWeaponIndex = Mathf.Clamp(value, 0, 2);
        }

        public int LeftHandWeaponIndex
        {
            get => m_leftHandWeaponIndex;
            set => m_leftHandWeaponIndex = Mathf.Clamp(value, 0, 2);
        }

        public bool IsMale
        {
            get => m_isMale;
            set => m_isMale = value;
        }

        /// <summary>Gets or sets the saved hairstyle index.</summary>
        public int HairstyleID
        {
            get => m_hairstyleID;
            set => m_hairstyleID = Mathf.Max(0, value);
        }

        /// <summary>Gets or sets the saved red hair channel.</summary>
        public int HairColorRed
        {
            get => m_hairColorRed;
            set => m_hairColorRed = Mathf.Clamp(value, 0, 255);
        }

        /// <summary>Gets or sets the saved green hair channel.</summary>
        public int HairColorGreen
        {
            get => m_hairColorGreen;
            set => m_hairColorGreen = Mathf.Clamp(value, 0, 255);
        }

        /// <summary>Gets or sets the saved blue hair channel.</summary>
        public int HairColorBlue
        {
            get => m_hairColorBlue;
            set => m_hairColorBlue = Mathf.Clamp(value, 0, 255);
        }

        /// <summary>Clears every classified inventory list before a new save pass.</summary>
        public void ClearInventoryData()
        {
            WeaponsInInventory.Clear();
            ProjectilesInInventory.Clear();
            QuickSlotItemsInInventory.Clear();
            HeadEquipmentInInventory.Clear();
            BodyEquipmentInInventory.Clear();
            HandEquipmentInInventory.Clear();
            LegEquipmentInInventory.Clear();
        }

        /// <summary>Gets whether a fixed world item has already been collected.</summary>
        public bool IsWorldItemLooted(int worldItemID)
        {
            return worldItemID >= 0 &&
                m_worldItemsLooted != null &&
                m_worldItemsLooted.TryGetValue(worldItemID, out bool isLooted) &&
                isLooted;
        }

        /// <summary>Gets a registered fixed world item's persisted loot state.</summary>
        public bool TryGetWorldItemLooted(int worldItemID, out bool isLooted)
        {
            isLooted = false;
            return worldItemID >= 0 &&
                m_worldItemsLooted != null &&
                m_worldItemsLooted.TryGetValue(worldItemID, out isLooted);
        }

        /// <summary>Adds or updates one fixed world item's persisted loot state.</summary>
        public bool SetWorldItemLooted(int worldItemID, bool isLooted)
        {
            if (worldItemID < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(worldItemID),
                    worldItemID,
                    "World item identifiers cannot be negative.");
            }

            m_worldItemsLooted ??= new WorldItemLootDictionary();
            if (m_worldItemsLooted.TryGetValue(worldItemID, out bool currentState) &&
                currentState == isLooted)
            {
                return false;
            }

            m_worldItemsLooted[worldItemID] = isLooted;
            return true;
        }

        /// <summary>Gets the saved lifecycle state for a boss, defaulting to dormant.</summary>
        public BossProgressState GetBossProgress(int bossID)
        {
            BossSaveData bossData = FindBossData(bossID);
            return bossData?.Progress ?? BossProgressState.Dormant;
        }

        /// <summary>
        /// Adds a boss entry or advances its state without allowing progress regression.
        /// </summary>
        public bool SetBossProgress(int bossID, BossProgressState progress)
        {
            if (bossID <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(bossID),
                    bossID,
                    "Boss identifiers must be greater than zero.");
            }

            m_bosses ??= new List<BossSaveData>();
            BossSaveData bossData = FindBossData(bossID);
            if (bossData != null)
            {
                return bossData.AdvanceTo(progress);
            }

            m_bosses.Add(new BossSaveData(bossID, progress));
            return true;
        }

        /// <summary>Gets whether the identified boss should remain absent after loading.</summary>
        public bool IsBossDefeated(int bossID)
        {
            return GetBossProgress(bossID) == BossProgressState.Defeated;
        }

        /// <summary>Gets whether the identified Site of Grace has been restored.</summary>
        public bool IsSiteOfGraceActivated(int siteOfGraceID)
        {
            return FindSiteOfGraceData(siteOfGraceID)?.IsActivated ?? false;
        }

        /// <summary>Adds or updates one Site of Grace activation entry.</summary>
        public bool SetSiteOfGraceActivated(int siteOfGraceID, bool isActivated)
        {
            if (siteOfGraceID <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(siteOfGraceID),
                    siteOfGraceID,
                    "Site of Grace identifiers must be greater than zero.");
            }

            m_sitesOfGrace ??= new List<SiteOfGraceSaveData>();
            SiteOfGraceSaveData siteData = FindSiteOfGraceData(siteOfGraceID);
            if (siteData != null)
            {
                if (siteData.IsActivated == isActivated)
                {
                    return false;
                }

                siteData.IsActivated = isActivated;
                return true;
            }

            m_sitesOfGrace.Add(new SiteOfGraceSaveData(
                siteOfGraceID,
                isActivated));
            return true;
        }

        internal void MigrateToLatestVersion()
        {
            if (m_dataVersion < k_AttributeDataVersion)
            {
                m_vitality = k_DefaultAttributeLevel;
                m_endurance = k_DefaultAttributeLevel;
                m_currentHealth = k_DefaultCurrentHealth;
                m_currentStamina = k_DefaultCurrentStamina;
            }

            if (m_dataVersion < k_EquipmentDataVersion)
            {
                m_headEquipmentID = -1;
                m_bodyEquipmentID = -1;
                m_handEquipmentID = -1;
                m_legEquipmentID = -1;
                m_rightHandWeaponSlot01ID = 1;
                m_rightHandWeaponSlot02ID = 2;
                m_rightHandWeaponSlot03ID = 0;
                m_leftHandWeaponSlot01ID = 3;
                m_leftHandWeaponSlot02ID = 2;
                m_leftHandWeaponSlot03ID = 0;
                m_rightHandWeaponIndex = 0;
                m_leftHandWeaponIndex = 0;
                m_isMale = true;
            }

            if (m_dataVersion < k_WorldLootDataVersion || m_worldItemsLooted == null)
            {
                m_worldItemsLooted = new WorldItemLootDictionary();
            }

            if (m_dataVersion < k_FocusPointsDataVersion)
            {
                m_mind = k_DefaultAttributeLevel;
                m_currentFocusPoints = k_DefaultCurrentFocusPoints;
                m_currentSpellID = k_DefaultSpellID;
            }

            if (m_dataVersion < k_ProjectileDataVersion)
            {
                m_mainProjectileID = 12;
                m_secondaryProjectileID = 13;
                m_mainProjectileAmount = 30;
                m_secondaryProjectileAmount = 30;
            }

            if (m_dataVersion < k_ComplexItemDataVersion)
            {
                m_currentHealthFlasksRemaining = 3;
                m_currentFocusPointFlasksRemaining = 1;
                m_rightHandWeaponSlot01 = CreateLegacyWeapon(
                    m_rightHandWeaponSlot01ID);
                m_rightHandWeaponSlot02 = CreateLegacyWeapon(
                    m_rightHandWeaponSlot02ID);
                m_rightHandWeaponSlot03 = CreateLegacyWeapon(
                    m_rightHandWeaponSlot03ID);
                m_leftHandWeaponSlot01 = CreateLegacyWeapon(
                    m_leftHandWeaponSlot01ID);
                m_leftHandWeaponSlot02 = CreateLegacyWeapon(
                    m_leftHandWeaponSlot02ID);
                m_leftHandWeaponSlot03 = CreateLegacyWeapon(
                    m_leftHandWeaponSlot03ID);
                m_mainProjectile = new SerializableRangeProjectile(
                    m_mainProjectileID,
                    m_mainProjectileAmount);
                m_secondaryProjectile = new SerializableRangeProjectile(
                    m_secondaryProjectileID,
                    m_secondaryProjectileAmount);
                m_quickSlotItem01 = new SerializableQuickSlotItem(14, 3);
                m_quickSlotItem02 = new SerializableQuickSlotItem(15, 1);
                m_quickSlotItem03 = new SerializableQuickSlotItem(-1, 0);
                m_quickSlotItemIndex = 0;
            }

            if (m_dataVersion < k_CharacterCreationDataVersion)
            {
                m_strength = k_DefaultAttributeLevel;
                m_dexterity = k_DefaultAttributeLevel;
                m_intelligence = k_DefaultAttributeLevel;
                m_faith = k_DefaultAttributeLevel;
                m_hairstyleID = 0;
                m_hairColorRed = 79;
                m_hairColorGreen = 53;
                m_hairColorBlue = 35;
            }

            if (m_dataVersion < k_LevelUpDataVersion)
            {
                m_runes = 0;
            }

            if (m_dataVersion < k_DeadSpotDataVersion)
            {
                m_hasDeadSpot = false;
                m_deadSpotPositionX = 0f;
                m_deadSpotPositionY = 0f;
                m_deadSpotPositionZ = 0f;
                m_deadSpotRuneCount = 0;
                m_lastSiteOfGraceRestedAt = 0;
            }

            m_bosses ??= new List<BossSaveData>();
            m_sitesOfGrace ??= new List<SiteOfGraceSaveData>();
            m_rightHandWeaponSlot01 ??= CreateLegacyWeapon(1);
            m_rightHandWeaponSlot02 ??= CreateLegacyWeapon(2);
            m_rightHandWeaponSlot03 ??= CreateLegacyWeapon(0);
            m_leftHandWeaponSlot01 ??= CreateLegacyWeapon(3);
            m_leftHandWeaponSlot02 ??= CreateLegacyWeapon(2);
            m_leftHandWeaponSlot03 ??= CreateLegacyWeapon(0);
            m_mainProjectile ??= new SerializableRangeProjectile(-1, 0);
            m_secondaryProjectile ??= new SerializableRangeProjectile(-1, 0);
            m_quickSlotItem01 ??= new SerializableQuickSlotItem(-1, 0);
            m_quickSlotItem02 ??= new SerializableQuickSlotItem(-1, 0);
            m_quickSlotItem03 ??= new SerializableQuickSlotItem(-1, 0);
            m_weaponsInInventory ??= new List<SerializableWeapon>();
            m_projectilesInInventory ??=
                new List<SerializableRangeProjectile>();
            m_quickSlotItemsInInventory ??=
                new List<SerializableQuickSlotItem>();
            m_headEquipmentInInventory ??= new List<int>();
            m_bodyEquipmentInInventory ??= new List<int>();
            m_handEquipmentInInventory ??= new List<int>();
            m_legEquipmentInInventory ??= new List<int>();
            m_dataVersion = k_CurrentDataVersion;
        }

        private static SerializableWeapon CreateLegacyWeapon(int itemID)
        {
            const int mediumShieldItemID = 3;
            const int parryAshOfWarItemID = 8;
            return new SerializableWeapon(
                itemID,
                itemID == mediumShieldItemID ? parryAshOfWarItemID : -1);
        }

        private BossSaveData FindBossData(int bossID)
        {
            if (bossID <= 0 || m_bosses == null)
            {
                return null;
            }

            return m_bosses.FirstOrDefault(boss => boss?.BossID == bossID);
        }

        private SiteOfGraceSaveData FindSiteOfGraceData(int siteOfGraceID)
        {
            if (siteOfGraceID <= 0 || m_sitesOfGrace == null)
            {
                return null;
            }

            return m_sitesOfGrace.FirstOrDefault(
                site => site?.SiteOfGraceID == siteOfGraceID);
        }
    }
}
