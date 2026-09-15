using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ZZ.Editor
{
    /// <summary>Exercises activation and resource restoration in a disposable, unsaved host session.</summary>
    public static class RestGameplaySmoke
    {
        [MenuItem("Tools/ZZ/Verify Grace Rest Resources")]
        public static void Verify()
        {
            WorldSaveGameManager save = WorldSaveGameManager.Instance;
            if (!Application.isPlaying || save == null || save.CurrentCharacterSlot != CharacterSlot.NoSlot)
            {
                throw new InvalidOperationException("Use an unsaved gameplay smoke session.");
            }
            save.StartCoroutine(Observe());
        }

        private static IEnumerator Observe()
        {
            PlayerManager player = UnityEngine.Object.FindObjectsByType<PlayerManager>(FindObjectsSortMode.None)
                .First(candidate => candidate.IsOwner);
            SiteOfGraceInteractable grace = UnityEngine.Object.FindObjectsByType<SiteOfGraceInteractable>(FindObjectsSortMode.None)
                .Where(candidate => candidate.IsSpawned).OrderBy(candidate =>
                    Vector3.Distance(candidate.transform.position, player.transform.position)).First();
            CharacterController capsule = player.GetComponent<CharacterController>();
            capsule.enabled = false;
            player.transform.position = grace.transform.position + grace.transform.forward;
            capsule.enabled = true;
            Physics.SyncTransforms();
            yield return new WaitForSecondsRealtime(1f);
            if (!grace.IsActivated)
            {
                grace.Interact(player);
                yield return new WaitForSecondsRealtime(0.3f);
            }
            var network = player.PlayerNetworkManager;
            network.CurrentHealth.Value = network.MaxHealth.Value * 0.5f;
            network.CurrentStamina.Value = network.MaxStamina.Value * 0.5f;
            network.CurrentFocusPoints.Value = 0f;
            network.RemainingHealthFlasks.Value = 0;
            network.RemainingFocusPointFlasks.Value = 0;
            grace.Interact(player);
            yield return new WaitForSecondsRealtime(4f);
            bool passed = network.CurrentHealth.Value == network.MaxHealth.Value &&
                network.CurrentFocusPoints.Value == network.MaxFocusPoints.Value &&
                network.RemainingHealthFlasks.Value == network.HealthFlaskCapacity &&
                network.RemainingFocusPointFlasks.Value == network.FocusPointFlaskCapacity;
            File.WriteAllText(".utmp/grace-rest-resource-smoke.txt",
                $"passed={passed} grace={grace.SiteOfGraceID} HP={network.CurrentHealth.Value}/{network.MaxHealth.Value} " +
                $"FP={network.CurrentFocusPoints.Value}/{network.MaxFocusPoints.Value} " +
                $"healthFlasks={network.RemainingHealthFlasks.Value}/{network.HealthFlaskCapacity} " +
                $"focusFlasks={network.RemainingFocusPointFlasks.Value}/{network.FocusPointFlaskCapacity}");
            if (!passed)
            {
                throw new InvalidOperationException("Grace resource restoration failed; inspect .utmp/grace-rest-resource-smoke.txt.");
            }
        }
    }
}
