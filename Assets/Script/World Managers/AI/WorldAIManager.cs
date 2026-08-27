using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Spawns server-owned enemies and tracks their network lifecycle in the active world.
    /// </summary>
    [DefaultExecutionOrder(-8000)]
    public class WorldAIManager : MonoBehaviour
    {
        private static WorldAIManager s_instance;

        [Header("Activation")]
        [SerializeField] private AIActivationBeacon m_aiActivationBeaconPrefab;

        [Header("Dialogue")]
        [SerializeField] private DialogueInteractable
            m_dialogueInteractablePrefab;

        private readonly List<AICharacterSpawner> m_characterSpawners = new();
        private readonly List<AICharacterManager> m_spawnedCharacters = new();
        private readonly List<AIPatrolPath> m_patrolPaths = new();

        private bool m_hasSpawnedCharacters;
        private bool m_isPerformingLoadingOperation;
        private Coroutine m_loadingOperation;

        /// <summary>Gets the World AI Manager in the currently loaded gameplay scene.</summary>
        public static WorldAIManager Instance => s_instance;

        /// <summary>Gets every currently spawned AI registered on this peer.</summary>
        public IReadOnlyList<AICharacterManager> SpawnedCharacters =>
            m_spawnedCharacters;

        /// <summary>Gets every registered spawn point in this gameplay scene.</summary>
        public IReadOnlyList<AICharacterSpawner> CharacterSpawners =>
            m_characterSpawners;

        /// <summary>Gets the registered patrol paths in the active gameplay scene.</summary>
        public IReadOnlyList<AIPatrolPath> PatrolPaths => m_patrolPaths;

        /// <summary>Gets the shared server-side beacon template used by every AI.</summary>
        public AIActivationBeacon AIActivationBeaconPrefab =>
            m_aiActivationBeaconPrefab;

        /// <summary>Gets the server-spawned reusable Talk trigger template.</summary>
        public DialogueInteractable DialogueInteractablePrefab =>
            m_dialogueInteractablePrefab;

        /// <summary>Gets whether Spawn, Reset, or Despawn work is running across frames.</summary>
        public bool IsPerformingLoadingOperation =>
            m_isPerformingLoadingOperation;

        /// <summary>Raised on each peer when a newly spawned AI joins the world registry.</summary>
        public event Action<AICharacterManager> AIRegistered;

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_instance = this;
        }

        private void Start()
        {
            StartCoroutine(SpawnWhenServerIsReady());
        }

        private void OnDestroy()
        {
            if (s_instance == this)
            {
                s_instance = null;
            }
        }

        /// <summary>Registers an AI spawned on this peer without duplicates.</summary>
        public void RegisterAI(AICharacterManager aiCharacter)
        {
            if (aiCharacter != null && !m_spawnedCharacters.Contains(aiCharacter))
            {
                m_spawnedCharacters.Add(aiCharacter);
                AIRegistered?.Invoke(aiCharacter);
            }
        }

        /// <summary>Removes a despawned AI from this peer's world list.</summary>
        public void UnregisterAI(AICharacterManager aiCharacter)
        {
            m_spawnedCharacters.Remove(aiCharacter);
        }

        /// <summary>
        /// Spawns and network-parents one Talk trigger after its AI has entered the world.
        /// </summary>
        public DialogueInteractable SpawnDialogueInteractable(
            AICharacterSoundFXManager soundFXManager)
        {
            if (!IsServerReady() ||
                soundFXManager == null ||
                soundFXManager.CharacterDialogueID ==
                    CharacterDialogueID.NoDialogue ||
                soundFXManager.InteractableDialogueObject != null)
            {
                return soundFXManager?.InteractableDialogueObject;
            }

            AICharacterManager aiCharacter =
                soundFXManager.GetComponentInParent<AICharacterManager>();
            NetworkObject aiNetworkObject =
                aiCharacter?.GetComponent<NetworkObject>();
            NetworkObject prefabNetworkObject =
                m_dialogueInteractablePrefab?.GetComponent<NetworkObject>();
            if (aiCharacter == null ||
                aiNetworkObject == null ||
                !aiNetworkObject.IsSpawned ||
                m_dialogueInteractablePrefab == null ||
                prefabNetworkObject == null)
            {
                Debug.LogWarning(
                    "Dialogue-capable AI requires a networked Dialogue Interactable prefab.",
                    this);
                return null;
            }

            DialogueInteractable dialogueInteractable = Instantiate(
                m_dialogueInteractablePrefab,
                aiCharacter.transform.position,
                aiCharacter.transform.rotation);
            dialogueInteractable.SetOwningCharacter(aiCharacter);
            NetworkObject dialogueNetworkObject =
                dialogueInteractable.GetComponent<NetworkObject>();
            dialogueNetworkObject.Spawn(true);
            if (!dialogueNetworkObject.TrySetParent(aiNetworkObject, false))
            {
                dialogueNetworkObject.Despawn(true);
                Debug.LogError(
                    $"Could not network-parent dialogue trigger to {aiCharacter.name}.",
                    aiCharacter);
                return null;
            }

            soundFXManager.RegisterDialogueInteractable(dialogueInteractable);
            return dialogueInteractable;
        }

        /// <summary>Registers one scene-authored spawner without duplicates.</summary>
        public void RegisterSpawner(AICharacterSpawner characterSpawner)
        {
            if (characterSpawner == null ||
                m_characterSpawners.Contains(characterSpawner))
            {
                return;
            }

            m_characterSpawners.Add(characterSpawner);
            if (m_hasSpawnedCharacters && IsServerReady())
            {
                characterSpawner.AttemptToSpawnCharacter();
            }
        }

        /// <summary>Removes a destroyed scene spawner from the world registry.</summary>
        public void UnregisterSpawner(AICharacterSpawner characterSpawner)
        {
            m_characterSpawners.Remove(characterSpawner);
        }

        /// <summary>Registers one scene patrol route without duplicates.</summary>
        public void AddPatrolPathToList(AIPatrolPath patrolPath)
        {
            if (patrolPath != null && !m_patrolPaths.Contains(patrolPath))
            {
                m_patrolPaths.Add(patrolPath);
            }
        }

        /// <summary>Removes one unloaded scene patrol route.</summary>
        public void RemovePatrolPathFromList(AIPatrolPath patrolPath)
        {
            m_patrolPaths.Remove(patrolPath);
        }

        /// <summary>Finds a patrol route by stable ID, or returns null for Idle fallback.</summary>
        public AIPatrolPath GetAIPatrolPathByID(int patrolPathID)
        {
            return m_patrolPaths.FirstOrDefault(path =>
                path != null && path.PatrolPathID == patrolPathID);
        }

        /// <summary>Spawns every authored AI across fixed-update frames.</summary>
        public void SpawnAllCharacters()
        {
            if (!IsServerReady() || m_hasSpawnedCharacters)
            {
                return;
            }

            StartLoadingOperation(SpawnAllCharactersRoutine());
        }

        /// <summary>Resets every reusable AI at its authored spawn point.</summary>
        public void ResetAllCharacters()
        {
            if (!IsServerReady())
            {
                Debug.LogWarning(
                    "Only the listening server can reset world AI characters.",
                    this);
                return;
            }

            StartLoadingOperation(ResetAllCharactersRoutine());
        }

        /// <summary>Ends every currently active Boss encounter on the listening server.</summary>
        public void DisableAllBossFights()
        {
            if (!IsServerReady())
            {
                return;
            }

            foreach (AICharacterManager character in m_spawnedCharacters)
            {
                character?.GetComponent<BossCharacterManager>()
                    ?.CompleteEncounter();
            }
        }

        /// <summary>Despawns every registered AI across fixed-update frames.</summary>
        public void DespawnAllCharacters()
        {
            if (!IsServerReady())
            {
                Debug.LogWarning(
                    "Only the listening server can despawn world AI characters.",
                    this);
                return;
            }

            StartLoadingOperation(DespawnAllCharactersRoutine());
        }

        private IEnumerator SpawnAllCharactersRoutine()
        {
            AICharacterSpawner[] orderedSpawners = GetOrderedSpawners();
            foreach (AICharacterSpawner characterSpawner in orderedSpawners)
            {
                characterSpawner.AttemptToSpawnCharacter();
                yield return new WaitForFixedUpdate();
            }

            m_hasSpawnedCharacters = true;
            FinishLoadingOperation();
        }

        private IEnumerator ResetAllCharactersRoutine()
        {
            AICharacterSpawner[] orderedSpawners = GetOrderedSpawners();
            foreach (AICharacterSpawner characterSpawner in orderedSpawners)
            {
                characterSpawner.ResetCharacter();
                yield return new WaitForFixedUpdate();
            }

            m_hasSpawnedCharacters = true;
            FinishLoadingOperation();
        }

        private IEnumerator DespawnAllCharactersRoutine()
        {
            AICharacterManager[] spawnedCharacters = m_spawnedCharacters
                .Where(character => character != null)
                .ToArray();
            foreach (AICharacterManager character in spawnedCharacters)
            {
                NetworkObject networkObject = character.NetworkObject;
                if (networkObject != null && networkObject.IsSpawned)
                {
                    networkObject.Despawn(true);
                }

                yield return new WaitForFixedUpdate();
            }

            m_spawnedCharacters.Clear();
            foreach (AICharacterSpawner characterSpawner in GetOrderedSpawners())
            {
                characterSpawner.ResetSpawnState();
            }

            m_hasSpawnedCharacters = false;
            FinishLoadingOperation();
        }

        private IEnumerator SpawnWhenServerIsReady()
        {
            while (!m_hasSpawnedCharacters)
            {
                if (IsServerReady())
                {
                    SpawnAllCharacters();
                    yield break;
                }

                yield return null;
            }
        }

        private void StartLoadingOperation(IEnumerator loadingOperation)
        {
            if (m_loadingOperation != null || m_isPerformingLoadingOperation)
            {
                return;
            }

            m_isPerformingLoadingOperation = true;
            Coroutine startedOperation = StartCoroutine(loadingOperation);
            if (m_isPerformingLoadingOperation)
            {
                m_loadingOperation = startedOperation;
            }
        }

        private void FinishLoadingOperation()
        {
            m_isPerformingLoadingOperation = false;
            m_loadingOperation = null;
        }

        private AICharacterSpawner[] GetOrderedSpawners()
        {
            return m_characterSpawners
                .Where(spawner => spawner != null)
                .OrderBy(spawner => spawner.transform.GetSiblingIndex())
                .ToArray();
        }

        private static bool IsServerReady()
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            return networkManager != null &&
                networkManager.IsListening &&
                networkManager.IsServer;
        }
    }
}
