using UnityEngine;
using UnityEngine.SceneManagement;

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
        private const string k_TitleSceneName = "SCN_MainMenu";

        [Header("Menu Presentation")]
        [SerializeField] private Camera m_menuCamera;
        [SerializeField] private AudioListener m_menuAudioListener;

        private Camera m_gameplayCamera;
        private AudioListener m_gameplayAudioListener;
        private bool m_hasMutedGameplayCamera;
        private bool m_hasMutedGameplayAudio;
        private bool m_lastExclusiveState;

        private void Start()
        {
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            ResolveMenuReferences();
            ResolveGameplayReferences();
            RefreshPresentation(SceneManager.GetActiveScene());
        }

        private void OnDestroy()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            RestoreGameplayRig();
        }

        private void OnActiveSceneChanged(Scene previousScene, Scene activeScene)
        {
            RefreshPresentation(activeScene);
        }

        private void RefreshPresentation(Scene activeScene)
        {
            bool useMenuPresentation =
                activeScene.name == k_TitleSceneName;
            if (useMenuPresentation == m_lastExclusiveState)
            {
                return;
            }

            m_lastExclusiveState = useMenuPresentation;
            if (useMenuPresentation)
            {
                ActivateMenuPresentation();
            }
            else
            {
                RestoreGameplayRig();
            }
        }

        private void ActivateMenuPresentation()
        {
            ResolveGameplayReferences();
            if (m_gameplayCamera != null && m_gameplayCamera.enabled)
            {
                m_gameplayCamera.enabled = false;
                m_hasMutedGameplayCamera = true;
            }

            if (m_menuAudioListener != null && m_gameplayAudioListener != null &&
                m_gameplayAudioListener.enabled)
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

            m_lastExclusiveState = true;
            LogState("menu presentation took over");
        }

        private void RestoreGameplayRig()
        {
            if (m_menuCamera != null)
            {
                m_menuCamera.enabled = false;
            }

            if (m_menuAudioListener != null)
            {
                m_menuAudioListener.enabled = false;
            }

            if (m_hasMutedGameplayCamera && m_gameplayCamera != null)
            {
                m_gameplayCamera.enabled = true;
                m_hasMutedGameplayCamera = false;
            }

            if (m_hasMutedGameplayAudio && m_gameplayAudioListener != null)
            {
                m_gameplayAudioListener.enabled = true;
                m_hasMutedGameplayAudio = false;
            }

            m_lastExclusiveState = false;
            LogState("gameplay rig restored");
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
