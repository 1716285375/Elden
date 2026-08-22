using System.Collections;
using TMPro;
using UnityEngine;

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

        [Header("TIMING")]
        [SerializeField, Min(0.01f)] private float m_fadeInDuration = 0.8f;
        [SerializeField, Min(0f)] private float m_visibleDuration = 2f;
        [SerializeField, Min(0.01f)] private float m_fadeOutDuration = 1f;
        [SerializeField, Min(0.01f)] private float m_textStretchDuration = 3f;
        [SerializeField, Min(0f)] private float m_finalCharacterSpacing = 22f;

        private Coroutine m_popupRoutine;

        private void OnDisable()
        {
            HideYouDiedPopup();
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

            if (m_popupRoutine != null)
            {
                StopAllCoroutines();
            }

            ResetPopupPresentation();
            m_youDiedPopup.SetActive(true);
            m_popupRoutine = StartCoroutine(DisplayYouDiedPopup());
        }

        /// <summary>
        /// Cancels and hides the YOU DIED popup, including any in-progress animation.
        /// </summary>
        public void HideYouDiedPopup()
        {
            StopAllCoroutines();
            m_popupRoutine = null;
            ResetPopupPresentation();
            m_youDiedPopup?.SetActive(false);
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
        }

        /// <summary>Closes transient interaction prompts without interrupting death presentation.</summary>
        public void CloseAllPopUpWindows()
        {
            m_playerMessagePopup?.SetActive(false);
        }

        private IEnumerator DisplayYouDiedPopup()
        {
            StartCoroutine(StretchPopUpTextOverTime());
            yield return FadeInPopUpOverTime();
            yield return WaitThenFadeOutPopUpOverTime();

            m_youDiedPopup.SetActive(false);
            m_popupRoutine = null;
        }

        private IEnumerator FadeInPopUpOverTime()
        {
            float elapsedTime = 0f;
            float duration = Mathf.Max(0.01f, m_fadeInDuration);
            while (elapsedTime < duration)
            {
                elapsedTime += Time.unscaledDeltaTime;
                m_popupCanvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsedTime / duration);
                yield return null;
            }

            m_popupCanvasGroup.alpha = 1f;
        }

        private IEnumerator WaitThenFadeOutPopUpOverTime()
        {
            float waitTime = 0f;
            while (waitTime < m_visibleDuration)
            {
                waitTime += Time.unscaledDeltaTime;
                yield return null;
            }

            float elapsedTime = 0f;
            float duration = Mathf.Max(0.01f, m_fadeOutDuration);
            while (elapsedTime < duration)
            {
                elapsedTime += Time.unscaledDeltaTime;
                m_popupCanvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsedTime / duration);
                yield return null;
            }

            m_popupCanvasGroup.alpha = 0f;
        }

        private IEnumerator StretchPopUpTextOverTime()
        {
            float elapsedTime = 0f;
            float duration = Mathf.Max(0.01f, m_textStretchDuration);
            while (elapsedTime < duration)
            {
                elapsedTime += Time.unscaledDeltaTime;
                float characterSpacing = Mathf.Lerp(
                    0f,
                    m_finalCharacterSpacing,
                    elapsedTime / duration);
                m_backgroundText.characterSpacing = characterSpacing;
                m_popupText.characterSpacing = characterSpacing;
                yield return null;
            }

            m_backgroundText.characterSpacing = m_finalCharacterSpacing;
            m_popupText.characterSpacing = m_finalCharacterSpacing;
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
    }
}
