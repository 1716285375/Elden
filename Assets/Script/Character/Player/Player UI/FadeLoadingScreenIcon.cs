using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ZZ
{
    /// <summary>Breathes a loading icon alpha with unscaled time.</summary>
    [RequireComponent(typeof(Image))]
    public class FadeLoadingScreenIcon : MonoBehaviour
    {
        [SerializeField, Min(0.05f)] private float m_halfCycleDuration = 0.75f;

        private Image m_loadingIcon;
        private Coroutine m_fadeIconCoroutine;

        private void Awake()
        {
            m_loadingIcon = GetComponent<Image>();
        }

        private void OnEnable()
        {
            m_loadingIcon ??= GetComponent<Image>();
            SetIconAlpha(1f);
            m_fadeIconCoroutine = StartCoroutine(FadeIcon());
        }

        private void OnDisable()
        {
            if (m_fadeIconCoroutine != null)
            {
                StopCoroutine(m_fadeIconCoroutine);
                m_fadeIconCoroutine = null;
            }

            SetIconAlpha(1f);
        }

        private IEnumerator FadeIcon()
        {
            float elapsed = 0f;
            while (true)
            {
                elapsed += Time.unscaledDeltaTime;
                float cycle = Mathf.PingPong(
                    elapsed / Mathf.Max(0.05f, m_halfCycleDuration),
                    1f);
                SetIconAlpha(1f - cycle);
                yield return null;
            }
        }

        private void SetIconAlpha(float alpha)
        {
            if (m_loadingIcon == null)
            {
                return;
            }

            Color iconColor = m_loadingIcon.color;
            iconColor.a = Mathf.Clamp01(alpha);
            m_loadingIcon.color = iconColor;
        }
    }
}
