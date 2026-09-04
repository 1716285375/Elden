using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ZZ
{
    /// <summary>Applies the player's persisted vignette accessibility preference.</summary>
    [RequireComponent(typeof(Volume))]
    public class DungeonPostProcessingController : MonoBehaviour
    {
        private const string k_VignettePreferenceKey = "Visual.Vignette.Enabled";

        private Volume m_volume;

        /// <summary>Gets whether the local player currently permits vignette.</summary>
        public bool IsVignetteEnabled => PlayerPrefs.GetInt(
            k_VignettePreferenceKey,
            1) != 0;

        private void Awake()
        {
            m_volume = GetComponent<Volume>();
            ApplyVignettePreference();
        }

        /// <summary>Persists and immediately applies the player's vignette preference.</summary>
        public void SetVignetteEnabled(bool isEnabled)
        {
            PlayerPrefs.SetInt(k_VignettePreferenceKey, isEnabled ? 1 : 0);
            ApplyVignettePreference();
        }

        private void ApplyVignettePreference()
        {
            m_volume ??= GetComponent<Volume>();
            VolumeProfile profile = m_volume?.profile;
            if (profile != null && profile.TryGet(out Vignette vignette))
            {
                vignette.active = IsVignetteEnabled;
            }
        }
    }
}
