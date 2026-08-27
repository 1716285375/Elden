using System;
using System.Collections;
using UnityEngine;

namespace ZZ
{
    /// <summary>Owns AI voice playback and one local player's linear dialogue session.</summary>
    public class AICharacterSoundFXManager : CharacterSoundFXManager
    {
        private enum DialoguePlaybackSection
        {
            None,
            Greeting,
            Core,
            Farewell
        }

        [Header("Blocking Sounds")]
        [SerializeField] private AudioClip[] m_blockingSoundEffects =
            Array.Empty<AudioClip>();

        [Header("Dialogue")]
        [SerializeField] private CharacterDialogueID m_characterDialogueID;
        [SerializeField] private DialogueInteractable
            m_interactableDialogueObject;
        [SerializeField, Min(0f)] private float m_dialogueLineBuffer = 0.75f;

        private AICharacterManager m_aiCharacter;
        private CharacterDialogue m_currentDialogue;
        private Coroutine m_dialogueCoroutine;
        private DialoguePlaybackSection m_playbackSection;
        private bool m_dialogueIsPlaying;
        private bool m_lastDialogueAvailability;

        public CharacterDialogueID CharacterDialogueID =>
            m_characterDialogueID;
        public DialogueInteractable InteractableDialogueObject =>
            m_interactableDialogueObject;
        public CharacterDialogue CurrentDialogue => m_currentDialogue;
        public bool DialogueIsPlaying => m_dialogueIsPlaying;

        /// <inheritdoc />
        protected override void Awake()
        {
            base.Awake();
            m_aiCharacter = GetComponentInParent<AICharacterManager>();
        }

        private void Start()
        {
            ResolveCurrentDialogue();
            if (m_characterDialogueID != CharacterDialogueID.NoDialogue)
            {
                WorldAIManager.Instance?.SpawnDialogueInteractable(this);
            }
        }

        private void Update()
        {
            if (m_aiCharacter == null ||
                !m_aiCharacter.IsSpawned ||
                !m_aiCharacter.IsServer ||
                m_interactableDialogueObject == null)
            {
                return;
            }

            bool isAvailable = !m_aiCharacter.IsDead &&
                m_aiCharacter.CurrentTarget == null;
            if (isAvailable == m_lastDialogueAvailability)
            {
                return;
            }

            m_lastDialogueAvailability = isAvailable;
            m_interactableDialogueObject.SetDialogueAvailability(isAvailable);
        }

        private void OnDisable()
        {
            CancelCurrentDialogueEvent();
        }

        private void OnDestroy()
        {
            DestroyCurrentDialogueCopy();
        }

        /// <summary>Plays one random AI blocking impact through the character's audio source.</summary>
        public override void PlayBlockingSoundEffect()
        {
            WorldSoundFXManager.Instance?.PlaySoundEffect(
                m_blockingSoundEffects,
                CharacterAudioSource);
        }

        /// <summary>Starts a new sequence or skips the currently playing line.</summary>
        public void PlayCurrentDialogueEvent(PlayerManager player)
        {
            if (m_dialogueIsPlaying)
            {
                SkipCurrentDialogueLine();
                return;
            }

            if (player == null || !player.IsOwner)
            {
                return;
            }

            if (m_currentDialogue == null)
            {
                ResolveCurrentDialogue();
            }

            if (m_currentDialogue == null ||
                !m_currentDialogue.ValidateDialogueData(true))
            {
                return;
            }

            PlayerUIPopUpManager popUpManager =
                PlayerUIManager.Instance?.PlayerUIPopUpManager;
            if (popUpManager == null)
            {
                Debug.LogWarning(
                    "Dialogue playback requires a local PlayerUIPopUpManager.",
                    this);
                return;
            }

            m_currentDialogue.ResetDialogueProgress();
            m_dialogueIsPlaying = true;
            m_playbackSection = DialoguePlaybackSection.Greeting;
            popUpManager.SendDialoguePopup(string.Empty);
            PlayerUIManager.Instance?.PlayerUIHUDManager?.HideHUD();
            m_dialogueCoroutine = StartCoroutine(PlayDialogueSequence());
        }

        /// <summary>Cancels playback without applying dialogue completion or Stage progress.</summary>
        public void CancelCurrentDialogueEvent()
        {
            if (!m_dialogueIsPlaying)
            {
                return;
            }

            StopDialogueCoroutineAndAudio();
            m_currentDialogue?.OnDialogueCanceled();
            m_currentDialogue?.ResetDialogueProgress();
            m_dialogueIsPlaying = false;
            m_playbackSection = DialoguePlaybackSection.None;
            CloseDialoguePresentation();
        }

