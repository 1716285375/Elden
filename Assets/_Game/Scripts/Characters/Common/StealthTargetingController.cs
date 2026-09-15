using System.Collections.Generic;

namespace ZZ
{
    /// <summary>
    /// Owns the target registry and concealment volumes that decide whether a character is hidden.
    /// Kept out of the combat manager because neither concern is combat logic.
    /// </summary>
    public sealed class StealthTargetingController
    {
        private readonly CharacterManager m_character;
        private readonly List<CharacterManager> m_charactersTargetingMe = new();
        private readonly List<StealthObject> m_stealthObjectsCurrentlyStandingIn = new();

        public StealthTargetingController(CharacterManager character)
        {
            m_character = character;
        }

        /// <summary>Gets the living characters currently treating this character as a target.</summary>
        public IReadOnlyList<CharacterManager> CharactersTargetingMe
        {
            get
            {
                RemoveInvalidTargetingCharacters();
                return m_charactersTargetingMe;
            }
        }

        /// <summary>Gets whether no living character currently has this character targeted.</summary>
        public bool IsHidden
        {
            get
            {
                RemoveInvalidTargetingCharacters();
                bool isHidden = m_charactersTargetingMe.Count == 0;
                SyncHiddenState(isHidden);
                return isHidden;
            }
        }

        /// <summary>Gets whether the character currently overlaps any authored concealment volume.</summary>
        public bool IsInsideStealthObject
        {
            get
            {
                m_stealthObjectsCurrentlyStandingIn.RemoveAll(
                    stealthObject => stealthObject == null ||
                        !stealthObject.isActiveAndEnabled);
                return m_stealthObjectsCurrentlyStandingIn.Count > 0;
            }
        }

        /// <summary>Registers one character that has selected this character as its target.</summary>
        public void AddCharacterTargetingMe(CharacterManager targetingCharacter)
        {
            RemoveInvalidTargetingCharacters();
            if (targetingCharacter == null ||
                targetingCharacter == m_character ||
                m_charactersTargetingMe.Contains(targetingCharacter))
            {
                return;
            }

            m_charactersTargetingMe.Add(targetingCharacter);
            SyncHiddenState(false);
        }

        /// <summary>Removes one character that no longer targets this character.</summary>
        public void RemoveCharacterTargetingMe(CharacterManager targetingCharacter)
        {
            m_charactersTargetingMe.Remove(targetingCharacter);
            RemoveInvalidTargetingCharacters();
            SyncHiddenState(m_charactersTargetingMe.Count == 0);
        }

        /// <summary>Registers an authored concealment volume currently containing this character.</summary>
        public void AddStealthObject(StealthObject stealthObject)
        {
            if (stealthObject != null &&
                !m_stealthObjectsCurrentlyStandingIn.Contains(stealthObject))
            {
                m_stealthObjectsCurrentlyStandingIn.Add(stealthObject);
            }
        }

        /// <summary>Removes an authored concealment volume that no longer contains this character.</summary>
        public void RemoveStealthObject(StealthObject stealthObject)
        {
            m_stealthObjectsCurrentlyStandingIn.Remove(stealthObject);
        }

        /// <summary>Drops every registration when the character is despawned or reused.</summary>
        public void Clear()
        {
            m_charactersTargetingMe.Clear();
            m_stealthObjectsCurrentlyStandingIn.Clear();
        }

        private void RemoveInvalidTargetingCharacters()
        {
            m_charactersTargetingMe.RemoveAll(targetingCharacter =>
                targetingCharacter == null ||
                targetingCharacter.IsDead ||
                !targetingCharacter.gameObject.activeInHierarchy);
        }

        private void SyncHiddenState(bool isHidden)
        {
            m_character?.CharacterNetworkManager?.SetHiddenState(isHidden);
        }
    }
}
