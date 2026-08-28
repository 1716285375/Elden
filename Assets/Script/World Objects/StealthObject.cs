using System.Collections.Generic;
using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Marks an authored trigger volume that fully conceals an untargeted sneaking character.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class StealthObject : MonoBehaviour
    {
        private readonly Dictionary<CharacterManager, int> m_overlapCounts = new();
        private readonly List<CharacterManager> m_charactersStandingInStealthObject =
            new();

        /// <summary>Gets the live characters currently overlapping this concealment volume.</summary>
        public IReadOnlyList<CharacterManager> CharactersStandingInStealthObject
        {
            get
            {
                m_charactersStandingInStealthObject.RemoveAll(
                    character => character == null);
                return m_charactersStandingInStealthObject;
            }
        }

        private void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        private void OnValidate()
        {
            Collider stealthCollider = GetComponent<Collider>();
            if (stealthCollider != null)
            {
                stealthCollider.isTrigger = true;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            CharacterManager character =
                other != null ? other.GetComponentInParent<CharacterManager>() : null;
            if (character == null)
            {
                return;
            }

            m_overlapCounts.TryGetValue(character, out int overlapCount);
            m_overlapCounts[character] = overlapCount + 1;
            if (overlapCount == 0)
            {
                m_charactersStandingInStealthObject.Add(character);
                character.CharacterCombatManager?.AddStealthObject(this);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            CharacterManager character =
                other != null ? other.GetComponentInParent<CharacterManager>() : null;
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
            m_charactersStandingInStealthObject.Remove(character);
            character.CharacterCombatManager?.RemoveStealthObject(this);
        }

        private void OnDisable()
        {
            foreach (CharacterManager character in
                m_charactersStandingInStealthObject)
            {
                if (character != null)
                {
                    character.CharacterCombatManager?.RemoveStealthObject(this);
                }
            }

            m_overlapCounts.Clear();
            m_charactersStandingInStealthObject.Clear();
        }
    }
}
