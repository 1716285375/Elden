using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Hands camera and audio output to the authored menu presentation for as long as
    /// the title Scene is alive, then hands both back to the persistent gameplay rig.
    /// </summary>
    /// <remarks>
    /// <see cref="PlayerCamera"/> is a <c>DontDestroyOnLoad</c> gameplay rig. Once the
    /// host player spawns, <see cref="PlayerManager"/> binds it in <c>LateUpdate</c>
    /// and drives it every frame, so it teleports onto the player and follows them.
    /// Press Start starts the host, which means the menu would otherwise be rendered
    /// from a camera that is chasing a spawned player.
    /// This component keeps the two rigs mutually exclusive instead of fighting over
    /// the same output. It lives on the menu presentation root so leaving the title
    /// Scene destroys it and restores the gameplay rig automatically.
    /// </remarks>
    public class TitleScreenCameraCoordinator : MonoBehaviour
    {
        [Header("Menu Presentation")]
        [SerializeField] private Camera m_menuCamera;
        [SerializeField] private AudioListener m_menuAudioListener;

        private Camera m_gameplayCamera;
        private AudioListener m_gameplayAudioListener;
        private bool m_hasMutedGameplayCamera;
        private bool m_hasMutedGameplayAudio;

        private void Start()
        {
            ResolveMenuReferences();
            ResolveGameplayReferences();

            if (m_gameplayCamera != null)
            {
                m_gameplayCamera.enabled = false;
                m_hasMutedGameplayCamera = true;
            }

            // Only silence the gameplay listener once a menu listener is guaranteed to
            // replace it, otherwise the Scene is left with no audio output at all.
            if (m_menuAudioListener != null && m_gameplayAudioListener != null)
            {
                m_gameplayAudioListener.enabled = false;
                m_hasMutedGameplayAudio = true;
            }

            if (m_menuCamera != null)
            {
                m_menuCamera.enabled = true;
            }

            if (m_menuAudioListener != null)
            {
                m_menuAudioListener.enabled = true;
            }

            LogState("menu presentation took over");
        }

        private void OnDestroy()
        {
            if (m_hasMutedGameplayCamera && m_gameplayCamera != null)
            {
                m_gameplayCamera.enabled = true;
            }

            if (m_hasMutedGameplayAudio && m_gameplayAudioListener != null)
            {
                m_gameplayAudioListener.enabled = true;
            }

            if (m_hasMutedGameplayCamera || m_hasMutedGameplayAudio)
            {
                LogState("gameplay rig restored");
            }
        }

        private void ResolveMenuReferences()
        {
            m_menuCamera ??= GetComponentInChildren<Camera>(true);
            if (m_menuCamera != null)
            {
                m_menuAudioListener ??= m_menuCamera.GetComponent<AudioListener>();
            }
        }

        private void ResolveGameplayReferences()
        {
            m_gameplayCamera = PlayerCamera.Instance != null
                ? PlayerCamera.Instance.CameraObject
                : null;

            if (m_gameplayCamera != null)
            {
                m_gameplayAudioListener = m_gameplayCamera.GetComponent<AudioListener>();
            }
        }

        private void LogState(string transition)
        {
            Debug.Log(
                $"[TitleScreenCameraCoordinator] {transition}. " +
                $"menu camera='{(m_menuCamera != null ? m_menuCamera.name : "none")}' " +
                $"enabled={(m_menuCamera != null && m_menuCamera.enabled)}; " +
                $"gameplay camera='{(m_gameplayCamera != null ? m_gameplayCamera.name : "none")}' " +
                $"enabled={(m_gameplayCamera != null && m_gameplayCamera.enabled)}; " +
                $"menu listener enabled={(m_menuAudioListener != null && m_menuAudioListener.enabled)}; " +
                $"gameplay listener enabled=" +
                $"{(m_gameplayAudioListener != null && m_gameplayAudioListener.enabled)}.");
        }
    }
}
