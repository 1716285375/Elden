using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZZ
{
    /// <summary>
    /// Presents transient local-player messages independently from persistent HUD state.
    /// </summary>
    public class PlayerUIPopUpManager : MonoBehaviour
    {
        [Header("YOU DIED POPUP")]
        [SerializeField] private GameObject m_youDiedPopup;
        [SerializeField] private CanvasGroup m_popupCanvasGroup;
        [SerializeField] private TMP_Text m_backgroundText;
        [SerializeField] private TMP_Text m_popupText;

        [Header("PLAYER MESSAGE POPUP")]
        [SerializeField] private GameObject m_playerMessagePopup;
        [SerializeField] private TMP_Text m_playerMessageText;

        [Header("ITEM POPUP")]
        [SerializeField] private GameObject m_itemPopup;
        [SerializeField] private Image m_itemIcon;
        [SerializeField] private TMP_Text m_itemNameText;
        [SerializeField] private HUDNumberDisplay m_itemAmountDisplay;

        [Header("DIALOGUE POPUP")]
        [SerializeField] private GameObject m_dialoguePopup;
        [SerializeField] private TMP_Text m_dialogueSubtitleText;

        [Header("STATUS EFFECT POPUP")]
        [SerializeField] private Transform m_popupOrganizer;
        [SerializeField] private UIStatusEffectWarning m_statusEffectWarningPrefab;

        [Header("TIMING")]
        [SerializeField, Min(0.01f)] private float m_fadeInDuration = 0.8f;
        [SerializeField, Min(0f)] private float m_visibleDuration = 2f;
        [SerializeField, Min(0.01f)] private float m_fadeOutDuration = 1f;
        [SerializeField, Min(0.01f)] private float m_textStretchDuration = 3f;
        [SerializeField, Min(0f)] private float m_finalCharacterSpacing = 22f;

        private Sequence m_popupSequence;

        public bool IsPopUpWindowOpen { get; private set; }
        public bool IsDialoguePopupOpen =>
            m_dialoguePopup?.activeSelf == true;

        private void OnDisable()
        {
            HideYouDiedPopup();
            CloseDialoguePopup();
            CloseAllPopUpWindows();
        }

        /// <summary>
        /// Restarts the complete YOU DIED fade and text-stretch presentation.
        /// </summary>
        public void SendYouDiedPopup()
        {
            CloseAllPopUpWindows();
            if (m_youDiedPopup == null ||
                m_popupCanvasGroup == null ||
                m_backgroundText == null ||
                m_popupText == null)
            {
                Debug.LogWarning("The YOU DIED popup references are incomplete.", this);
                return;
            }

            KillPopupSequence();
            ResetPopupPresentation();
            m_youDiedPopup.SetActive(true);
            RefreshPopupState();
            m_popupSequence = BuildYouDiedSequence();
        }

        /// <summary>
        /// Cancels and hides the YOU DIED popup, including any in-progress animation.
        /// </summary>
        public void HideYouDiedPopup()
        {
            KillPopupSequence();
            ResetPopupPresentation();
            m_youDiedPopup?.SetActive(false);
            RefreshPopupState();
        }

        /// <summary>Displays the current interaction prompt for the locally owned player.</summary>
        public void SendPlayerMessagePopup(string message)
        {
            if (m_playerMessagePopup == null || m_playerMessageText == null)
            {
                Debug.LogWarning("The player message popup references are incomplete.", this);
                return;
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                CloseAllPopUpWindows();
                return;
            }

            m_playerMessageText.text = message;
            m_playerMessagePopup.SetActive(true);
            RefreshPopupState();
        }

        /// <summary>Displays the authored item presentation and collected amount.</summary>
        public void SendItemPopup(Item item, int amount)
        {
            if (item == null ||
                m_itemPopup == null ||
                m_itemIcon == null ||
                m_itemNameText == null ||
                m_itemAmountDisplay == null)
            {
                Debug.LogWarning("The item popup references are incomplete.", this);
                return;
            }

            ClosePlayerMessagePopup();
            m_itemIcon.sprite = item.ItemIcon;
            m_itemIcon.enabled = item.ItemIcon != null;
            m_itemNameText.text = item.ItemName;
            m_itemAmountDisplay.gameObject.SetActive(true);
            m_itemAmountDisplay.SetNumber(Mathf.Max(1, amount));
            m_itemPopup.SetActive(true);
            RefreshPopupState();
        }

        /// <summary>Creates one passive owner-only status warning under the popup organizer.</summary>
        public void SendStatusEffectPopup(Buildup statusType)
        {
            if (m_statusEffectWarningPrefab == null)
            {
                Debug.LogWarning(
                    "The status effect warning prefab is not configured.",
                    this);
                return;
            }

            Transform parent = m_popupOrganizer != null
                ? m_popupOrganizer
                : transform;
            UIStatusEffectWarning warning = Instantiate(
                m_statusEffectWarningPrefab,
                parent);
            warning.Initialize(statusType);
        }

        /// <summary>Closes only the proximity prompt without dismissing an item popup.</summary>
        public void ClosePlayerMessagePopup()
        {
            m_playerMessagePopup?.SetActive(false);
            RefreshPopupState();
        }

        /// <summary>Closes transient interaction prompts without interrupting death presentation.</summary>
        public void CloseAllPopUpWindows()
        {
            m_playerMessagePopup?.SetActive(false);
            m_itemPopup?.SetActive(false);
            RefreshPopupState();
        }

        /// <summary>Opens the persistent bottom-center subtitle presentation.</summary>
        public void SendDialoguePopup(string subtitle)
        {
            if (m_dialoguePopup == null || m_dialogueSubtitleText == null)
            {
                Debug.LogWarning(
                    "The dialogue popup references are incomplete.",
                    this);
                return;
            }

            m_playerMessagePopup?.SetActive(false);
            m_itemPopup?.SetActive(false);
            m_dialogueSubtitleText.text = subtitle ?? string.Empty;
            m_dialoguePopup.SetActive(true);
            RefreshPopupState();
        }

        /// <summary>Updates only the current subtitle without restarting playback.</summary>
        public void UpdateDialogueSubtitle(string subtitle)
        {
            if (m_dialogueSubtitleText != null)
            {
                m_dialogueSubtitleText.text = subtitle ?? string.Empty;
            }
        }

        /// <summary>Closes the dialogue presentation without affecting other popup types.</summary>
        public void CloseDialoguePopup()
        {
            if (m_dialogueSubtitleText != null)
            {
                m_dialogueSubtitleText.text = string.Empty;
            }

            m_dialoguePopup?.SetActive(false);
            RefreshPopupState();
        }

        /// <summary>
        /// Builds the YOU DIED presentation as one unscaled sequence: fade in while the text
        /// stretches, hold, then fade out.
        /// </summary>
        private Sequence BuildYouDiedSequence()
        {
            Sequence sequence = DOTween.Sequence();
            sequence.SetUpdate(true);
            sequence.Append(m_popupCanvasGroup
                .DOFade(1f, m_fadeInDuration)
                .SetEase(Ease.Linear)
                .SetUpdate(true));
            sequence.Join(DOTween.To(
                    () => m_backgroundText.characterSpacing,
                    SetPopupCharacterSpacing,
                    m_finalCharacterSpacing,
                    m_textStretchDuration)
                .SetEase(Ease.Linear)
                .SetUpdate(true));
            sequence.AppendInterval(m_visibleDuration);
            sequence.Append(m_popupCanvasGroup
                .DOFade(0f, m_fadeOutDuration)
                .SetEase(Ease.Linear)
                .SetUpdate(true));
            sequence.OnComplete(CompleteYouDiedPresentation);
            return sequence;
        }

        private void CompleteYouDiedPresentation()
        {
            m_popupSequence = null;
            m_youDiedPopup?.SetActive(false);
            RefreshPopupState();
        }

        private void SetPopupCharacterSpacing(float characterSpacing)
        {
            if (m_backgroundText != null)
            {
                m_backgroundText.characterSpacing = characterSpacing;
            }

            if (m_popupText != null)
            {
                m_popupText.characterSpacing = characterSpacing;
            }
        }

        /// <summary>
        /// Killing without completing is deliberate: <see cref="ResetPopupPresentation"/> runs
        /// straight afterwards, so a partially tweened value never survives into the next popup.
        /// </summary>
        private void KillPopupSequence()
        {
            m_popupSequence?.Kill();
            m_popupSequence = null;
        }

        private void ResetPopupPresentation()
        {
            if (m_popupCanvasGroup != null)
            {
                m_popupCanvasGroup.alpha = 0f;
            }

            if (m_backgroundText != null)
            {
                m_backgroundText.characterSpacing = 0f;
            }

            if (m_popupText != null)
            {
                m_popupText.characterSpacing = 0f;
            }
        }

        private void RefreshPopupState()
        {
            IsPopUpWindowOpen = m_youDiedPopup?.activeSelf == true ||
                m_playerMessagePopup?.activeSelf == true ||
                m_itemPopup?.activeSelf == true ||
                m_dialoguePopup?.activeSelf == true;
        }
    }
}
