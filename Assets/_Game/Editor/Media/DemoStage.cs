using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZZ.Editor
{
    /// <summary>
    /// Gets the world into a recordable state: boot a new game, then place the player and camera at
    /// a chosen pose so a clip frames something worth looking at.
    /// </summary>
    internal static class DemoStage
    {
        private const string k_YawFieldName = "m_leftAndRightLookAngle";

        /// <summary>Loads a fresh game the same way the title screen would, without touching saves.</summary>
        public static void BootIntoGameplay()
        {
            WorldSaveGameManager save = WorldSaveGameManager.Instance;
            if (save == null)
            {
                throw new System.InvalidOperationException(
                    "Demo capture needs WorldSaveGameManager. Start Play Mode from SCN_MainMenu.");
            }

            if (save.CurrentCharacterSlot != CharacterSlot.NoSlot)
            {
                throw new System.InvalidOperationException(
                    "Demo capture must start from the title screen with no save slot selected.");
            }

            FieldInfo saveDataField = typeof(WorldSaveGameManager).GetField(
                "m_currentCharacterData", BindingFlags.Instance | BindingFlags.NonPublic);
            saveDataField?.SetValue(save, new CharacterSaveData());

            TitleScreenManager title = Object.FindFirstObjectByType<TitleScreenManager>();
            if (title == null)
            {
                throw new System.InvalidOperationException(
                    "Demo capture could not find TitleScreenManager.");
            }

            title.StartNetworkAsHost();
            save.StartCoroutine(save.LoadNewGame());
        }

        /// <summary>True once the world scene is active and the locally owned player has spawned.</summary>
        public static bool IsGameplayReady()
        {
            if (SceneManager.GetActiveScene().name != WorldScenePathLayout.MasterSceneName)
            {
                return false;
            }

            if (PlayerInputManager.Instance == null)
            {
                return false;
            }

            return FindOwnerPlayer() != null;
        }

        /// <summary>Teleports the local player to a world pose and snaps the follow camera to match.</summary>
        public static void PlacePlayer(float x, float z, float yawDegrees)
        {
            PlayerManager player = FindOwnerPlayer();
            if (player == null)
            {
                return;
            }

            var target = new Vector3(x, 0f, z);
            Terrain terrain = Object.FindFirstObjectByType<Terrain>();
            if (terrain != null)
            {
                target.y = terrain.SampleHeight(target) + terrain.transform.position.y + 0.2f;
            }

            CharacterController controller = player.GetComponent<CharacterController>();
            bool wasEnabled = controller.enabled;
            controller.enabled = false;
            player.transform.position = target;
            player.transform.rotation = Quaternion.Euler(0f, yawDegrees, 0f);
            player.SetInvulnerable(true);
            controller.enabled = wasEnabled;
            Physics.SyncTransforms();

            PlayerCamera camera = PlayerCamera.Instance;
            if (camera == null)
            {
                return;
            }

            camera.SnapToPlayerAndResetRotation(player);
            FieldInfo yawField = typeof(PlayerCamera).GetField(
                k_YawFieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            yawField?.SetValue(camera, yawDegrees);
            camera.transform.rotation = Quaternion.Euler(0f, yawDegrees, 0f);
        }

        public static PlayerManager FindOwnerPlayer()
        {
            foreach (PlayerManager player in Object.FindObjectsByType<PlayerManager>(
                         FindObjectsSortMode.None))
            {
                if (player.IsOwner && player.IsSpawned)
                {
                    return player;
                }
            }

            return null;
        }
    }
}
