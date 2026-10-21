using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ZZ.Editor
{
    /// <summary>Checks the authored effects through the real player's presentation entry points in an unsaved session.</summary>
    public static class AnimeVfxRuntimeSmoke
    {
        private const string k_Report = ".utmp/anime-vfx-runtime.txt";

        /// <summary>Runs transient lifetime checks and captures effects against the gameplay background.</summary>
        [MenuItem("Tools/ZZ/Art/Verify Anime VFX In Gameplay")]
        public static void Verify()
        {
            WorldSaveGameManager save = WorldSaveGameManager.Instance;
            if (!Application.isPlaying || save == null || save.CurrentCharacterSlot != CharacterSlot.NoSlot)
            {
                throw new InvalidOperationException("Start an unsaved gameplay smoke session first.");
            }
            File.WriteAllText(k_Report, "Anime VFX runtime checks\n");
            save.StartCoroutine(Observe());
        }

        private static IEnumerator Observe()
        {
            PlayerManager player = UnityEngine.Object.FindObjectsByType<PlayerManager>(FindObjectsSortMode.None)
                .First(candidate => candidate.IsOwner);
            Vector3 position = player.LockOnTransform.position;
            Vector3 right = Camera.main != null ? Camera.main.transform.right : Vector3.right;
            player.CharacterEffectsManager.PlayBloodSplatterVFX(position - right, Vector3.up);
            player.CharacterEffectsManager.PlayCriticalBloodSplatterVFX(position + right, Vector3.up);
            player.CharacterEffectsManager.PlayFlaskRestorationVFX(true);
            CombatVfx.PlayGuard(position - right * 1.8f);
            CombatVfx.PlayParry(position + right * 1.8f);
            yield return null;
            string[] names = { "BloodSplatterVFX", "Critical Impact", "Health Flask VFX", "Guard Spark", "Parry Flash" };
            GameObject[] effects = new GameObject[names.Length];
            for (int index = 0; index < names.Length; index++)
            {
                effects[index] = GameObject.Find(names[index] + "(Clone)");
                Check(effects[index] != null, names[index] + " spawned through gameplay entry point");
                Check(effects[index].GetComponentsInChildren<ParticleSystem>().Any(particles => particles.isPlaying),
                    names[index] + " particle playback started");
            }
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(".utmp/anime-vfx-gameplay.png");
            yield return new WaitForSeconds(3.2f);
            for (int index = 0; index < effects.Length; index++)
            {
                Check(effects[index] == null, names[index] + " cleaned up after playback");
            }
            File.AppendAllText(k_Report, "ALL 15 CHECKS PASSED\n");
        }

        private static void Check(bool condition, string message)
        {
            File.AppendAllText(k_Report, (condition ? "PASS: " : "FAIL: ") + message + "\n");
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
