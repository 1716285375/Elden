using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZZ
{
    /// <summary>
    /// Immediately obscures world transitions, then fades out after loading work completes.
    /// </summary>
    public class PlayerUILoadingScreenManager : MonoBehaviour
    {
        [Header("LOADING SCREEN")]
        [SerializeField] private GameObject m_loadingScreen;
        [SerializeField] private CanvasGroup m_loadingScreenCanvasGroup;
        [SerializeField, Min(0f)] private float m_fadeDelay = 0.25f;
        [SerializeField, Min(0f)] private float m_fadeDuration = 1f;

        private Coroutine m_fadeLoadingScreenCoroutine;

        /// <summary>Gets whether the persistent loading overlay is visible.</summary>
        public bool IsLoadingScreenActive => m_loadingScreen?.activeSelf == true;

        /// <summary>Gets whether one fade-out sequence is already running.</summary>
        public bool IsFadingLoadingScreen =>
            m_fadeLoadingScreenCoroutine != null;

        private void OnEnable()
        {
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            CancelFadeLoadingScreen();
        }

        /// <summary>Shows a fully opaque loading overlay without a fade-in.</summary>
        public void ActivateLoadingScreen()
        {
            if (IsLoadingScreenActive && !IsFadingLoadingScreen)
            {
                return;
            }

            CancelFadeLoadingScreen();
            if (m_loadingScreenCanvasGroup != null)
            {
                m_loadingScreenCanvasGroup.alpha = 1f;
            }

            m_loadingScreen?.SetActive(true);
        }

        /// <summary>Fades the overlay using its authored delay and duration.</summary>
        public void DeactivateLoadingScreen()
        {
            DeactivateLoadingScreen(m_fadeDelay, m_fadeDuration);
        }

        /// <summary>Fades the overlay after a custom unscaled delay.</summary>
        public void DeactivateLoadingScreen(float delay)
        {
            DeactivateLoadingScreen(delay, m_fadeDuration);
        }

        /// <summary>
        /// Waits for world loading work, then fades the overlay with unscaled time.
        /// </summary>
        public void DeactivateLoadingScreen(float delay, float duration)
        {
            if (!IsLoadingScreenActive || IsFadingLoadingScreen)
            {
                return;
            }

            m_fadeLoadingScreenCoroutine = StartCoroutine(
                FadeLoadingScreen(
                    Mathf.Max(0f, delay),
                    Mathf.Max(0f, duration)));
        }

        private IEnumerator FadeLoadingScreen(float delay, float duration)
        {
            yield return null;
            while (WorldAIManager.Instance?.IsPerformingLoadingOperation == true)
            {
                yield return null;
            }

            float delayElapsed = 0f;
            while (delayElapsed < delay)
            {
                delayElapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            float fadeElapsed = 0f;
            while (fadeElapsed < duration)
            {
                fadeElapsed += Time.unscaledDeltaTime;
                if (m_loadingScreenCanvasGroup != null)
                {
                    m_loadingScreenCanvasGroup.alpha = Mathf.Lerp(
                        1f,
                        0f,
                        Mathf.Clamp01(fadeElapsed / duration));
                }

                yield return null;
            }

            if (m_loadingScreenCanvasGroup != null)
            {
                m_loadingScreenCanvasGroup.alpha = 0f;
            }

            m_loadingScreen?.SetActive(false);
            m_fadeLoadingScreenCoroutine = null;
        }

        private void OnActiveSceneChanged(Scene previousScene, Scene activeScene)
        {
            int worldSceneIndex =
                WorldSaveGameManager.Instance?.GetWorldSceneIndex() ?? -1;
            if (activeScene.buildIndex == worldSceneIndex)
            {
                DeactivateLoadingScreen();
            }
        }

        private void CancelFadeLoadingScreen()
        {
            if (m_fadeLoadingScreenCoroutine == null)
            {
                return;
            }

            StopCoroutine(m_fadeLoadingScreenCoroutine);
            m_fadeLoadingScreenCoroutine = null;
        }
    }
}
