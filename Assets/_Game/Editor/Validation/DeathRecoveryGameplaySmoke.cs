using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ZZ.Editor
{
    /// <summary>Verifies death penalties and revival resources without writing a character slot.</summary>
    public static class DeathRecoveryGameplaySmoke
    {
        private const string k_ReportPath = ".utmp/death-recovery-smoke.txt";

        [MenuItem("Tools/ZZ/Verify Death Recovery")]
        public static void Verify()
        {
            WorldSaveGameManager save = WorldSaveGameManager.Instance;
            if (!Application.isPlaying || save == null || save.CurrentCharacterSlot != CharacterSlot.NoSlot)
            {
                throw new InvalidOperationException("Use an unsaved gameplay smoke session.");
            }

            File.WriteAllText(k_ReportPath, "Running death recovery checks\n");
            save.StartCoroutine(Observe());
        }

        private static IEnumerator Observe()
        {
            PlayerManager player = UnityEngine.Object.FindObjectsByType<PlayerManager>(FindObjectsSortMode.None)
                .First(candidate => candidate.IsOwner);
            PlayerNetworkManager network = player.PlayerNetworkManager;
            WorldCharacterEffectsManager effects = WorldCharacterEffectsManager.Instance;
            File.AppendAllText(k_ReportPath,
                $"Effects manager={effects != null}; resource={Resources.Load<GameObject>("Effects/Dead Spot") != null}; " +
                $"resolved prefab={(effects != null && effects.DeadSpotVFX != null)}\n");
            CharacterSaveData data = WorldSaveGameManager.Instance.CurrentCharacterData;
            network.ConfigureFlaskCapacity(2, 3);
            int[] balances = { 120, 35, 0 };
            foreach (int balance in balances)
            {
                player.PlayerStatsManager.AddRunes(balance - player.PlayerStatsManager.Runes);
                network.CurrentFocusPoints.Value = 0f;
                network.RemainingHealthFlasks.Value = 0;
                network.RemainingFocusPointFlasks.Value = 0;
                network.CurrentHealth.Value = 0f;
                yield return null;
                Check(player.IsDead, $"Death registered with {balance} Runes");
                yield return null;
                PickupRunesInteractable[] spots = GetSpots(player);
                Check(spots.Length == (balance > 0 ? 1 : 0), $"Only latest recovery point: {balance}");
                Check(data.HasDeadSpot == (balance > 0) && data.DeadSpotRuneCount == balance,
                    $"Saved recovery state: {balance}");
                Check(player.PlayerStatsManager.Runes == 0, "Carried Runes transferred");
                float deadline = Time.realtimeSinceStartup + 20f;
                while (player.IsDead && Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                }

                Check(!player.IsDead, "Automatic revival completed");
                Check(network.CurrentHealth.Value == network.MaxHealth.Value &&
                    network.CurrentFocusPoints.Value == network.MaxFocusPoints.Value &&
                    network.RemainingHealthFlasks.Value == 2 && network.RemainingFocusPointFlasks.Value == 3,
                    "Revival restores HP, FP and custom flask allocation");
                yield return new WaitForSecondsRealtime(2f);
            }

            player.PlayerStatsManager.AddRunes(48);
            Check(player.PlayerCombatManager.CreateDeadSpot(player.transform.position, 48), "Create reclaim target");
            yield return null;
            PickupRunesInteractable recovery = GetSpots(player).Single();
            recovery.Interact(player);
            recovery.Interact(player);
            yield return new WaitForSecondsRealtime(0.5f);
            Check(player.PlayerStatsManager.Runes == 48, "Repeated interaction credits Runes once");
            Check(!data.HasDeadSpot && data.DeadSpotRuneCount == 0 && GetSpots(player).Length == 0,
                "Reclaim clears world and saved recovery point");
            File.AppendAllText(k_ReportPath, "ALL PASSED\n");
        }

        private static PickupRunesInteractable[] GetSpots(PlayerManager player)
        {
            return UnityEngine.Object.FindObjectsByType<PickupRunesInteractable>(FindObjectsSortMode.None)
                .Where(spot => spot.IsSpawned && spot.DeadSpotOwnerClientId == player.OwnerClientId).ToArray();
        }

        private static void Check(bool passed, string message)
        {
            File.AppendAllText(k_ReportPath, $"{(passed ? "PASS" : "FAIL")}: {message}\n");
            if (!passed)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
