using System.Collections.Generic;
using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Tracks platform occupancy independently from the reusable interaction Trigger.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class IsOnElevatorTrigger : MonoBehaviour
    {
        [SerializeField] private ElevatorInteractable m_elevator;

        private readonly Dictionary<CharacterManager, int> m_overlapCounts = new();

        private void Awake()
        {
            Collider occupancyCollider = GetComponent<Collider>();
            occupancyCollider.isTrigger = true;
            m_elevator ??= GetComponentInParent<ElevatorInteractable>();
        }

        private void OnDisable()
        {
            foreach (CharacterManager character in m_overlapCounts.Keys)
            {
                m_elevator?.RemoveCharacter(character);
            }

            m_overlapCounts.Clear();
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
            if (overlapCount == 0)
            {
                m_elevator?.AddCharacter(character);
            }
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
                return;
            }

            m_overlapCounts.Remove(character);
            m_elevator?.RemoveCharacter(character);
        }
    }
}
