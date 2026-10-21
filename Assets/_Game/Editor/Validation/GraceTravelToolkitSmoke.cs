using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace ZZ.Editor
{
    /// <summary>Exercises native grace navigation and actual local travel in a disposable session.</summary>
    public static class GraceTravelToolkitSmoke
    {
        private const string k_Report = ".utmp/grace-travel-toolkit-smoke.txt";
        private const BindingFlags k_Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [MenuItem("Tools/ZZ/UI Toolkit/Verify Grace Travel")]
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
            PlayerUISiteOfGraceManager grace = ui.PlayerUISiteOfGraceManager;
            PlayerUITeleportLocationManager travel = ui.PlayerUITeleportLocationManager;
            PlayerManager player = ui.LocalPlayer;
            Vector3 origin = player.transform.position;
            GameObject fixture = new GameObject("Temporary Travel Destination");
            fixture.AddComponent<NetworkObject>();
            SiteOfGraceInteractable destination = fixture.AddComponent<SiteOfGraceInteractable>();
            typeof(SiteOfGraceInteractable).GetField("m_siteOfGraceID", k_Private).SetValue(destination, 900001);
            typeof(SiteOfGraceInteractable).GetField("m_displayName", k_Private).SetValue(destination, "旅途验证 · 临时赐福");
            GameObject arrival = new GameObject("Arrival");
            arrival.transform.SetParent(fixture.transform);
            arrival.transform.position = origin + Vector3.right * 2f;
            typeof(SiteOfGraceInteractable).GetField("m_teleportTransform", k_Private)
                .SetValue(destination, arrival.transform);
            NetworkVariable<bool> activated = (NetworkVariable<bool>)typeof(SiteOfGraceInteractable)
                .GetField("m_isActivated", k_Private).GetValue(destination);
            fixture.GetComponent<NetworkObject>().Spawn();
            try
            {
                grace.OpenSiteOfGraceMenu();
                yield return null;
                VisualElement root = UnityEngine.Object.FindFirstObjectByType<SiteOfGraceView>()
                    .GetComponent<UIDocument>().rootVisualElement;
                Check(grace.IsMenuOpen && root.Q<Button>("travel").enabledSelf, "Grace opens with available single-player travel");
                ScreenCapture.CaptureScreenshot(Path.GetFullPath(".utmp/grace-toolkit.png"));
                yield return new WaitForEndOfFrame();
                Submit(root.Q<Button>("level-up"));
                Check(ui.PlayerUILevelUpManager.IsMenuOpen && !grace.IsMenuOpen, "Grace enters native level-up");
                Back(ui);
                yield return null;
                root = UnityEngine.Object.FindFirstObjectByType<SiteOfGraceView>().GetComponent<UIDocument>().rootVisualElement;
                Submit(root.Q<Button>("storage"));
                Check(ui.PlayerUIStorageManager.IsMenuOpen && !grace.IsMenuOpen, "Grace enters native storage");
                Back(ui);
                yield return null;
                root = UnityEngine.Object.FindFirstObjectByType<SiteOfGraceView>().GetComponent<UIDocument>().rootVisualElement;
                Submit(root.Q<Button>("travel"));
                yield return null;
                root = UnityEngine.Object.FindFirstObjectByType<TravelMenuView>().GetComponent<UIDocument>().rootVisualElement;
                ListView list = root.Q<ListView>("destinations");
                Check(travel.IsMenuOpen && !list.itemsSource.Contains(destination), "Locked registered destination stays hidden");
                activated.Value = true;
                Submit(root.Q<Button>("refresh"));
                Check(list.itemsSource.Contains(destination), "New unlocked ID appears without prefab button authoring");
                list.SetSelection(list.itemsSource.IndexOf(destination));
                Vector3 before = player.transform.position;
                Check(!travel.IsConfirmationOpen, "Selecting a destination never starts travel");
                Submit(root.Q<Button>("travel"));
                yield return null;
                Check(travel.IsConfirmationOpen && !root.Q("travel-content").enabledSelf &&
                    root.panel.focusController.focusedElement == root.Q<Button>("cancel"),
                    "Travel confirmation locks list and defaults to Cancel");
                Back(ui);
                Check(travel.IsMenuOpen && !travel.IsConfirmationOpen &&
                    Vector3.Distance(before, player.transform.position) < 0.1f, "Back cancels only confirmation without teleporting");
                Submit(root.Q<Button>("travel"));
                activated.Value = false;
                Submit(root.Q<Button>("confirm"));
                Check(travel.IsMenuOpen && !travel.IsConfirmationOpen && !list.itemsSource.Contains(destination),
                    "Revoked destination is rejected and removed on commit");
                activated.Value = true;
                travel.RefreshUnlockedLocations();
                list.SetSelection(list.itemsSource.IndexOf(destination));
                Submit(root.Q<Button>("travel"));
                WorldObjectManager.Instance.UnregisterSiteOfGrace(destination);
                Submit(root.Q<Button>("confirm"));
                Check(travel.IsMenuOpen && !travel.IsConfirmationOpen &&
                    Vector3.Distance(before, player.transform.position) < 0.1f, "Unregistered destination cannot move player");
                WorldObjectManager.Instance.RegisterSiteOfGrace(destination);
                travel.RefreshUnlockedLocations();
                list.SetSelection(list.itemsSource.IndexOf(destination));
                yield return new WaitForSecondsRealtime(0.2f);
                ScreenCapture.CaptureScreenshot(Path.GetFullPath(".utmp/travel-toolkit.png"));
                yield return new WaitForEndOfFrame();
                Submit(root.Q<Button>("travel"));
                Submit(root.Q<Button>("confirm"));
                Check(Vector3.Distance(player.transform.position, arrival.transform.position) < 0.05f &&
                    !travel.IsMenuOpen && !travel.IsConfirmationOpen,
                    "Confirmed travel reaches destination and closes travel menu");
                yield return new WaitForSecondsRealtime(1.5f);
                Check(!ui.IsMenuWindowOpen, "Travel fade completes before releasing gameplay input");
                before = player.transform.position;
                arrival.transform.position += Vector3.right * 3f;
                travel.ConfirmTravel();
                Check(Vector3.Distance(before, player.transform.position) < 0.05f, "Repeated confirmation cannot teleport again");
                travel.OpenTeleportLocationMenu();
                yield return null;
                Check(travel.IsMenuOpen && !travel.IsConfirmationOpen, "Reopening has no pending destination");
                Back(ui);
                Check(grace.IsMenuOpen && !travel.IsMenuOpen, "Back from list returns to grace");
                ui.CloseAllMenuWindows();
                File.AppendAllText(k_Report, "ALL PASSED\n");
            }
            finally
            {
                ui.CloseAllMenuWindows();
                fixture.GetComponent<NetworkObject>().Despawn();
            }
        }

        private static void Back(PlayerUIManager ui)
        {
            typeof(PlayerUICharacterMenuManager).GetMethod("OnCloseMenuPerformed", k_Private)
                .Invoke(ui.PlayerUICharacterMenuManager, new object[] { default(InputAction.CallbackContext) });
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
