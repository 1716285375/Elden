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
    /// <summary>Checks native attribute planning without writing a character save.</summary>
    public static class LevelUpToolkitSmoke
    {
        private const string k_Report = ".utmp/level-up-toolkit-smoke.txt";

        [MenuItem("Tools/ZZ/UI Toolkit/Verify Level Up")]
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
            PlayerManager player = ui.LocalPlayer;
            PlayerUILevelUpManager manager = ui.PlayerUILevelUpManager;
            player.PlayerStatsManager.SetRunes(200000);
            manager.OpenMenu();
            yield return null;
            yield return null;
            LevelUpView view = UnityEngine.Object.FindFirstObjectByType<LevelUpView>();
            Check(view != null && manager.IsMenuOpen, "Native level menu opens");
            VisualElement root = view.GetComponent<UIDocument>().rootVisualElement;
            Check(root.Query<SliderInt>().ToList().Count == 7, "All seven attributes use integer sliders");
            Check(!root.Q<Button>("review").enabledSelf, "Unchanged plan cannot be committed");
            int vigor = player.PlayerNetworkManager.Vitality.Value;
            int mind = player.PlayerNetworkManager.Mind.Value;
            Submit(root.Q("attribute-0").Q<Button>("increase"));
            Submit(root.Q("attribute-1").Q<Button>("increase"));
            int cost = manager.TotalLevelUpCost;
            int balance = player.PlayerStatsManager.Runes;
            Check(cost > 0 && player.PlayerNetworkManager.Vitality.Value == vigor &&
                player.PlayerStatsManager.Runes == balance, "Preview changes neither attributes nor runes");
            Submit(root.Q<Button>("review"));
            yield return null;
            Check(manager.IsConfirmationOpen && !root.Q("level-content").enabledSelf &&
                root.panel.focusController.focusedElement == root.Q<Button>("cancel"),
                "Confirmation locks the plan and defaults to Cancel");
            Back(ui);
            Check(manager.IsMenuOpen && !manager.IsConfirmationOpen && manager.TotalLevelUpCost == cost &&
                player.PlayerStatsManager.Runes == balance, "Back cancels confirmation without consuming or clearing plan");
            Submit(root.Q<Button>("review"));
            Submit(root.Q<Button>("confirm"));
            yield return null;
            Check(player.PlayerNetworkManager.Vitality.Value == vigor + 1 &&
                player.PlayerNetworkManager.Mind.Value == mind + 1 &&
                player.PlayerStatsManager.Runes == balance - cost, "Commit applies both attributes and pays the exact total");
            balance = player.PlayerStatsManager.Runes;
            manager.ConfirmLevels();
            Check(player.PlayerStatsManager.Runes == balance && manager.TotalLevelUpCost == 0,
                "Duplicate commit is rejected and the plan resets");
            Submit(root.Q("attribute-0").Q<Button>("increase"));
            Submit(root.Q<Button>("review"));
            player.PlayerNetworkManager.Vitality.Value++;
            Submit(root.Q<Button>("confirm"));
            Check(player.PlayerStatsManager.Runes == balance && !manager.IsConfirmationOpen &&
                manager.TotalLevelUpCost == 0, "Changed live attributes invalidate confirmation without charging");
            player.PlayerStatsManager.SetRunes(0);
            Submit(root.Q("attribute-0").Q<Button>("increase"));
            manager.RequestConfirmation();
            Check(!manager.IsConfirmationOpen && !root.Q<Button>("review").enabledSelf,
                "Insufficient runes block confirmation");
            player.PlayerStatsManager.SetRunes(200000);
            manager.InitializeLevelUpMenu();
            Submit(root.Q("attribute-2").Q<Button>("increase"));
            Submit(root.Q("attribute-3").Q<Button>("increase"));
            yield return new WaitForSecondsRealtime(0.25f);
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(".utmp/level-up-toolkit.png"));
            yield return new WaitForEndOfFrame();
            for (int index = 0; index < 7; index++)
            {
                manager.SetProjectedAttribute((CharacterAttribute)index, 99);
            }
            Check(manager.TotalLevelUpCost == int.MaxValue && !root.Q<Button>("review").enabledSelf,
                "Level cap blocks an excessive plan");
            Submit(root.Q<Button>("reset"));
            Check(manager.TotalLevelUpCost == 0, "Reset discards the entire preview");
            Back(ui);
            Check(!manager.IsMenuOpen && ui.PlayerUISiteOfGraceManager.IsMenuOpen, "Back returns to grace");
            manager.OpenMenu();
            yield return null;
            Check(manager.IsMenuOpen && manager.TotalLevelUpCost == 0 && !manager.IsConfirmationOpen,
                "Reopening rebuilds controls from current attributes");
            ui.CloseAllMenuWindows();
            Check(!ui.IsMenuWindowOpen, "Closing releases gameplay input");
            File.AppendAllText(k_Report, "ALL PASSED\n");
        }

        private static void Back(PlayerUIManager ui)
        {
            typeof(PlayerUICharacterMenuManager).GetMethod("OnCloseMenuPerformed",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ui.PlayerUICharacterMenuManager,
                    new object[] { default(InputAction.CallbackContext) });
        }

        private static void Submit(Button button)
        {
            button.Focus();
            using NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled();
            submit.target = button;
            button.SendEvent(submit);
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
