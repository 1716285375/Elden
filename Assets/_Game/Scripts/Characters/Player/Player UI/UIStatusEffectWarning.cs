using System.Collections;
using TMPro;
using UnityEngine;

namespace ZZ
{
    /// <summary>Presents one passive local status warning without blocking input.</summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class UIStatusEffectWarning : MonoBehaviour
    {
        [SerializeField] private CanvasGroup m_canvasGroup;
        [SerializeField] private TMP_Text m_statusText;
        [SerializeField, Min(0f)] private float m_stayDuration = 2f;
        [SerializeField, Min(0.01f)] private float m_fadeDuration = 1f;

        /// <summary>Gets the status currently presented by this runtime instance.</summary>
        public Buildup StatusType { get; private set; }

        private void Awake()
        {
            m_canvasGroup ??= GetComponent<CanvasGroup>();
            m_statusText ??= GetComponentInChildren<TMP_Text>(true);
        }

        /// <summary>Starts a two-stage status warning using unscaled UI time.</summary>
        public void Initialize(Buildup statusType)
        {
            gameObject.SetActive(true);
            m_canvasGroup ??= GetComponent<CanvasGroup>();
            m_statusText ??= GetComponentInChildren<TMP_Text>(true);
            StatusType = statusType;

            if (m_statusText != null)
            {
                m_statusText.text = GetDisplayText(statusType);
                m_statusText.color = GetDisplayColor(statusType);
            }

            if (m_canvasGroup != null)
            {
                m_canvasGroup.alpha = 1f;
            }

            StopAllCoroutines();
            StartCoroutine(DisplayWarning());
        }

        /// <summary>Returns the reusable label for one buildup-triggered status.</summary>
        public static string GetDisplayText(Buildup statusType)
        {
            return statusType switch
            {
                Buildup.Poison => "POISONED",
                Buildup.Bleed => "BLOOD LOSS",
                Buildup.Frost => "FROSTBITE",
                _ => statusType.ToString().ToUpperInvariant()
            };
        }

        /// <summary>Returns the shared presentation color for one status.</summary>
        public static Color GetDisplayColor(Buildup statusType)
        {
            switch (statusType)
            {
                case Buildup.Poison:
                    return WorldUtilityManager.Instance != null
                        ? WorldUtilityManager.Instance.PoisonColor
                        : new Color(0.34f, 0.62f, 0.2f, 1f);
                case Buildup.Frost:
                    return WorldUtilityManager.Instance != null
                        ? WorldUtilityManager.Instance.FrostColor
                        : new Color(0.25f, 0.72f, 1f, 1f);
                default:
                    return new Color(0.66f, 0.08f, 0.1f, 1f);
            }
        }

        private IEnumerator DisplayWarning()
        {
            float elapsedTime = 0f;
            while (elapsedTime < m_stayDuration)
            {
                elapsedTime += Time.unscaledDeltaTime;
                yield return null;
            }

            elapsedTime = 0f;
            float duration = Mathf.Max(0.01f, m_fadeDuration);
            while (elapsedTime < duration)
            {
                elapsedTime += Time.unscaledDeltaTime;
                if (m_canvasGroup != null)
                {
                    m_canvasGroup.alpha = Mathf.Lerp(
                        1f,
                        0f,
                        elapsedTime / duration);
                }

                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
