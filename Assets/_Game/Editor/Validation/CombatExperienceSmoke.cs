using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZZ.Editor
{
    /// <summary>Runs combat checks in an unsaved Village host session and leaves user saves untouched.</summary>
    [InitializeOnLoad]
    public static class CombatExperienceSmoke
    {
        private const string k_Phase = "ZZ.CombatExperienceSmoke.Phase";
        private const string k_Start = "ZZ.CombatExperienceSmoke.Start";
        private const string k_CaptureFrame = "ZZ.CombatExperienceSmoke.CaptureFrame";
        private const string k_Report = "Docs/CombatRefresh/runtime-smoke.txt";

        static CombatExperienceSmoke()
        {
            EditorApplication.update += Tick;
        }

        [MenuItem("Tools/ZZ/Combat/Smoke Village Combat Experience")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Stop Play Mode first.");
            }
            EditorSceneManager.OpenScene(WorldScenePathLayout.MainMenuScenePath, OpenSceneMode.Single);
            SessionState.SetInt(k_Phase, 1);
            SessionState.SetFloat(k_Start, (float)EditorApplication.timeSinceStartup);
            File.WriteAllText(k_Report, "RUNNING\n");
            EditorApplication.isPlaying = true;
        }

        private static void Tick()
        {
            int phase = SessionState.GetInt(k_Phase, 0);
            if (phase == 0 || !EditorApplication.isPlaying)
            {
                return;
            }
            try
            {
                if (EditorApplication.timeSinceStartup - SessionState.GetFloat(k_Start, 0f) > 180f)
                {
                    throw new TimeoutException("Combat smoke timed out in phase " + phase);
                }
                if (phase == 1)
                {
                    if (WorldSaveGameManager.Instance == null ||
                        UnityEngine.Object.FindFirstObjectByType<TitleScreenManager>() == null)
                    {
                        return;
                    }
                    GameplayRuntimeSmoke.Start();
                    SessionState.SetInt(k_Phase, 2);
                    return;
                }
                if (phase == 2)
                {
                    if (SceneManager.GetActiveScene().path != WorldScenePathLayout.GameplayScenePath ||
                        WorldSaveGameManager.Instance.IsSceneLoadInProgress ||
                        WorldAIManager.Instance == null || WorldAIManager.Instance.IsPerformingLoadingOperation ||
                        PlayerUIManager.Instance?.PlayerUILoadingScreenManager?.IsLoadingScreenActive == true)
                    {
                        return;
                    }
                    AICharacterSpawner[] spawners = UnityEngine.Object.FindObjectsByType<AICharacterSpawner>(FindObjectsSortMode.None);
                    if (spawners.Any(spawner => spawner.InstantiatedCharacter == null))
                    {
                        return;
                    }
                    AICharacterSpawner archer = spawners.Single(spawner => spawner.name == "Village Banished Archer");
                    if (!archer.InstantiatedCharacter.IsSpawned)
                    {
                        throw new InvalidOperationException("Archer was not network spawned.");
                    }
                    // Isolate the player's contract checks from unsolicited world attacks.
                    foreach (AICharacterSpawner spawner in spawners)
                    {
                        spawner.InstantiatedCharacter.enabled = false;
                        spawner.InstantiatedCharacter.GetComponent<AICharacterCombatManager>().CloseDamageColliders();
                    }
                    File.AppendAllText(k_Report, $"SpawnedAI={spawners.Length}\nArcherNetworkSpawned=True\n");
                    EldenRingArtImport.VerifyRuntimeLockOn();
                    CombatFrameworkSmoke.Verify();
                    SessionState.SetInt(k_Phase, 3);
                    return;
                }
                if (phase == 3)
                {
                    string commitReport = File.ReadAllText(".utmp/combat-framework-smoke.txt");
                    if (commitReport.Contains("FAIL"))
                    {
                        throw new InvalidOperationException(commitReport);
                    }
                    if (!commitReport.Contains("ALL PASSED"))
                    {
                        return;
                    }
                    string lockReport = File.ReadAllText("Docs/RigCombatRepair/runtime-lock-on.txt");
                    if (!lockReport.StartsWith("PASS", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(lockReport);
                    }
                    CombatFrameworkSmoke.VerifyBufferedInput();
                    CombatFrameworkSmoke.VerifyHitResolution();
                    File.AppendAllText(k_Report, File.ReadAllText(".utmp/combat-framework-smoke.txt"));
                    WorldSaveGameManager.Instance.StartCoroutine(EnemyAttackRuntimeSmoke.Run());
                    SessionState.SetInt(k_Phase, 5);
                    return;
                }
                if (phase == 5)
                {
                    string enemyReport = File.ReadAllText(EnemyAttackRuntimeSmoke.ReportPath);
                    if (enemyReport.Contains("FAIL:"))
                    {
                        throw new InvalidOperationException(enemyReport);
                    }
                    if (!enemyReport.EndsWith("PASS\n", StringComparison.Ordinal))
                    {
                        return;
                    }
                    ScreenCapture.CaptureScreenshot(Path.GetFullPath("Docs/CombatRefresh/village-combat.png"));
                    SessionState.SetInt(k_CaptureFrame, Time.frameCount);
                    SessionState.SetInt(k_Phase, 6);
                    return;
                }
                if (phase == 6)
                {
                    if (Time.frameCount <= SessionState.GetInt(k_CaptureFrame, 0) + 1)
                    {
                        return;
                    }
                    WorldSaveGameManager.Instance.ReturnToMainMenu();
                    SessionState.SetInt(k_Phase, 4);
                    return;
                }
                if (phase == 4 && SceneManager.GetActiveScene().path == WorldScenePathLayout.MainMenuScenePath &&
                    WorldSaveGameManager.Instance?.IsSceneLoadInProgress != true)
                {
                    File.AppendAllText(k_Report, "ReturnedToMenu=True\nPASS\n");
                    Stop();
                }
            }
            catch (Exception exception)
            {
                File.AppendAllText(k_Report, "FAIL\n" + exception);
                Stop();
            }
        }

        private static void Stop()
        {
            SessionState.SetInt(k_Phase, 0);
            EditorApplication.isPlaying = false;
        }
    }
}
