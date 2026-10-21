using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;

namespace ZZ.Editor
{
    /// <summary>Exercises the real host loading path without creating or changing player save files.</summary>
    [InitializeOnLoad]
    public static class VillageLoadingSmoke
    {
        private const string k_PhaseKey = "ZZ.VillageSmoke.Phase";
        private const string k_StartKey = "ZZ.VillageSmoke.Start";
        private static float s_maxFrameMilliseconds;
        private static int s_lastFrame;
        private static string s_worldReport;

        static VillageLoadingSmoke()
        {
            EditorApplication.update += Tick;
        }

        [MenuItem("Tools/ZZ/Environment/Smoke Village Loading")]
        [MenuItem("Tools/ZZ/Elden Ring/Smoke Groveside Loading")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Stop Play Mode first.");
            }
            EditorSceneManager.OpenScene(WorldScenePathLayout.MainMenuScenePath, OpenSceneMode.Single);
            SessionState.SetInt(k_PhaseKey, 1);
            SessionState.SetFloat(k_StartKey, (float)EditorApplication.timeSinceStartup);
            EditorApplication.isPlaying = true;
        }

        private static void Tick()
        {
            int phase = SessionState.GetInt(k_PhaseKey, 0);
            if (phase == 0 || !EditorApplication.isPlaying) { return; }
            try
            {
                float elapsed = (float)EditorApplication.timeSinceStartup - SessionState.GetFloat(k_StartKey, 0f);
                if (elapsed > 180f) { throw new TimeoutException("Village loading smoke timed out in phase " + phase); }
                if (phase == 1)
                {
                    TitleScreenManager title = UnityEngine.Object.FindFirstObjectByType<TitleScreenManager>();
                    if (title == null || WorldSaveGameManager.Instance == null) { return; }
                    title.StartNetworkAsHost();
                    if (NetworkManager.Singleton?.IsHost != true) { throw new InvalidOperationException("Host failed to start."); }
                    s_lastFrame = Time.frameCount;
                    SessionState.SetInt(k_PhaseKey, 5);
                    return;
                }
                if (phase == 5)
                {
                    if (Time.frameCount <= s_lastFrame) { return; }
                    WorldSaveGameManager.Instance.StartCoroutine(WorldSaveGameManager.Instance.LoadNewGame());
                    SessionState.SetInt(k_PhaseKey, 2);
                    SessionState.SetFloat(k_StartKey, (float)EditorApplication.timeSinceStartup);
                    s_maxFrameMilliseconds = 0f;
                    s_lastFrame = Time.frameCount;
                    return;
                }
                if (s_lastFrame != Time.frameCount)
                {
                    s_lastFrame = Time.frameCount;
                    s_maxFrameMilliseconds = Mathf.Max(s_maxFrameMilliseconds, Time.unscaledDeltaTime * 1000f);
                }
                if (phase == 2)
                {
                    if (SceneManager.GetActiveScene().name != WorldScenePathLayout.GameplaySceneName ||
                        WorldSaveGameManager.Instance.IsSceneLoadInProgress ||
                        WorldAIManager.Instance == null || WorldAIManager.Instance.IsPerformingLoadingOperation ||
                        PlayerUIManager.Instance?.PlayerUILoadingScreenManager?.IsLoadingScreenActive == true)
                    {
                        return;
                    }
                    NetworkObject player = NetworkManager.Singleton.LocalClient?.PlayerObject;
                    if (player == null || WorldAIManager.Instance.SpawnedCharacters.Count !=
                        UnityEngine.Object.FindObjectsByType<AICharacterSpawner>(FindObjectsSortMode.None).Length)
                    {
                        return;
                    }
                    s_worldReport = $"World={SceneManager.GetActiveScene().name}\nLoadAndReadySeconds={elapsed:F3}\n" +
                        $"MaxFrameMilliseconds={s_maxFrameMilliseconds:F2}\nPlayer={player.transform.position}\n" +
                        $"Camera={Camera.main?.transform.position}; Rotation={Camera.main?.transform.eulerAngles}\n" +
                        $"SpawnedAI={WorldAIManager.Instance.SpawnedCharacters.Count}\nLoadedScenes={SceneManager.sceneCount}\n";
                    ScreenCapture.CaptureScreenshot(Path.GetFullPath(ReportFolder + "/loading-playmode.png"));
                    SessionState.SetInt(k_PhaseKey, 3);
                    SessionState.SetFloat(k_StartKey, (float)EditorApplication.timeSinceStartup);
                    return;
                }
                if (phase == 3 && elapsed > 2f)
                {
                    WorldSaveGameManager.Instance.ReturnToMainMenu();
                    SessionState.SetInt(k_PhaseKey, 4);
                    SessionState.SetFloat(k_StartKey, (float)EditorApplication.timeSinceStartup);
                    return;
                }
                if (phase == 4 && SceneManager.GetActiveScene().path == WorldScenePathLayout.MainMenuScenePath &&
                    WorldSaveGameManager.Instance?.IsSceneLoadInProgress != true)
                {
                    Finish(s_worldReport + "ReturnedToMenu=True\nPASS\n");
                }
            }
            catch (Exception exception)
            {
                Finish(s_worldReport + "FAIL\n" + exception);
            }
        }

        private static void Finish(string report)
        {
            File.WriteAllText(ReportFolder + "/loading-smoke.txt", report);
            SessionState.SetInt(k_PhaseKey, 0);
            EditorApplication.isPlaying = false;
        }

        private static string ReportFolder => WorldScenePathLayout.GameplaySceneName == "SCN_m31_03_00_00"
            ? "Docs/OriginalSingleMap" : "Docs/ArtRefresh";
    }
}
