using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZZ
{
    /// <summary>Owns death Rune transfer, persistent recovery points and scene restoration.</summary>
    public sealed class RuneRecoveryController
    {
        private readonly PlayerManager m_player;
        private Coroutine m_restoreDeadSpotRoutine;
        private PickupRunesInteractable m_activeDeadSpot;

        public RuneRecoveryController(PlayerManager player)
        {
            m_player = player;
        }

        public void Enable()
        {
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        public void Disable()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            if (m_restoreDeadSpotRoutine != null)
            {
                m_player.StopCoroutine(m_restoreDeadSpotRoutine);
                m_restoreDeadSpotRoutine = null;
            }
        }

        /// <summary>
        /// Replaces the Host's recovery point; a zero-Rune death clears the previous point.
        /// Restoring a saved point never removes carried Runes.
        /// </summary>
        public bool CreateDeadSpot(
            Vector3 position,
            int runeCount,
            bool removePlayersRunes = true)
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager?.IsHost != true ||
                m_player == null ||
                !m_player.IsOwner)
            {
                return false;
            }

            if (runeCount <= 0)
            {
                if (!removePlayersRunes)
                {
                    return false;
                }

                RemoveActiveDeadSpot();
                WorldSaveGameManager.Instance?.ClearDeadSpot(true);
                return true;
            }

            GameObject deadSpotPrefab =
                WorldCharacterEffectsManager.Instance?.DeadSpotVFX;
            if (deadSpotPrefab == null)
            {
                Debug.LogError(
                    "WorldCharacterEffectsManager is missing the Dead Spot prefab.",
                    m_player);
                return false;
            }

            GameObject deadSpotObject = Object.Instantiate(
                deadSpotPrefab,
                position,
                Quaternion.identity);
            NetworkObject networkObject =
                deadSpotObject.GetComponent<NetworkObject>();
            PickupRunesInteractable deadSpot =
                deadSpotObject.GetComponent<PickupRunesInteractable>();
            if (networkObject == null || deadSpot == null)
            {
                Debug.LogError(
                    "The Dead Spot prefab requires NetworkObject and PickupRunesInteractable.",
                    deadSpotObject);
                Object.Destroy(deadSpotObject);
                return false;
            }

            networkObject.Spawn(true);
            if (!deadSpot.InitializeDeadSpot(runeCount, m_player.OwnerClientId))
            {
                networkObject.Despawn(true);
                return false;
            }

            RemoveActiveDeadSpot();
            m_activeDeadSpot = deadSpot;
            if (!removePlayersRunes)
            {
                return true;
            }

            m_player.PlayerStatsManager?.AddRunes(-runeCount);
            WorldSaveGameManager saveGameManager = WorldSaveGameManager.Instance;
            saveGameManager?.RecordDeadSpot(position, runeCount, false);
            if (saveGameManager?.CanSaveGame == true)
            {
                saveGameManager.SaveGame();
            }

            return true;
        }

        private void RemoveActiveDeadSpot()
        {
            if (m_activeDeadSpot == null)
            {
                return;
            }

            m_activeDeadSpot.SetInteractionAvailable(false);
            if (m_activeDeadSpot.IsSpawned)
            {
                m_activeDeadSpot.NetworkObject.Despawn(true);
            }
            else
            {
                Object.Destroy(m_activeDeadSpot.gameObject);
            }

            m_activeDeadSpot = null;
        }

        /// <summary>Restores a saved Dead Spot after the gameplay Scene becomes active.</summary>
        public void RestoreDeadSpotFromSaveIfNeeded()
        {
            if (m_restoreDeadSpotRoutine == null)
            {
                m_restoreDeadSpotRoutine = m_player.StartCoroutine(
                    RestoreDeadSpotAfterSceneChange());
            }
        }

        private void OnActiveSceneChanged(Scene previousScene, Scene activeScene)
        {
            if (activeScene.buildIndex > 0)
            {
                RestoreDeadSpotFromSaveIfNeeded();
            }
        }

        private IEnumerator RestoreDeadSpotAfterSceneChange()
        {
            yield return null;

            m_restoreDeadSpotRoutine = null;
            CharacterSaveData saveData =
                WorldSaveGameManager.Instance?.CurrentCharacterData;
            if (NetworkManager.Singleton?.IsHost != true ||
                m_player == null ||
                !m_player.IsOwner ||
                SceneManager.GetActiveScene().buildIndex <= 0 ||
                m_activeDeadSpot != null ||
                saveData?.HasDeadSpot != true ||
                saveData.DeadSpotRuneCount <= 0)
            {
                yield break;
            }

            Vector3 deadSpotPosition = new Vector3(
                saveData.DeadSpotPositionX,
                saveData.DeadSpotPositionY,
                saveData.DeadSpotPositionZ);
            CreateDeadSpot(
                deadSpotPosition,
                saveData.DeadSpotRuneCount,
                false);
        }

    }
}
