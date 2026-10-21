using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZZ
{
    /// <summary>Owns the Site of Grace Level Up preview, validation, and commit flow.</summary>
    public class PlayerUILevelUpManager : PlayerUIMenu
    {
        private const int k_AttributeCount = 8;
        private const int k_MinimumCharacterLevel = 1;
        private const int k_MaximumAttributeValue = 99;

        [Header("LEVEL COST")]
        [SerializeField, Min(0)] private int m_baseLevelCost = 500;
        [SerializeField, Min(0)] private int m_levelCostIncrease = 50;
        [SerializeField] private int[] m_playerLevels = new int[100];

        [Header("SUMMARY")]
        [SerializeField] private TMP_Text m_characterLevelText;
        [SerializeField] private TMP_Text m_projectedCharacterLevelText;
        [SerializeField] private TMP_Text m_runesHeldText;
        [SerializeField] private TMP_Text m_projectedRunesHeldText;
        [SerializeField] private TMP_Text m_runesNeededText;

        [Header("ATTRIBUTES")]
        [SerializeField] private UICharacterAttributeSlider[] m_attributeSliders =
            new UICharacterAttributeSlider[k_AttributeCount];
        [SerializeField] private TMP_Text[] m_currentAttributeTexts =
            new TMP_Text[k_AttributeCount];
        [SerializeField] private TMP_Text[] m_projectedAttributeTexts =
            new TMP_Text[k_AttributeCount];

        [Header("ACTIONS")]
        [SerializeField] private Button m_confirmButton;

        [Header("COLORS")]
        [SerializeField] private Color m_unchangedColor = Color.white;
        [SerializeField] private Color m_affordableColor =
            new Color(0.35f, 0.65f, 1f, 1f);
        [SerializeField] private Color m_unaffordableColor =
            new Color(1f, 0.28f, 0.24f, 1f);

        private readonly int[] m_currentAttributes = new int[k_AttributeCount];
        private readonly int[] m_projectedAttributes = new int[k_AttributeCount];
        private LevelUpView m_view;
        private bool m_isConfirmationPending;
        private bool m_isInitializing;
        private int m_totalLevelUpCost;

        /// <summary>Gets the currently calculated cost for the full preview.</summary>
        public int TotalLevelUpCost => m_totalLevelUpCost;
        public bool IsConfirmationOpen => m_isConfirmationPending;

        private void Awake()
        {
            SetAllLevelsCost();
        }

        private void OnValidate()
        {
            SetAllLevelsCost();
        }

        /// <inheritdoc />
        public override void OpenMenu()
        {
            base.OpenMenu();
            if (IsMenuOpen)
            {
                EnsureToolkitMenu();
                InitializeLevelUpMenu();
            }
        }

        /// <summary>Initializes Actual and Projected values from the local player.</summary>
        public void InitializeLevelUpMenu()
        {
            PlayerManager player = PlayerUIManager.Instance?.LocalPlayer;
            if (player?.PlayerNetworkManager == null ||
                player.PlayerStatsManager == null)
            {
                CloseMenu();
                return;
            }

            CancelConfirmation();
            m_isInitializing = true;
            for (int index = 0; index < k_AttributeCount; index++)
            {
                CharacterAttribute characterAttribute =
                    (CharacterAttribute)index;
                int currentValue = GetAttributeValue(
                    player.PlayerNetworkManager,
                    characterAttribute);
                m_currentAttributes[index] = currentValue;
                m_projectedAttributes[index] = currentValue;
                if (index < m_attributeSliders.Length)
                {
                    m_attributeSliders[index]?.SetRangeAndValue(currentValue, k_MaximumAttributeValue);
                }
                SetText(m_currentAttributeTexts, index, currentValue);
                SetText(m_projectedAttributeTexts, index, currentValue);
            }

            int currentLevel = CalculateCharacterLevelBasedOnAttributes(false);
            SetText(m_characterLevelText, currentLevel);
            SetText(m_projectedCharacterLevelText, currentLevel);
            int currentRunes = player.PlayerStatsManager.Runes;
            SetText(m_runesHeldText, currentRunes);
            SetText(m_projectedRunesHeldText, currentRunes);
            SetText(m_runesNeededText, 0);
            m_totalLevelUpCost = 0;
            m_isInitializing = false;
            RefreshProjection();
            if (m_view != null)
            {
                m_view.FocusFirstAttribute();
            }
            else
            {
                m_attributeSliders[0]?.Select();
            }
        }

        /// <summary>Updates one Projected attribute and recalculates the full plan.</summary>
        public void SetProjectedAttribute(
            CharacterAttribute characterAttribute,
            int projectedValue)
        {
            if (m_isInitializing || m_isConfirmationPending)
            {
                return;
            }

            int index = (int)characterAttribute;
            if (index < 0 || index >= k_AttributeCount)
            {
                return;
            }

            m_projectedAttributes[index] = Mathf.Clamp(projectedValue,
                m_currentAttributes[index], Mathf.Max(m_currentAttributes[index], k_MaximumAttributeValue));
            SetText(m_projectedAttributeTexts, index, m_projectedAttributes[index]);
            RefreshProjection();
        }

        /// <summary>Commits one affordable preview, then immediately saves and resets it.</summary>
        public void ConfirmLevels()
        {
            PlayerManager player = PlayerUIManager.Instance?.LocalPlayer;
            if (!IsMenuOpen || player == null || player.IsDead ||
                m_view != null && !m_isConfirmationPending ||
                !player.IsOwner ||
                player.PlayerNetworkManager == null ||
                player.PlayerStatsManager == null)
            {
                return;
            }

            for (int index = 0; index < k_AttributeCount; index++)
            {
                if (GetAttributeValue(player.PlayerNetworkManager, (CharacterAttribute)index) != m_currentAttributes[index])
                {
                    InitializeLevelUpMenu();
                    m_view?.SetFeedback("属性已发生变化，请重新分配本次加点。");
                    return;
                }
            }
            RefreshProjection();
            if (CalculateCharacterLevelBasedOnAttributes(true) <= CalculateCharacterLevelBasedOnAttributes(false) ||
                m_totalLevelUpCost == int.MaxValue ||
                m_totalLevelUpCost > player.PlayerStatsManager.Runes ||
                !player.PlayerStatsManager.TrySpendRunes(m_totalLevelUpCost))
            {
                CancelConfirmation();
                m_view?.SetFeedback("升级未完成：请检查当前卢恩与等级限制。");
                return;
            }

            for (int index = 0; index < k_AttributeCount; index++)
            {
                SetAttributeValue(player.PlayerNetworkManager,
                    (CharacterAttribute)index, m_projectedAttributes[index]);
            }

            WorldSaveGameManager saveGameManager =
                WorldSaveGameManager.Instance;
            if (saveGameManager?.CanSaveGame == true)
            {
                saveGameManager.SaveGame();
            }

            InitializeLevelUpMenu();
            m_view?.SetFeedback("升级完成，属性已生效。");
        }

        /// <summary>Opens confirmation only for a changed, affordable plan.</summary>
        public void RequestConfirmation()
        {
            RefreshProjection();
            int runes = PlayerUIManager.Instance?.LocalPlayer?.PlayerStatsManager?.Runes ?? 0;
            if (!IsMenuOpen || m_isConfirmationPending || m_totalLevelUpCost == int.MaxValue ||
                m_totalLevelUpCost > runes ||
                CalculateCharacterLevelBasedOnAttributes(true) <= CalculateCharacterLevelBasedOnAttributes(false))
            {
                return;
            }
            m_isConfirmationPending = true;
            m_view?.ShowConfirmation(m_totalLevelUpCost, CalculateCharacterLevelBasedOnAttributes(true));
        }

        /// <summary>Cancels the pending commit without altering the preview or player state.</summary>
        public void CancelConfirmation()
        {
            m_isConfirmationPending = false;
            m_view?.HideConfirmation();
        }

        /// <summary>Discards the preview and returns to the grace menu.</summary>
        public void ReturnToSiteOfGrace()
        {
            CloseMenu();
            PlayerUIManager.Instance?.PlayerUISiteOfGraceManager?.OpenSiteOfGraceMenu();
        }

        /// <inheritdoc />
        public override void CloseMenu()
        {
            CancelConfirmation();
            m_totalLevelUpCost = 0;
            base.CloseMenu();
        }

        /// <summary>Calculates Actual or Projected Level from the eight attributes.</summary>
        public int CalculateCharacterLevelBasedOnAttributes(bool isProjected)
        {
            int[] attributes = isProjected
                ? GetProjectedAttributes()
                : m_currentAttributes;
            return CalculateCharacterLevel(
                attributes[0],
                attributes[1],
                attributes[2],
                attributes[3],
                attributes[4],
                attributes[5],
                attributes[6],
                attributes[7]);
        }

        /// <summary>Returns Level 1 at eight base attributes of ten.</summary>
        public static int CalculateCharacterLevel(
            int vigor,
            int mind,
            int endurance,
            int strength,
            int dexterity,
            int intelligence,
            int faith,
            int arcane = 10)
        {
            long totalAttributes = (long)Mathf.Max(0, vigor) +
                Mathf.Max(0, mind) +
                Mathf.Max(0, endurance) +
                Mathf.Max(0, strength) +
                Mathf.Max(0, dexterity) +
                Mathf.Max(0, intelligence) +
                Mathf.Max(0, faith) + Mathf.Max(0, arcane);
            long characterLevel = System.Math.Max(
                k_MinimumCharacterLevel,
                totalAttributes - 79L);
            return characterLevel >= int.MaxValue
                ? int.MaxValue
                : (int)characterLevel;
        }

        /// <summary>Generates the configurable Level 0 through Level 99 cost table.</summary>
        public void SetAllLevelsCost()
        {
            if (m_playerLevels == null || m_playerLevels.Length != 100)
            {
                m_playerLevels = new int[100];
            }

            for (int level = 0; level < m_playerLevels.Length; level++)
            {
                long levelCost = (long)Mathf.Max(0, m_baseLevelCost) +
                    (long)Mathf.Max(0, m_levelCostIncrease) * level;
                m_playerLevels[level] = levelCost >= int.MaxValue
                    ? int.MaxValue
                    : (int)levelCost;
            }
        }

        /// <summary>Accumulates only transitions after the current paid Level.</summary>
        public int CalculateLevelCost(int currentLevel, int projectedLevel)
        {
            if (m_playerLevels == null ||
                currentLevel < k_MinimumCharacterLevel ||
                projectedLevel < currentLevel ||
                projectedLevel >= m_playerLevels.Length)
            {
                return projectedLevel == currentLevel ? 0 : int.MaxValue;
            }

            long totalCost = 0L;
            for (int level = currentLevel; level < projectedLevel; level++)
            {
                totalCost += Mathf.Max(0, m_playerLevels[level]);
                if (totalCost >= int.MaxValue)
                {
                    return int.MaxValue;
                }
            }

            return (int)totalCost;
        }

        /// <summary>Applies White, Blue, or Red to one Projected stat field.</summary>
        public void ChangeTextFieldColorBasedOnStat(
            TMP_Text textField,
            int currentValue,
            int projectedValue,
            PlayerManager player)
        {
            if (textField == null)
            {
                return;
            }

            bool hasChanged = projectedValue > currentValue;
            bool canAfford = m_totalLevelUpCost != int.MaxValue &&
                m_totalLevelUpCost <=
                    (player?.PlayerStatsManager?.Runes ?? 0);
            textField.color = GetProjectedValueColor(
                hasChanged,
                canAfford,
                m_unchangedColor,
                m_affordableColor,
                m_unaffordableColor);
        }

        /// <summary>Returns the deterministic Projected-value feedback color.</summary>
        public static Color GetProjectedValueColor(
            bool hasChanged,
            bool canAfford,
            Color unchangedColor,
            Color affordableColor,
            Color unaffordableColor)
        {
            if (!hasChanged)
            {
                return unchangedColor;
            }

            return canAfford ? affordableColor : unaffordableColor;
        }

        private void RefreshProjection()
        {
            PlayerManager player = PlayerUIManager.Instance?.LocalPlayer;
            if (player?.PlayerStatsManager == null)
            {
                return;
            }

            int currentLevel = CalculateCharacterLevelBasedOnAttributes(false);
            int projectedLevel = CalculateCharacterLevelBasedOnAttributes(true);
            m_totalLevelUpCost = CalculateLevelCost(
                currentLevel,
                projectedLevel);
            int currentRunes = player.PlayerStatsManager.Runes;
            bool canAfford = m_totalLevelUpCost != int.MaxValue &&
                m_totalLevelUpCost <= currentRunes;
            long projectedRunes = m_totalLevelUpCost == int.MaxValue
                ? long.MinValue
                : (long)currentRunes - m_totalLevelUpCost;

            if (m_view != null)
            {
                m_view.Refresh(m_currentAttributes, m_projectedAttributes, currentLevel, projectedLevel,
                    currentRunes, m_totalLevelUpCost, canAfford);
                return;
            }

            SetText(m_projectedCharacterLevelText, projectedLevel);
            m_projectedRunesHeldText.text = projectedRunes == long.MinValue
                ? "LEVEL CAP"
                : projectedRunes.ToString();
            m_runesNeededText.text = m_totalLevelUpCost == int.MaxValue
                ? "LEVEL CAP"
                : m_totalLevelUpCost.ToString();
            if (m_confirmButton != null)
            {
                m_confirmButton.interactable = canAfford;
            }

            int[] projectedAttributes = GetProjectedAttributes();
            for (int index = 0; index < Mathf.Min(k_AttributeCount, m_projectedAttributeTexts.Length); index++)
            {
                ChangeTextFieldColorBasedOnStat(
                    m_projectedAttributeTexts[index],
                    m_currentAttributes[index],
                    projectedAttributes[index],
                    player);
            }

            ChangeTextFieldColorBasedOnStat(
                m_projectedCharacterLevelText,
                currentLevel,
                projectedLevel,
                player);
            bool hasCost = m_totalLevelUpCost > 0;
            Color costColor = GetProjectedValueColor(
                hasCost,
                canAfford,
                m_unchangedColor,
                m_affordableColor,
                m_unaffordableColor);
            m_projectedRunesHeldText.color = costColor;
            m_runesNeededText.color = costColor;
        }

        private int[] GetProjectedAttributes()
        {
            return m_projectedAttributes;
        }

        private void EnsureToolkitMenu()
        {
            if (m_view != null || MenuWindow == null || !Application.isPlaying)
            {
                return;
            }
            CanvasGroup legacy = MenuWindow.GetComponent<CanvasGroup>();
            if (legacy == null)
            {
                legacy = MenuWindow.AddComponent<CanvasGroup>();
            }
            legacy.alpha = 0f;
            legacy.interactable = false;
            legacy.blocksRaycasts = false;
            m_view = LevelUpView.Create(MenuWindow.transform, this);
        }

        private static int GetAttributeValue(
            PlayerNetworkManager networkManager,
            CharacterAttribute characterAttribute)
        {
            return characterAttribute switch
            {
                CharacterAttribute.Vigor => networkManager.Vitality.Value,
                CharacterAttribute.Mind => networkManager.Mind.Value,
                CharacterAttribute.Endurance => networkManager.Endurance.Value,
                CharacterAttribute.Strength => networkManager.Strength.Value,
                CharacterAttribute.Dexterity => networkManager.Dexterity.Value,
                CharacterAttribute.Intelligence =>
                    networkManager.Intelligence.Value,
                CharacterAttribute.Faith => networkManager.Faith.Value,
                CharacterAttribute.Arcane => networkManager.Arcane.Value,
                _ => 0
            };
        }

        private static void SetAttributeValue(
            PlayerNetworkManager networkManager,
            CharacterAttribute characterAttribute,
            int value)
        {
            int resolvedValue = Mathf.Clamp(value, 0, k_MaximumAttributeValue);
            switch (characterAttribute)
            {
                case CharacterAttribute.Vigor:
                    networkManager.Vitality.Value = resolvedValue;
                    break;
                case CharacterAttribute.Mind:
                    networkManager.Mind.Value = resolvedValue;
                    break;
                case CharacterAttribute.Endurance:
                    networkManager.Endurance.Value = resolvedValue;
                    break;
                case CharacterAttribute.Strength:
                    networkManager.Strength.Value = resolvedValue;
                    break;
                case CharacterAttribute.Dexterity:
                    networkManager.Dexterity.Value = resolvedValue;
                    break;
                case CharacterAttribute.Intelligence:
                    networkManager.Intelligence.Value = resolvedValue;
                    break;
                case CharacterAttribute.Faith:
                    networkManager.Faith.Value = resolvedValue;
                    break;
                case CharacterAttribute.Arcane:
                    networkManager.Arcane.Value = resolvedValue;
                    break;
                default:
                    break;
            }
        }

        private static void SetText(TMP_Text text, int value)
        {
            if (text != null)
            {
                text.text = value.ToString();
            }
        }

        private static void SetText(TMP_Text[] texts, int index, int value)
        {
            if (texts != null && index >= 0 && index < texts.Length)
            {
                SetText(texts[index], value);
            }
        }
    }
}
