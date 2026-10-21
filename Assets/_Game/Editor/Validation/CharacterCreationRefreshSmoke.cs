using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace ZZ.Editor
{
    /// <summary>Validates the real creation UI, writes one fresh slot, then reloads it through the title flow.</summary>
    [InitializeOnLoad]
    public static class CharacterCreationRefreshSmoke
    {
        private const string k_Phase = "ZZ.CreationRefresh.Phase";
        private const string k_Start = "ZZ.CreationRefresh.Start";
        private const string k_Report = "Docs/CharacterCreationRefresh/runtime-creation.txt";
        private static int s_waitFrame;

        static CharacterCreationRefreshSmoke() { EditorApplication.update += Tick; }

        [MenuItem("Tools/ZZ/Character Creation/Reset Test Slots And Preview")]
        public static void ResetTestSlotsAndPreview()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Stop Play Mode before resetting test saves.");
            }
            Directory.CreateDirectory("Docs/CharacterCreationRefresh");
            string[] files = Directory.Exists(Application.persistentDataPath)
                ? Directory.GetFiles(Application.persistentDataPath).Where(path =>
                    System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(path),
                        @"^CharacterSlot\d{2}\.json(?:\.(?:bak|tmp))?$")).ToArray()
                : Array.Empty<string>();
            File.WriteAllLines("Docs/CharacterCreationRefresh/last-slot-reset.txt", files.Select(Path.GetFileName));
            foreach (string path in files)
            {
                File.Delete(path);
            }
            Preview();
        }

        [MenuItem("Tools/ZZ/Character Creation/Reload Fresh Saved Character")]
        public static void ReloadFreshSavedCharacter()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorSceneManager.OpenScene(WorldScenePathLayout.MainMenuScenePath, OpenSceneMode.Single);
            }
            SessionState.SetInt(k_Phase, 6);
            SessionState.SetFloat(k_Start, (float)EditorApplication.timeSinceStartup);
            File.AppendAllText(k_Report, "RESUMING DISK RELOAD THROUGH PRESS START\n");
            EditorApplication.isPlaying = true;
        }

        [MenuItem("Tools/ZZ/Character Creation/Preview Fresh Character")]
        public static void Preview()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Stop Play Mode first.");
            }
            EditorSceneManager.OpenScene(WorldScenePathLayout.MainMenuScenePath, OpenSceneMode.Single);
            SessionState.SetInt(k_Phase, 1);
            SessionState.SetFloat(k_Start, (float)EditorApplication.timeSinceStartup);
            File.WriteAllText(k_Report, "RUNNING\n");
            AllMenuPresentationSmoke.StartReviewFromTitle();
        }

        [MenuItem("Tools/ZZ/Character Creation/Commit Fresh Character")]
        public static void Commit()
        {
            var creation = Object.FindFirstObjectByType<TitleScreenCharacterCreationManager>();
            if (SessionState.GetInt(k_Phase, 0) != 3 || creation?.IsReady != true)
            {
                throw new InvalidOperationException("Wait for the fresh-character preview smoke to become ready.");
            }
            UIDocument document = creation.GetComponentsInChildren<UIDocument>()
                .Single(item => item.gameObject.name == "Character Creation Toolkit");
            document.rootVisualElement.Q<Button>("create").Focus();
            using (NavigationSubmitEvent finish = NavigationSubmitEvent.GetPooled())
            {
                document.rootVisualElement.Q<Button>("create").SendEvent(finish);
            }
            EditorApplication.delayCall += () =>
            {
                document.rootVisualElement.Q<Button>("confirm-create").Focus();
                using (NavigationSubmitEvent confirm = NavigationSubmitEvent.GetPooled())
                {
                    document.rootVisualElement.Q<Button>("confirm-create").SendEvent(confirm);
                }
                Check(!creation.IsOpen, "Confirmed UI commits the fresh character");
                SessionState.SetInt(k_Phase, 4);
                SessionState.SetFloat(k_Start, (float)EditorApplication.timeSinceStartup);
            };
        }

        private static void Tick()
        {
            int phase = SessionState.GetInt(k_Phase, 0);
            if (phase == 0 || phase == 3 || !EditorApplication.isPlaying) { return; }
            try
            {
                if (EditorApplication.timeSinceStartup - SessionState.GetFloat(k_Start, 0f) > 180f)
                {
                    throw new TimeoutException("Creation smoke timed out in phase " + phase);
                }
                var title = Object.FindFirstObjectByType<TitleScreenManager>();
                WorldSaveGameManager save = WorldSaveGameManager.Instance;
                if (phase == 1)
                {
                    if (title == null || save == null) { return; }
                    Check(Enumerable.Range(1, 10).All(index =>
                        save.GetCharacterDataForSlot((CharacterSlot)index) == null), "All old character slots are empty");
                    title.StartNetworkAsHost();
                    SessionState.SetInt(k_Phase, 2);
                    return;
                }
                if (phase == 2)
                {
                    var creation = Object.FindFirstObjectByType<TitleScreenCharacterCreationManager>();
                    if (creation == null) { return; }
                    if (!creation.IsOpen) { title.StartNewGame(); return; }
                    if (!creation.IsReady) { return; }
                    UIDocument origins = creation.GetComponentsInChildren<UIDocument>(true)
                        .Single(item => item.gameObject.name == "Character Origin Toolkit");
                    Button origin = origins.rootVisualElement.Q("origin-cards").Query<Button>().ToList().First();
                    using (NavigationSubmitEvent choose = NavigationSubmitEvent.GetPooled()) { origin.SendEvent(choose); }
                    Check(origins.rootVisualElement.Q<Label>("origin-heading").text == "选择体型",
                        "Origin confirmation opens body-type selection");
                    Button female = origins.rootVisualElement.Q("origin-cards").Query<Button>().ToList()[1];
                    female.Focus();
                    using (NavigationSubmitEvent choose = NavigationSubmitEvent.GetPooled()) { female.SendEvent(choose); }
                    Check(origins.rootVisualElement.style.display.value == DisplayStyle.None,
                        "Body-type confirmation enters appearance editing");
                    UIDocument document = creation.GetComponentsInChildren<UIDocument>()
                        .Single(item => item.gameObject.name == "Character Creation Toolkit");
                    VisualElement root = document.rootVisualElement;
                    creation.SubmitCharacterName("褪色者测试");
                    root.Q<DropdownField>("hairstyle").index = 3;
                    root.Q<DropdownField>("eyebrow").index = 2;
                    root.Q<Slider>("iris-size").value = 1.05f;
                    creation.SetEyeColor(new Color32(41, 81, 110, 255));
                    creation.SelectHairColor(new Color32(130, 86, 48, 255));
                    root.Q<SliderInt>("face-25").value = 211;
                    root.Q<SliderInt>("face-58").value = 77;
                    root.Q<SliderInt>("face-18").value = 153;
                    creation.Preview.SetCloseUp(true);
                    creation.Preview.RenderNow();
                    SavePreview(creation.Preview.Texture, "face-preview.png");
                    creation.Preview.SetCloseUp(false);
                    int faceControls = document.rootVisualElement.Query<SliderInt>().ToList()
                        .Count(item => item.ClassListContains("creation-face-slider"));
                    Check(faceControls == 87, "UI exposes all 87 original face controls; actual=" + faceControls);
                    Check(save.CurrentCharacterSlot == CharacterSlot.NoSlot, "Preview does not allocate a save slot");
                    ScreenCapture.CaptureScreenshot(Path.GetFullPath("Docs/CharacterCreationRefresh/creation-ui.png"));
                    Check(true, "READY FOR VISUAL REVIEW; Commit Fresh Character continues the persisted test");
                    SessionState.SetInt(k_Phase, 3);
                    return;
                }
                if (phase == 4 || phase == 7)
                {
                    if (SceneManager.GetActiveScene().path != WorldScenePathLayout.GameplayScenePath ||
                        save == null || save.IsSceneLoadInProgress || PlayerUIManager.Instance?.LocalPlayer == null ||
                        WorldAIManager.Instance?.IsPerformingLoadingOperation != false) { return; }
                    PlayerManager player = PlayerUIManager.Instance.LocalPlayer;
                    if (!player.IsSpawned) { return; }
                    foreach (AICharacterManager enemy in Object.FindObjectsByType<AICharacterManager>(FindObjectsSortMode.None))
                    {
                        enemy.enabled = false;
                        enemy.GetComponent<AICharacterCombatManager>()?.CloseDamageColliders();
                    }
                    if (PlayerUIManager.Instance.PlayerUILoadingScreenManager?.IsLoadingScreenActive == true) { return; }
                    if (s_waitFrame++ < 2) { return; }
                    s_waitFrame = 0;
                    Check(!player.PlayerNetworkManager.IsMale.Value, "Selected female body survives creation/reload");
                    Check(player.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any(renderer =>
                        renderer.name == "FG_A_0100_13" && renderer.enabled && renderer.gameObject.activeInHierarchy),
                        "The open starter helmet keeps the actual FaceGen face visible");
                    CharacterAppearanceData actual = player.PlayerNetworkManager.Appearance.Value.ToData();
                    Check(actual.Eyebrow == 2 && actual.EyeColor.Equals(new Color32(41, 81, 110, 255)) &&
                        Mathf.Abs(actual.IrisSize - 1.05f) < 0.004f, "Eye and eyebrow settings survive " +
                        (phase == 4 ? "creation" : "disk reload"));
                    Check(actual.Hairstyle == 3 && Mathf.Abs(actual.GetShape(25) - 0.65f) < 0.01f &&
                        Mathf.Abs(actual.GetShape(58) + 0.4f) < 0.01f, "Face and hair survive " +
                        (phase == 4 ? "creation" : "disk reload"));
                    Check(player.PlayerNetworkManager.CharacterName.Value.ToString() == "褪色者测试",
                        "Character name survives " + (phase == 4 ? "creation" : "disk reload"));
                    Check(save.CurrentCharacterSlot == CharacterSlot.CharacterSlot01, "Fresh character owns slot 01");
                    if (phase == 7)
                    {
                        Check(true, "PERSISTED CREATE AND RELOAD PASSED");
                        ScreenCapture.CaptureScreenshot(Path.GetFullPath("Docs/CharacterCreationRefresh/reloaded-village.png"));
                        SessionState.SetInt(k_Phase, 0);
                        return;
                    }
                    Check(save.SaveGame(), "New character saves its live gameplay snapshot");
                    File.Copy(Path.Combine(Application.persistentDataPath, "CharacterSlot01.json"),
                        "Docs/CharacterCreationRefresh/new-character-save.json", true);
                    save.ReturnToMainMenu();
                    SessionState.SetInt(k_Phase, 6);
                    return;
                }
                if (phase == 6 && title != null && save?.IsSceneLoadInProgress == false)
                {
                    if (Unity.Netcode.NetworkManager.Singleton == null ||
                        !Unity.Netcode.NetworkManager.Singleton.IsListening)
                    {
                        title.PressStart();
                        return;
                    }
                    title.ContinueGame();
                    SessionState.SetInt(k_Phase, 7);
                    SessionState.SetFloat(k_Start, (float)EditorApplication.timeSinceStartup);
                }
            }
            catch (Exception exception)
            {
                File.AppendAllText(k_Report, "FAIL\n" + exception);
                SessionState.SetInt(k_Phase, 0);
                EditorApplication.isPlaying = false;
            }
        }

        private static void Check(bool condition, string message)
        {
            File.AppendAllText(k_Report, (condition ? "PASS: " : "FAIL: ") + message + "\n");
            if (!condition) { throw new InvalidOperationException(message); }
        }

        private static void SavePreview(RenderTexture source, string filename)
        {
            RenderTexture previous = RenderTexture.active;
            var texture = new Texture2D(source.width, source.height, TextureFormat.RGB24, false);
            try
            {
                RenderTexture.active = source;
                texture.ReadPixels(new Rect(0f, 0f, source.width, source.height), 0, 0);
                texture.Apply();
                File.WriteAllBytes("Docs/CharacterCreationRefresh/" + filename, texture.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; Object.Destroy(texture); }
        }
    }
}
