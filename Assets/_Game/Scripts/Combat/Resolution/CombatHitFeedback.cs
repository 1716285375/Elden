using UnityEngine;
using UnityEngine.InputSystem;

namespace ZZ
{
    /// <summary>Local impact presentation with bounded animation pause and owner-only haptics.</summary>
    [DisallowMultipleComponent]
    public sealed class CombatHitFeedback : MonoBehaviour
    {
        private const float k_MaximumContinuousPauseSeconds = 0.1f;
        private Animator m_animator;
        private float m_savedSpeed;
        private float m_resumeAt;
        private float m_pauseStartedAt;
        private bool m_isPaused;
        private Gamepad m_gamepad;

        /// <summary>Presents a settled hit: bounded animation pause, local shake and haptics.</summary>
        public static void Present(in HitResult result)
        {
            if (!Application.isPlaying || !result.WasAccepted)
            {
                return;
            }

            float strength = Mathf.Clamp01(result.PoiseDamage / 50f);
            PauseCharacter(result.Attacker, strength);
            if (result.Defender != result.Attacker)
            {
                PauseCharacter(result.Defender, strength);
            }

            if (IsLocalPlayer(result.Attacker) || IsLocalPlayer(result.Defender))
            {
                PlayerCamera.Instance?.Shake(Mathf.Lerp(0.2f, 0.8f, strength), 0.12f);
            }
        }

        private static bool IsLocalPlayer(CharacterManager character)
        {
            return character is PlayerManager && character.IsOwner;
        }

        private static void PauseCharacter(CharacterManager character, float strength)
        {
            if (character == null || !character.gameObject.activeInHierarchy ||
                character.CharacterNetworkManager?.IsFrozen.Value == true)
            {
                return;
            }

            if (!character.TryGetComponent(out CombatHitFeedback feedback))
            {
                feedback = character.gameObject.AddComponent<CombatHitFeedback>();
            }

            feedback.Play(strength, character is PlayerManager && character.IsOwner);
        }

        private void Awake()
        {
            m_animator = GetComponentInChildren<Animator>();
        }

        private void Update()
        {
            if (Time.unscaledTime >= m_resumeAt || Time.timeScale <= 0f ||
                m_isPaused && !GameSettings.HitStopEnabled ||
                m_gamepad != null && !GameSettings.HapticsEnabled)
            {
                Stop();
            }
        }

        private void OnDisable() => Stop();

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                Stop();
            }
        }

        /// <summary>Restores the captured animation speed and releases vibration on interruption.</summary>
        public void Stop()
        {
            if (m_isPaused && m_animator != null)
            {
                m_animator.speed = m_savedSpeed;
            }

            m_isPaused = false;
            m_resumeAt = 0f;
            if (m_gamepad != null && m_gamepad.added)
            {
                m_gamepad.SetMotorSpeeds(0f, 0f);
            }

            m_gamepad = null;
        }

        private void Play(float strength, bool isLocalPlayer)
        {
            if (m_animator == null || Time.timeScale <= 0f)
            {
                return;
            }

            if (!m_isPaused && GameSettings.HitStopEnabled)
            {
                m_savedSpeed = m_animator.speed;
                m_isPaused = true;
                m_pauseStartedAt = Time.unscaledTime;
            }

            if (m_isPaused)
            {
                m_animator.speed = 0f;
            }
            float duration = Mathf.Lerp(0.025f, 0.065f, Mathf.Clamp01(strength));
            m_resumeAt = m_isPaused
                ? Mathf.Min(Time.unscaledTime + duration, m_pauseStartedAt + k_MaximumContinuousPauseSeconds)
                : Time.unscaledTime + duration;
            if (isLocalPlayer && GameSettings.HapticsEnabled && Gamepad.current != null)
            {
                m_gamepad = Gamepad.current;
                m_gamepad.SetMotorSpeeds(0.15f + strength * 0.25f, 0.3f + strength * 0.3f);
            }
        }
    }
}
