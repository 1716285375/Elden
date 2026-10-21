using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ZZ.Editor
{
    /// <summary>Validates the Toolkit overlay against the local player's actual network state.</summary>
    public static class CombatHudToolkitSmoke
    {
        [MenuItem("Tools/ZZ/UI Toolkit/Preview Combat Feedback")]
        public static void PreviewFeedback()
        {
            PlayerUIHUDManager manager = PlayerUIManager.Instance?.PlayerUIHUDManager;
            CombatHudView view = manager?.Toolkit;
            if (!Application.isPlaying || view == null)
            {
                throw new InvalidOperationException("Start an unsaved gameplay session first.");
            }
            VisualElement root = view.GetComponent<UIDocument>().rootVisualElement;
            string previousPrompt = view.HasPrompt ? root.Q<Label>("interaction-text").text : null;
            view.SetHealth(28f, 150f);
            view.SetStamina(12f, 100f);
            view.SetPrompt("触碰回响 · 整备与休息");
            root.schedule.Execute(() => ScreenCapture.CaptureScreenshot(
                Path.GetFullPath(".utmp/combat-hud-toolkit-feedback.png"))).StartingIn(600);
            root.schedule.Execute(() =>
            {
                manager.RefreshToolkit();
                view.SetPrompt(previousPrompt);
            }).StartingIn(1500);
        }

        [MenuItem("Tools/ZZ/UI Toolkit/Verify Combat HUD")]
        public static void Verify()
        {
            PlayerUIHUDManager manager = PlayerUIManager.Instance?.PlayerUIHUDManager;
            CombatHudView view = manager?.Toolkit;
            if (!Application.isPlaying || view == null)
            {
                throw new InvalidOperationException("Start an unsaved gameplay session before verifying the combat HUD.");
            }
            VisualElement root = view.GetComponent<UIDocument>().rootVisualElement;
            var report = new StringBuilder();
            root.Query<VisualElement>().ForEach(element =>
            {
                if (element.pickingMode != PickingMode.Ignore)
                {
                    throw new InvalidOperationException("HUD captures pointer input: " + element.name);
                }
            });
            report.AppendLine("PASS: All HUD elements pass pointer input through");
            manager.HideHUD();
            if (root.style.display.value != DisplayStyle.None)
            {
                throw new InvalidOperationException("Modal menu did not hide the Toolkit HUD");
            }
            manager.ShowHUD();
            manager.RefreshToolkit();
            foreach (string channel in new[] { "health", "stamina", "focus" })
            {
                report.AppendLine($"PASS: {channel} displays {root.Q<Label>(channel + "-value").text}");
            }
            report.AppendLine($"Viewport: {Screen.width}x{Screen.height}");
            report.AppendLine($"HUD bounds: {root.Q("combat-hud").worldBound}");
            Directory.CreateDirectory(".utmp");
            File.WriteAllText(".utmp/combat-hud-toolkit-smoke.txt", report.ToString());
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(".utmp/combat-hud-toolkit-gameplay.png"));
            Debug.Log(report.ToString());
        }
    }
}
