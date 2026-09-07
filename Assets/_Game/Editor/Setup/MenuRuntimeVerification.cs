using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace ZZ.Editor
{
    /// <summary>Runs UI checks without loading or writing a real character slot.</summary>
    public static class MenuRuntimeVerification
    {
        [MenuItem("Tools/ZZ/Preview Archive")]
        public static void PreviewArchive()
        {
            TitleScreenManager title = Object.FindFirstObjectByType<TitleScreenManager>();
            title.PressStart();
            title.OpenLoadGameMenu();
            title.StartCoroutine(Capture("archive-keyboard"));
        }

        [MenuItem("Tools/ZZ/Verify Upgrade UI")]
        public static void VerifyUpgrade()
        {
            if (WorldSaveGameManager.Instance.CurrentCharacterSlot != CharacterSlot.NoSlot)
            {
                throw new System.InvalidOperationException("Upgrade verification requires an unsaved smoke session.");
            }
            PlayerUIManager.Instance.StartCoroutine(VerifyUpgradeRoutine());
        }

        [MenuItem("Tools/ZZ/Verify Return Menu Button")]
        public static void VerifyReturn()
        {
            PlayerUIManager ui = PlayerUIManager.Instance;
            ui.PlayerUICharacterMenuManager.OpenCharacterMenu();
            FindCharacterButton("Return To Main Menu Button").onClick.Invoke();
        }

        [MenuItem("Tools/ZZ/Verify Quit Button")]
        public static void VerifyQuit()
        {
            FindCharacterButton("Quit Game Button").onClick.Invoke();
        }

        private static IEnumerator VerifyUpgradeRoutine()
        {
            PlayerUIManager ui = PlayerUIManager.Instance;
            var lines = new List<string>();
            ui.PlayerUICharacterMenuManager.OpenCharacterMenu();
            yield return Capture("character-keyboard");
            Gamepad pad = InputSystem.AddDevice<Gamepad>();
            try
            {
                InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = new Vector2(0.7f, 0) });
                yield return null;
                yield return Capture("character-gamepad");
                InputSystem.QueueStateEvent(pad, new GamepadState());
                Button openButton = FindCharacterButton("Upgrade Weapon Button");
                lines.Add("Upgrade command raycast=" + IsRaycastReachable(openButton));
                openButton.onClick.Invoke();
                yield return null;
                PlayerUIWeaponUpgradeManager upgrade = ui.GetComponent<PlayerUIWeaponUpgradeManager>();
                lines.Add($"Upgrade open={upgrade.IsMenuOpen} weapon={upgrade.CurrentSelectedWeapon?.ItemName}");
                yield return Capture("upgrade");
                var data = new SerializedObject(upgrade);
                Button strengthen = (Button)data.FindProperty("m_upgradeButton").objectReferenceValue;
                lines.Add("Strengthen raycast=" + IsRaycastReachable(strengthen));
                UpgradeMaterial cost = upgrade.CurrentUpgradeCost;
                if (cost != null)
                {
                    UpgradeMaterial materials = Object.Instantiate(cost);
                    ui.LocalPlayer.InventoryManager.AddItemToInventory(materials);
                    int before = (int)upgrade.CurrentSelectedWeapon.UpgradeLevel;
                    strengthen.onClick.Invoke();
                    yield return null;
                    lines.Add("Confirmation opened=" + upgrade.IsConfirmationOpen);
                    yield return Capture("upgrade-confirmation");
                    Button confirm = (Button)data.FindProperty("m_confirmButton").objectReferenceValue;
                    lines.Add("Confirm raycast=" + IsRaycastReachable(confirm));
                    confirm.onClick.Invoke();
                    lines.Add($"Upgrade level {before} -> {(int)upgrade.CurrentSelectedWeapon.UpgradeLevel}");
                    lines.Add("Confirmation closed=" + !upgrade.IsConfirmationOpen);
                }
                foreach (InputHintText hint in ui.GetComponentsInChildren<InputHintText>(true))
                {
                    lines.Add($"HINT {hint.Template} -> {hint.GetComponent<TMP_Text>().text}");
                }
            }
            finally
            {
                InputSystem.RemoveDevice(pad);
                File.WriteAllLines(".utmp/menu-runtime-verification.txt", lines);
            }
        }

        private static Button FindCharacterButton(string name)
        {
            return PlayerUIManager.Instance.transform.Find(
                "Player UI/Character Menu/Menu Panel/Command Column/" + name).GetComponent<Button>();
        }

        private static bool IsRaycastReachable(Button button)
        {
            var results = new List<RaycastResult>();
            var rect = (RectTransform)button.transform;
            Canvas canvas = button.GetComponentInParent<Canvas>();
            Vector2 point = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera,
                rect.TransformPoint(rect.rect.center));
            var pointer = new PointerEventData(EventSystem.current) { position = point };
            EventSystem.current.RaycastAll(pointer, results);
            File.AppendAllText(".utmp/menu-raycasts.txt", $"{button.name} point={point} rect={rect.rect} " +
                $"hits={string.Join(",", results.Select(hit => AnimationUtility.CalculateTransformPath(hit.gameObject.transform, null)))}\n");
            return results.Count > 0 && results[0].gameObject.GetComponentInParent<Button>() == button;
        }

        private static IEnumerator Capture(string name)
        {
            yield return new WaitForSecondsRealtime(0.3f);
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(".utmp/" + name + ".png"));
            yield return new WaitForEndOfFrame();
        }
    }
}