        /// <summary>Completes playback and applies authored Stage progress exactly once.</summary>
        public void OnCurrentDialogueEventEnded()
        {
            if (!m_dialogueIsPlaying || m_currentDialogue == null)
            {
                return;
            }

            StopDialogueCoroutineAndAudio();
            CharacterDialogue completedDialogue = m_currentDialogue;
            m_dialogueIsPlaying = false;
            m_playbackSection = DialoguePlaybackSection.None;
            completedDialogue.OnDialogueEnded();
            CloseDialoguePresentation();

            if (completedDialogue.SetStageAfterDialogue)
            {
                WorldSaveGameManager.Instance?.SetStageOfDialogue(
                    m_characterDialogueID,
                    completedDialogue.StageIDToSet,
                    true);
                ResolveCurrentDialogue();
            }
            else
            {
                completedDialogue.ResetDialogueProgress();
            }
        }

        /// <summary>Connects the server-created network trigger on every peer.</summary>
        public void RegisterDialogueInteractable(
            DialogueInteractable dialogueInteractable)
        {
            if (dialogueInteractable == null)
            {
                return;
            }

            m_interactableDialogueObject = dialogueInteractable;
            m_lastDialogueAvailability =
                dialogueInteractable.IsDialogueAvailable.Value;
        }

        /// <summary>Clears only the trigger currently owned by this character.</summary>
        public void UnregisterDialogueInteractable(
            DialogueInteractable dialogueInteractable)
        {
            if (m_interactableDialogueObject == dialogueInteractable)
            {
                m_interactableDialogueObject = null;
            }
        }

        private IEnumerator PlayDialogueSequence()
        {
            if (m_playbackSection == DialoguePlaybackSection.Greeting)
            {
                if (m_currentDialogue.TryGetRandomGreeting(
                        out string greeting,
                        out AudioClip greetingClip))
                {
                    yield return PlayDialogueLine(greeting, greetingClip);
                }

                m_playbackSection = DialoguePlaybackSection.Core;
            }

            while (m_playbackSection == DialoguePlaybackSection.Core &&
                m_currentDialogue.TryGetCurrentDialogueLine(
                    out string dialogueLine,
                    out AudioClip dialogueClip))
            {
                yield return PlayDialogueLine(dialogueLine, dialogueClip);
                m_currentDialogue.AdvanceDialogue();
            }

            if (m_playbackSection == DialoguePlaybackSection.Core)
            {
                m_playbackSection = DialoguePlaybackSection.Farewell;
            }

            if (m_playbackSection == DialoguePlaybackSection.Farewell &&
                m_currentDialogue.TryGetRandomFarewell(
                    out string farewell,
                    out AudioClip farewellClip))
            {
                yield return PlayDialogueLine(farewell, farewellClip);
            }

            m_dialogueCoroutine = null;
            OnCurrentDialogueEventEnded();
        }

        private IEnumerator PlayDialogueLine(
            string subtitle,
            AudioClip audioClip)
        {
            PlayerUIManager.Instance?.PlayerUIPopUpManager
                ?.UpdateDialogueSubtitle(subtitle);
            if (audioClip != null && CharacterAudioSource != null)
            {
                CharacterAudioSource.PlayOneShot(audioClip);
            }

            float duration = (audioClip != null ? audioClip.length : 0f) +
                m_dialogueLineBuffer;
            if (duration > 0f)
            {
                yield return new WaitForSeconds(duration);
            }
            else
            {
                yield return null;
            }
        }

        private void SkipCurrentDialogueLine()
        {
            StopDialogueCoroutineAndAudio();
            switch (m_playbackSection)
            {
                case DialoguePlaybackSection.Greeting:
                    m_playbackSection = DialoguePlaybackSection.Core;
                    break;
                case DialoguePlaybackSection.Core:
                    m_currentDialogue?.AdvanceDialogue();
                    break;
                case DialoguePlaybackSection.Farewell:
                    OnCurrentDialogueEventEnded();
                    return;
            }

            m_dialogueCoroutine = StartCoroutine(PlayDialogueSequence());
        }

        private void ResolveCurrentDialogue()
        {
            DestroyCurrentDialogueCopy();
            m_currentDialogue = WorldSaveGameManager.Instance
                ?.GetCurrentDialogue(m_characterDialogueID);
        }

        private void DestroyCurrentDialogueCopy()
        {
            if (m_currentDialogue == null)
            {
                return;
            }

            Destroy(m_currentDialogue);
            m_currentDialogue = null;
        }

        private void StopDialogueCoroutineAndAudio()
        {
            if (m_dialogueCoroutine != null)
            {
                StopCoroutine(m_dialogueCoroutine);
                m_dialogueCoroutine = null;
            }

            CharacterAudioSource?.Stop();
        }

        private static void CloseDialoguePresentation()
        {
            PlayerUIManager.Instance?.PlayerUIPopUpManager
                ?.CloseDialoguePopup();
            PlayerUIManager.Instance?.PlayerUIHUDManager?.ShowHUD();
        }
    }
}
