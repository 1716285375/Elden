using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZZ
{
    /// <summary>
    /// Moves the real player to the LV01 greybox start once the normal session has
    /// finished placing them.
    /// </summary>
    /// <remarks>
    /// <see cref="PlayerManager"/> only warps to a spawn point when the master world
    /// Scene loads, and that spawn point belongs to the Ashen Crypt blockout. This
    /// component waits for that hand-off to finish and then relocates the player to
    /// the spawn point in this Scene, so the LV01 route can be walked without
    /// editing the master Scene's authored spawn.
    /// </remarks>
    public class GreyboxTestBootstrap : MonoBehaviour
    {
        [SerializeField] private bool m_warpPlayerToSpawnPoint = true;
        [SerializeField] private string m_spawnPointName = "Player Spawn Point";
        [SerializeField] private float m_warpDelaySeconds = 1.5f;

        private void Start()
        {
            if (m_warpPlayerToSpawnPoint)
            {
                StartCoroutine(WarpWhenPlayerIsReady());
            }
        }

        private IEnumerator WarpWhenPlayerIsReady()
        {
            yield return new WaitForSeconds(m_warpDelaySeconds);

            PlayerManager player = null;
            float timeout = Time.realtimeSinceStartup + 30f;
            while (player == null && Time.realtimeSinceStartup < timeout)
            {
                player = FindFirstObjectByType<PlayerManager>(FindObjectsInactive.Include);
                if (player == null)
                {
                    yield return new WaitForSeconds(0.25f);
                }
            }

            if (player == null)
            {
                Debug.LogWarning(
                    "[LV01Greybox] No PlayerManager appeared; the greybox start was not applied.");
                yield break;
            }

            Transform spawnPoint = FindSpawnPointInThisScene();
            if (spawnPoint == null)
            {
                Debug.LogWarning(
                    $"[LV01Greybox] This Scene has no '{m_spawnPointName}'; " +
                    "the greybox start was not applied.");
                yield break;
            }

            Warp(player, spawnPoint);
            Debug.Log(
                $"[LV01Greybox] Player placed at the greybox start {spawnPoint.position}.");
        }

        private static void Warp(PlayerManager player, Transform spawnPoint)
        {
            CharacterController controller = player.GetComponent<CharacterController>();
            if (controller != null)
            {
                controller.enabled = false;
            }

            player.transform.SetPositionAndRotation(
                spawnPoint.position, spawnPoint.rotation);

            if (controller != null)
            {
                controller.enabled = true;
            }

            PlayerCamera.Instance?.SnapToPlayerAndResetRotation(player);
        }

        private Transform FindSpawnPointInThisScene()
        {
            Scene ownScene = gameObject.scene;
            foreach (GameObject rootObject in ownScene.GetRootGameObjects())
            {
                foreach (Transform candidate in
                         rootObject.GetComponentsInChildren<Transform>(true))
                {
                    if (candidate.name == m_spawnPointName)
                    {
                        return candidate;
                    }
                }
            }

            return null;
        }
    }
}
