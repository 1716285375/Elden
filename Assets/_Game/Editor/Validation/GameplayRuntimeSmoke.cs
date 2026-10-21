using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace ZZ.Editor
{
    /// <summary>Runs an unsaved host session for authored world streaming diagnostics.</summary>
    public static class GameplayRuntimeSmoke
    {
        [MenuItem("Tools/ZZ/Start Unsaved Gameplay Smoke")]
        public static void Start()
        {
            WorldSaveGameManager save = WorldSaveGameManager.Instance;
            if (!Application.isPlaying || save == null || save.CurrentCharacterSlot != CharacterSlot.NoSlot)
            {
                throw new InvalidOperationException("Start Play Mode in the main menu without selecting a save slot.");
            }
            typeof(WorldSaveGameManager).GetField("m_currentCharacterData", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(save, new CharacterSaveData());
            TitleScreenManager title = UnityEngine.Object.FindFirstObjectByType<TitleScreenManager>();
            title.StartNetworkAsHost();
            save.StartCoroutine(save.LoadNewGame());
        }

        [MenuItem("Tools/ZZ/Record Gameplay Smoke State")]
        public static void Record()
        {
            var report = new StringBuilder();
            report.AppendLine($"playing={Application.isPlaying} host={NetworkManager.Singleton?.IsHost}");
            foreach (PlayerManager player in UnityEngine.Object.FindObjectsByType<PlayerManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                report.AppendLine($"PLAYER spawned={player.IsSpawned} owner={player.IsOwner} position={player.transform.position} area={player.AreaCurrentlyIn?.name}");
            }
            foreach (AICharacterSpawner spawner in UnityEngine.Object.FindObjectsByType<AICharacterSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                AICharacterManager ai = spawner.InstantiatedCharacter;
                report.AppendLine($"SPAWN {spawner.name} boss={spawner.BossID} spawned={ai != null && ai.IsSpawned} active={ai != null && ai.gameObject.activeInHierarchy}");
            }
            foreach (Interactable interaction in UnityEngine.Object.FindObjectsByType<Interactable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                report.AppendLine($"INTERACTION {interaction.name} spawned={interaction.IsSpawned} collider={interaction.InteractableCollider?.enabled}");
            }
            Directory.CreateDirectory(".utmp");
            File.WriteAllText(".utmp/gameplay-runtime-smoke.txt", report.ToString());
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(".utmp/gameplay-smoke.png"));
        }

        [MenuItem("Tools/ZZ/Traverse Unsaved World")]
        public static void Traverse()
        {
            WorldSaveGameManager save = WorldSaveGameManager.Instance;
            if (!Application.isPlaying || save == null || save.CurrentCharacterSlot != CharacterSlot.NoSlot)
            {
                throw new InvalidOperationException("Traversal is only available in an unsaved smoke session.");
            }
            save.StartCoroutine(TraverseAreas());
        }

        [MenuItem("Tools/ZZ/Capture Gameplay Menu")]
        public static void CaptureMenu()
        {
            PlayerUIManager.Instance.PlayerUICharacterMenuManager.OpenCharacterMenu();
            PlayerUIManager.Instance.StartCoroutine(CaptureMenuAfterTransition());
        }

        [MenuItem("Tools/ZZ/Verify Backstep Audio")]
        public static void VerifyBackstepAudio()
        {
            PlayerManager player = UnityEngine.Object.FindObjectsByType<PlayerManager>(FindObjectsSortMode.None)
                .First(candidate => candidate.IsOwner);
            AudioSource source = player.CharacterSoundFXManager.GetComponent<AudioSource>();
            source.Stop();
            player.PlayerAnimatorManager.PlayTargetActionAnimation(CharacterActionAnimation.BackStep, true, true);
            Directory.CreateDirectory(".utmp");
            File.WriteAllText(".utmp/backstep-audio-smoke.txt", $"backstepAudioPlaying={source.isPlaying} spatialBlend={source.spatialBlend}");
            if (!source.isPlaying)
            {
                throw new InvalidOperationException("Backstep did not start its action sound.");
            }
        }

        private static IEnumerator CaptureMenuAfterTransition()
        {
            yield return new WaitForSecondsRealtime(0.3f);
            var report = new StringBuilder();
            report.AppendLine($"eventSystem={UnityEngine.EventSystems.EventSystem.current?.name} selected={UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject?.name} menuOpen={PlayerUIManager.Instance.IsMenuWindowOpen}");
            foreach (UnityEngine.UI.Button button in PlayerUIManager.Instance.PlayerUICharacterMenuManager
                         .GetComponentsInChildren<UnityEngine.UI.Button>(true))
            {
                var rect = (RectTransform)button.transform;
                report.AppendLine($"{button.name} active={button.gameObject.activeInHierarchy} interactable={button.IsInteractable()} pos={rect.anchoredPosition} size={rect.sizeDelta}");
            }
            File.WriteAllText(".utmp/gameplay-menu-state.txt", report.ToString());
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(".utmp/gameplay-menu.png"));
        }

        private static IEnumerator TraverseAreas()
        {
            PlayerManager player = UnityEngine.Object.FindObjectsByType<PlayerManager>(FindObjectsSortMode.None)
                .First(candidate => candidate.IsOwner);
            Terrain terrain = UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None)
                .First(candidate => candidate.name == "LandTerrain");
            Vector3 previousPosition = player.transform.position;
            bool wasInvulnerable = player.IsInvulnerable;
            Vector2[] positions = { new(50f, 50f), new(72f, 145f), new(150f, 190f), new(229f, 180f),
                new(150f, 225f), new(64f, 240f), new(235f, 249f), new(150f, 278f) };
            var report = new StringBuilder();
            try
            {
                player.SetInvulnerable(true);
                foreach (Vector2 position in positions)
                {
                    Vector3 target = new(position.x, 0f, position.y);
                    target.y = terrain.SampleHeight(target) + terrain.transform.position.y + 1f;
                    player.GetComponent<CharacterController>().enabled = false;
                    player.transform.position = target;
                    player.GetComponent<CharacterController>().enabled = true;
                    Physics.SyncTransforms();
                    yield return new WaitForSecondsRealtime(4f);
                    AICharacterSpawner[] spawners = UnityEngine.Object.FindObjectsByType<AICharacterSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                    report.AppendLine($"AREA {player.AreaCurrentlyIn?.name} at={player.transform.position} " +
                        $"spawned={spawners.Count(spawner => spawner.InstantiatedCharacter != null)}/{spawners.Length}");
                    foreach (AICharacterSpawner boss in spawners.Where(spawner => spawner.IsBoss))
                    {
                        report.AppendLine($"BOSS {boss.BossID} spawned={boss.InstantiatedCharacter != null} active={boss.InstantiatedCharacter?.gameObject.activeInHierarchy}");
                    }
                    foreach (TreasureChestInteractable chest in UnityEngine.Object.FindObjectsByType<TreasureChestInteractable>(FindObjectsSortMode.None))
                    {
                        if (Vector3.Distance(player.transform.position, chest.transform.position) < 20f)
                        {
                            chest.Interact(player);
                            report.AppendLine($"CHEST {chest.WorldItemID} opened={chest.IsOpened.Value} collider={chest.InteractableCollider.enabled}");
                        }
                    }
                }
            }
            finally
            {
                player.SetInvulnerable(wasInvulnerable);
                player.GetComponent<CharacterController>().enabled = false;
                player.transform.position = previousPosition;
                player.GetComponent<CharacterController>().enabled = true;
                File.WriteAllText(".utmp/gameplay-traversal.txt", report.ToString());
            }
        }
    }
}
