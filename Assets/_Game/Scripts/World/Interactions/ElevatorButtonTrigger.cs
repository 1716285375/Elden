using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ZZ
{
    /// <summary>Starts one shared elevator while characters occupy a pressure button.</summary>
    [RequireComponent(typeof(Collider))]
    public class ElevatorButtonTrigger : MonoBehaviour
    {
        private const string k_PushDownState = "PushDown";
        private const string k_PushedDownState = "PushedDown";
        private const string k_ReleaseState = "Release";

        [SerializeField] private ElevatorInteractable m_elevator;
        [SerializeField] private Animator m_buttonAnimator;
        [SerializeField, Min(0f)] private float m_pushAnimationDuration = 0.25f;
        [SerializeField, Min(0f)] private float m_minimumButtonReleaseTime = 2f;

        private readonly Dictionary<CharacterManager, int> m_overlapCounts = new();
        private readonly List<CharacterManager> m_nullCharacters = new();
        private Coroutine m_buttonRoutine;
        private bool m_buttonHasBeenPressed;

        /// <summary>Gets whether the local button is held in its pressed state.</summary>
        public bool ButtonHasBeenPressed => m_buttonHasBeenPressed;

        private void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
            m_elevator ??= GetComponentInParent<ElevatorInteractable>();
            m_buttonAnimator ??= GetComponentInChildren<Animator>(true);
        }

        private void OnDisable()
        {
            if (m_buttonRoutine != null)
            {
                StopCoroutine(m_buttonRoutine);
                m_buttonRoutine = null;
            }

            m_overlapCounts.Clear();
            m_buttonHasBeenPressed = false;
        }

        private void OnTriggerEnter(Collider other)
        {
            CharacterManager character =
                other.GetComponentInParent<CharacterManager>();
            if (character is not PlayerManager)
            {
                return;
            }

            m_overlapCounts.TryGetValue(character, out int overlapCount);
            m_overlapCounts[character] = overlapCount + 1;
            TryPressButton();
        }

        private void OnTriggerExit(Collider other)
        {
            CharacterManager character =
                other.GetComponentInParent<CharacterManager>();
            if (character == null ||
                !m_overlapCounts.TryGetValue(character, out int overlapCount))
            {
                return;
            }

            if (overlapCount > 1)
            {
                m_overlapCounts[character] = overlapCount - 1;
            }
            else
            {
                m_overlapCounts.Remove(character);
            }
        }

        private void TryPressButton()
        {
            RemoveNullCharacters();
            if (m_buttonHasBeenPressed ||
                m_overlapCounts.Count == 0 ||
                m_elevator == null ||
                m_elevator.IsMoving)
            {
                return;
            }

            m_buttonHasBeenPressed = true;
            m_buttonAnimator?.Play(k_PushDownState);
            if (m_elevator.IsServer &&
                !m_elevator.ActivateElevatorFromServer())
            {
                ReleaseButton();
                return;
            }

            m_buttonRoutine = StartCoroutine(MaintainPressedButton());
        }

        private IEnumerator MaintainPressedButton()
        {
            if (m_pushAnimationDuration > 0f)
            {
                yield return new WaitForSeconds(m_pushAnimationDuration);
            }

            m_buttonAnimator?.Play(k_PushedDownState);
            while (m_elevator != null && m_elevator.IsMoving)
            {
                yield return null;
            }

            if (m_minimumButtonReleaseTime > 0f)
            {
                yield return new WaitForSeconds(m_minimumButtonReleaseTime);
            }

            RemoveNullCharacters();
            while (m_overlapCounts.Count > 0)
            {
                RemoveNullCharacters();
                yield return null;
            }

            ReleaseButton();
        }

        private void ReleaseButton()
        {
            m_buttonAnimator?.Play(k_ReleaseState);
            m_buttonHasBeenPressed = false;
            m_buttonRoutine = null;
        }

        private void RemoveNullCharacters()
        {
            m_nullCharacters.Clear();
            foreach (CharacterManager character in m_overlapCounts.Keys)
            {
                if (character == null)
                {
                    m_nullCharacters.Add(character);
                }
            }

            foreach (CharacterManager character in m_nullCharacters)
            {
                m_overlapCounts.Remove(character);
            }
        }
    }
}
