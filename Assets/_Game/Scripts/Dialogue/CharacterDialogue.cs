using System.Collections.Generic;
using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Stores one authored stage of linear NPC dialogue while a runtime copy owns playback progress.
    /// </summary>
    [GameAsset(
        MenuName = "Character Dialogue/Character Dialogue",
        FileName = "New Character Dialogue")]
    public class CharacterDialogue : ScriptableObject
    {
        [Header("Stage")]
        [SerializeField, Min(0)] private int m_requiredStageID;
        [SerializeField] private bool m_setStageAfterDialogue;
        [SerializeField, Min(0)] private int m_stageIDToSet;
        [SerializeField] private DialogueEndEvent m_dialogueEndEvent;

        [Header("Greeting")]
        [SerializeField] private List<string> m_greetingStrings = new();
        [SerializeField] private List<AudioClip> m_greetingAudioClips = new();

        [Header("Core Dialogue")]
        [SerializeField] private List<string> m_dialogueStrings = new();
        [SerializeField] private List<AudioClip> m_dialogueAudioClips = new();

        [Header("Farewell")]
        [SerializeField] private List<string> m_farewellStrings = new();
        [SerializeField] private List<AudioClip> m_farewellAudioClips = new();

        [System.NonSerialized] private int m_dialogueIndex;

        public int RequiredStageID => m_requiredStageID;
        public bool SetStageAfterDialogue => m_setStageAfterDialogue;
        public int StageIDToSet => m_stageIDToSet;
        public DialogueEndEvent DialogueEndEvent => m_dialogueEndEvent;
        public int DialogueIndex => m_dialogueIndex;
        public int CoreLineCount => m_dialogueStrings?.Count ?? 0;

        /// <summary>Creates a non-persistent playback copy so authored assets stay immutable.</summary>
        public CharacterDialogue CreateRuntimeCopy()
        {
            CharacterDialogue runtimeDialogue = Instantiate(this);
            runtimeDialogue.name = $"{name} (Runtime)";
            runtimeDialogue.hideFlags = HideFlags.DontSave;
            runtimeDialogue.ResetDialogueProgress();
            return runtimeDialogue;
        }

        /// <summary>Returns one random authored greeting with its matching voice clip.</summary>
        public bool TryGetRandomGreeting(
            out string subtitle,
            out AudioClip audioClip)
        {
            return TryGetRandomLine(
                m_greetingStrings,
                m_greetingAudioClips,
                out subtitle,
                out audioClip);
        }

        /// <summary>Returns the current sequential core line without advancing it.</summary>
        public bool TryGetCurrentDialogueLine(
            out string subtitle,
            out AudioClip audioClip)
        {
            subtitle = string.Empty;
            audioClip = null;
            if (!HasMatchingLineCounts(
                    m_dialogueStrings,
                    m_dialogueAudioClips) ||
                m_dialogueIndex < 0 ||
                m_dialogueIndex >= m_dialogueStrings.Count)
            {
                return false;
            }

            subtitle = m_dialogueStrings[m_dialogueIndex];
            audioClip = m_dialogueAudioClips[m_dialogueIndex];
            return true;
        }

        /// <summary>Returns one random authored farewell with its matching voice clip.</summary>
        public bool TryGetRandomFarewell(
            out string subtitle,
            out AudioClip audioClip)
        {
            return TryGetRandomLine(
                m_farewellStrings,
                m_farewellAudioClips,
                out subtitle,
                out audioClip);
        }

        /// <summary>Advances only after the current core line has finished or been skipped.</summary>
        public void AdvanceDialogue()
        {
            m_dialogueIndex = Mathf.Min(
                m_dialogueIndex + 1,
                CoreLineCount);
        }

        /// <summary>Returns this runtime copy to the first core line.</summary>
        public void ResetDialogueProgress()
        {
            m_dialogueIndex = 0;
        }

        /// <summary>Checks every subtitle-to-audio collection before playback.</summary>
        public bool ValidateDialogueData(bool logWarning)
        {
            bool isValid =
                HasMatchingLineCounts(
                    m_greetingStrings,
                    m_greetingAudioClips) &&
                HasMatchingLineCounts(
                    m_dialogueStrings,
                    m_dialogueAudioClips) &&
                HasMatchingLineCounts(
                    m_farewellStrings,
                    m_farewellAudioClips);
            if (!isValid && logWarning)
            {
                Debug.LogWarning(
                    $"Dialogue {name} requires one AudioClip per subtitle in " +
                    "Greeting, Core, and Farewell lists.",
                    this);
            }

            return isValid;
        }

        /// <summary>Extension point invoked only after the full sequence completes.</summary>
        public virtual void OnDialogueEnded()
        {
            if (m_dialogueEndEvent == DialogueEndEvent.Blacksmith)
            {
                PlayerUIManager.Instance?.PlayerUIWeaponUpgradeManager
                    ?.OpenMenuAfterFixedFrame();
            }
        }

        /// <summary>Extension point invoked when the player leaves or combat interrupts playback.</summary>
        public virtual void OnDialogueCanceled()
        {
            if (m_dialogueEndEvent == DialogueEndEvent.Blacksmith)
            {
                PlayerUIManager.Instance?.PlayerUIWeaponUpgradeManager
                    ?.CloseMenuAfterFixedFrame();
            }
        }

        private void OnValidate()
        {
            ValidateDialogueData(true);
        }

        private static bool TryGetRandomLine(
            IReadOnlyList<string> subtitles,
            IReadOnlyList<AudioClip> audioClips,
            out string subtitle,
            out AudioClip audioClip)
        {
            subtitle = string.Empty;
            audioClip = null;
            if (!HasMatchingLineCounts(subtitles, audioClips) ||
                subtitles.Count == 0)
            {
                return false;
            }

            int lineIndex = Random.Range(0, subtitles.Count);
            subtitle = subtitles[lineIndex];
            audioClip = audioClips[lineIndex];
            return true;
        }

        private static bool HasMatchingLineCounts<TFirst, TSecond>(
            IReadOnlyCollection<TFirst> firstCollection,
            IReadOnlyCollection<TSecond> secondCollection)
        {
            return firstCollection != null &&
                secondCollection != null &&
                firstCollection.Count == secondCollection.Count;
        }
    }
}
