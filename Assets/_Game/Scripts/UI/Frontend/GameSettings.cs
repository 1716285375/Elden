using UnityEngine;

namespace ZZ
{
    /// <summary>Persists device preferences independently of character save slots.</summary>
    public static class GameSettings
    {
        private const string k_Prefix = "ZZ.Settings.";

        /// <summary>Gets the master volume from zero to one.</summary>
        public static float Volume { get; private set; } = 1f;
        /// <summary>Gets the multiplier applied to pointer and controller camera input.</summary>
        public static float LookSensitivity { get; private set; } = 1f;
        /// <summary>Gets whether vertical camera input is inverted.</summary>
        public static bool InvertLook { get; private set; }
        /// <summary>Gets the selected quality level.</summary>
        public static int Quality { get; private set; }
        /// <summary>Gets whether borderless fullscreen is enabled.</summary>
        public static bool Fullscreen { get; private set; }
        /// <summary>Gets the maximum frame rate, or minus one for unlimited.</summary>
        public static int FrameRate { get; private set; } = 60;
        /// <summary>Gets the camera shake multiplier; zero disables shake.</summary>
        public static float ShakeIntensity { get; private set; } = 1f;
        /// <summary>Gets whether local impact vibration is allowed.</summary>
        public static bool HapticsEnabled { get; private set; } = true;
        /// <summary>Gets whether brief impact animation pauses are allowed.</summary>
        public static bool HitStopEnabled { get; private set; } = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            Volume = ClampFinite(PlayerPrefs.GetFloat(k_Prefix + "Volume", 1f), 0f, 1f);
            LookSensitivity = ClampFinite(PlayerPrefs.GetFloat(k_Prefix + "Look", 1f), 0.25f, 3f);
            InvertLook = PlayerPrefs.GetInt(k_Prefix + "Invert", 0) != 0;
            Quality = Mathf.Clamp(PlayerPrefs.GetInt(k_Prefix + "Quality", QualitySettings.GetQualityLevel()),
                0, Mathf.Max(0, QualitySettings.names.Length - 1));
            Fullscreen = PlayerPrefs.GetInt(k_Prefix + "Fullscreen", Screen.fullScreen ? 1 : 0) != 0;
            FrameRate = PlayerPrefs.GetInt(k_Prefix + "FrameRate", 60);
            ShakeIntensity = ClampFinite(PlayerPrefs.GetFloat(k_Prefix + "Shake", 1f), 0f, 1f);
            HapticsEnabled = PlayerPrefs.GetInt(k_Prefix + "Haptics", 1) != 0;
            HitStopEnabled = PlayerPrefs.GetInt(k_Prefix + "HitStop", 1) != 0;
            if (FrameRate != 30 && FrameRate != 60 && FrameRate != 120 && FrameRate != -1)
            {
                FrameRate = 60;
            }
            Apply();
        }

        /// <summary>Updates and saves the master volume immediately.</summary>
        public static void SetVolume(float value)
        {
            Volume = ClampFinite(value, 0f, 1f);
            AudioListener.volume = Volume;
            Save();
        }

        /// <summary>Updates and saves camera controls without changing the authored base speeds.</summary>
        public static void SetLook(float sensitivity, bool invert)
        {
            LookSensitivity = ClampFinite(sensitivity, 0.25f, 3f);
            InvertLook = invert;
            Save();
        }

        /// <summary>Applies and saves display options using supported quality and frame limits.</summary>
        public static void SetDisplay(int quality, bool fullscreen, int frameRate)
        {
            Quality = Mathf.Clamp(quality, 0, Mathf.Max(0, QualitySettings.names.Length - 1));
            Fullscreen = fullscreen;
            FrameRate = frameRate == 30 || frameRate == 120 || frameRate == -1 ? frameRate : 60;
            Apply();
            Save();
        }

        private static void Apply()
        {
            AudioListener.volume = Volume;
            if (QualitySettings.GetQualityLevel() != Quality)
            {
                QualitySettings.SetQualityLevel(Quality, true);
            }
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = FrameRate;
            if (!Application.isEditor)
            {
                Screen.fullScreenMode = Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            }
        }

        /// <summary>Persists impact accessibility without changing damage or stamina rules.</summary>
        public static void SetImpact(float shakeIntensity, bool hapticsEnabled, bool hitStopEnabled)
        {
            ShakeIntensity = ClampFinite(shakeIntensity, 0f, 1f);
            HapticsEnabled = hapticsEnabled;
            HitStopEnabled = hitStopEnabled;
            Save();
        }

        private static float ClampFinite(float value, float minimum, float maximum)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 1f : Mathf.Clamp(value, minimum, maximum);
        }

        private static void Save()
        {
            PlayerPrefs.SetFloat(k_Prefix + "Volume", Volume);
            PlayerPrefs.SetFloat(k_Prefix + "Look", LookSensitivity);
            PlayerPrefs.SetInt(k_Prefix + "Invert", InvertLook ? 1 : 0);
            PlayerPrefs.SetInt(k_Prefix + "Quality", Quality);
            PlayerPrefs.SetInt(k_Prefix + "Fullscreen", Fullscreen ? 1 : 0);
            PlayerPrefs.SetInt(k_Prefix + "FrameRate", FrameRate);
            PlayerPrefs.SetFloat(k_Prefix + "Shake", ShakeIntensity);
            PlayerPrefs.SetInt(k_Prefix + "Haptics", HapticsEnabled ? 1 : 0);
            PlayerPrefs.SetInt(k_Prefix + "HitStop", HitStopEnabled ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
