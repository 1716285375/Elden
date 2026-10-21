using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace ZZ.Editor
{
    /// <summary>Checks save rejection and transition input ownership without writing character saves.</summary>
    public static class SaveLoadingToolkitSmoke
    {
        private const string k_Report = ".utmp/save-loading-toolkit-smoke.txt";
        private const BindingFlags k_Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [MenuItem("Tools/ZZ/UI Toolkit/Verify Save Loading")]
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
            File.WriteAllText(k_Report, "RUNNING\n");
            PlayerUIManager ui = PlayerUIManager.Instance;
            PlayerUISaveGameManager save = ui.PlayerUISaveGameManager;
            PlayerUILoadingScreenManager loading = ui.PlayerUILoadingScreenManager;
            float previousTimeScale = Time.timeScale;
            try
            {
                save.OpenSaveGameMenu();
                yield return null;
                yield return null;
                VisualElement root = UnityEngine.Object.FindFirstObjectByType<SaveGameView>()
                    .GetComponent<UIDocument>().rootVisualElement;
                Check(save.IsSaveGameMenuOpen && ui.IsMenuWindowOpen, "Native save menu owns input");
                Check(!root.Q<Button>("save").enabledSelf && !save.CanSaveCurrentGame,
                    "Unsaved session disables writing and displays its slot state");
                Check(root.panel.focusController.focusedElement == root.Q<Button>("back"),
                    "Unavailable save focuses the return command");
                save.SaveCurrentGame();
                Check(root.Q<Label>("save-feedback").text.Contains("保存未完成"),
                    "Direct unavailable request reports failure without calling the disk writer");
                ScreenCapture.CaptureScreenshot(Path.GetFullPath(".utmp/save-toolkit.png"));
                yield return new WaitForEndOfFrame();
                Back(ui);
                Check(!save.IsSaveGameMenuOpen && ui.PlayerUICharacterMenuManager.IsMenuOpen,
                    "Save back returns to character commands");
                save.OpenSaveGameMenu();
                yield return null;
                root = UnityEngine.Object.FindFirstObjectByType<SaveGameView>().GetComponent<UIDocument>().rootVisualElement;
                Check(root.Q<Label>("save-feedback").text.Contains("暂时无法保存"), "Reopening resets previous feedback");
                save.SetDeathInputBlocked(true);
                save.OpenSaveGameMenu();
                Check(!save.IsSaveGameMenuOpen, "Death closes the save menu and prevents reopening");
                save.SetDeathInputBlocked(false);
                save.OpenSaveGameMenu();
                yield return null;
                yield return null;
                loading.ActivateLoadingScreen();
                yield return null;
                LoadingScreenView view = UnityEngine.Object.FindFirstObjectByType<LoadingScreenView>();
                VisualElement cover = view.GetComponent<UIDocument>().rootVisualElement.Q("loading-overlay");
                Check(loading.IsLoadingScreenActive && ui.IsMenuWindowOpen && !ui.CanOpenMenuWindows &&
                    view.GetComponent<UIDocument>().panelSettings.sortingOrder > 900, "Native loading covers menus and retains input");
                Back(ui);
                Check(save.IsSaveGameMenuOpen && loading.IsLoadingScreenActive, "Back cannot dismiss loading or its underlying menu");
                Time.timeScale = 0f;
                VisualElement pulse = cover.Q("loading-pulse");
                float initialMargin = pulse.style.marginLeft.value.value;
                yield return new WaitForSecondsRealtime(0.15f);
                Check(Mathf.Abs(pulse.style.marginLeft.value.value - initialMargin) > 0.1f,
                    "Activity indicator animates while scaled time is paused");
                ScreenCapture.CaptureScreenshot(Path.GetFullPath(".utmp/loading-toolkit.png"));
                yield return new WaitForEndOfFrame();
                loading.DeactivateLoadingScreen(0f, 0.5f);
                object fade = typeof(PlayerUILoadingScreenManager).GetField("m_fadeLoadingScreenCoroutine", k_Private)
                    .GetValue(loading);
                loading.DeactivateLoadingScreen(0f, 0f);
                Check(ReferenceEquals(fade, typeof(PlayerUILoadingScreenManager)
                    .GetField("m_fadeLoadingScreenCoroutine", k_Private).GetValue(loading)), "Repeated deactivation keeps one fade");
                yield return new WaitForSecondsRealtime(0.1f);
                loading.ActivateLoadingScreen();
                Check(!loading.IsFadingLoadingScreen && cover.style.opacity.value == 1f,
                    "New transition cancels the old fade and restores full opacity");
                loading.DeactivateLoadingScreen(0f, 0.1f);
                yield return new WaitForSecondsRealtime(0.3f);
                root = UnityEngine.Object.FindFirstObjectByType<SaveGameView>().GetComponent<UIDocument>().rootVisualElement;
                Check(!loading.IsLoadingScreenActive && save.IsSaveGameMenuOpen && ui.IsMenuWindowOpen &&
                    root.panel.focusController.focusedElement == root.Q<Button>("back"),
                    "Unscaled fade restores underlying menu focus without releasing its input");
                loading.ActivateLoadingScreen();
                ui.CloseAllMenuWindows();
                Check(ui.IsMenuWindowOpen && !save.IsSaveGameMenuOpen && !PlayerInputManager.Instance.IsMovementInputEnabled,
                    "Closing all menus cannot release gameplay during loading");
                loading.DeactivateLoadingScreen(0f, 0f);
                yield return null;
                yield return null;
                Check(!loading.IsLoadingScreenActive && !ui.IsMenuWindowOpen,
                    "Loading completion releases input when no menu remains");
                File.AppendAllText(k_Report, "ALL PASSED\n");
            }
            finally
            {
                Time.timeScale = previousTimeScale;
                save.SetDeathInputBlocked(false);
                loading.DeactivateLoadingScreen(0f, 0f);
                ui.CloseAllMenuWindows();
            }
        }

        private static void Back(PlayerUIManager ui)
        {
            typeof(PlayerUICharacterMenuManager).GetMethod("OnCloseMenuPerformed", k_Private)
                .Invoke(ui.PlayerUICharacterMenuManager, new object[] { default(InputAction.CallbackContext) });
        }

        private static void Check(bool passed, string message)
        {
            File.AppendAllText(k_Report, $"{(passed ? "PASS" : "FAIL")}: {message}\n");
            if (!passed)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
