using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace ZZ.Editor
{
    /// <summary>Captures live menu states without committing transactions or character saves.</summary>
    [InitializeOnLoad]
    public static class AllMenuPresentationSmoke
    {
        static AllMenuPresentationSmoke()
        {
            EditorApplication.playModeStateChanged += RestoreStartScene;
        }

        [MenuItem("Tools/ZZ/UI Toolkit/Start Review From Title")]
        public static void StartReviewFromTitle()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Stop Play Mode before starting title verification.");
            }
            SessionState.SetString("ZZ.UIReview.PreviousStartScene",
                AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            SessionState.SetBool("ZZ.UIReview.RestoreStartScene", true);
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(
                WorldScenePathLayout.MainMenuScenePath);
            EditorApplication.isPlaying = true;
        }

        private static void RestoreStartScene(PlayModeStateChange state)
        {
            if ((state == PlayModeStateChange.EnteredPlayMode || state == PlayModeStateChange.EnteredEditMode) &&
                SessionState.GetBool("ZZ.UIReview.RestoreStartScene", false))
            {
                EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    SessionState.GetString("ZZ.UIReview.PreviousStartScene", string.Empty));
                SessionState.SetBool("ZZ.UIReview.RestoreStartScene", false);
            }
        }

        private const string k_Folder = "Docs/UIParity/Runtime";

        [MenuItem("Tools/ZZ/UI Toolkit/Configure Loading Artwork")]
        public static void ConfigureLoadingArtwork()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D",
                new[] { "Assets/_Game/Resources/UI/EldenRing/Loading" }))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                importer.textureType = TextureImporterType.Default;
                importer.mipmapEnabled = false;
                importer.isReadable = false;
                importer.maxTextureSize = 4096;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
        }

        [MenuItem("Tools/ZZ/UI Toolkit/Verify All Menu Presentation")]
        public static void Verify()
        {
            if (!Application.isPlaying || PlayerUIManager.Instance?.LocalPlayer == null ||
                SessionState.GetInt("ZZ.CreationRefresh.Phase", 0) != 0 ||
                WorldSaveGameManager.Instance?.IsSceneLoadInProgress == true || VillageSceneWarmup.IsLoading)
            {
                throw new InvalidOperationException("Load the existing gameplay test character first.");
            }
            PlayerUIManager.Instance.StartCoroutine(Observe());
        }

        [MenuItem("Tools/ZZ/UI Toolkit/Capture Frontend Presentation")]
        public static void CaptureFrontendPresentation()
        {
            var title = UnityEngine.Object.FindFirstObjectByType<TitleScreenManager>();
            if (!Application.isPlaying || title == null)
            {
                throw new InvalidOperationException("Enter Play Mode in the title scene first.");
            }
            title.StartCoroutine(ObserveFrontend(title));
        }

        private static IEnumerator ObserveFrontend(TitleScreenManager title)
        {
            Directory.CreateDirectory(k_Folder);
            File.AppendAllText(k_Folder + "/report.txt", "FRONTEND RUNNING\n");
            yield return Capture("press-start");
            title.PressStart();
            yield return Capture("title");
            TitleMenuCarousel carousel = UnityEngine.Object.FindFirstObjectByType<TitleMenuCarousel>();
            var commands = carousel.GetComponentsInChildren<UnityEngine.UI.Button>();
            foreach (var command in commands.Where(command => command.interactable))
            {
                command.Select();
                yield return new WaitForSecondsRealtime(0.2f);
                Check(commands.Count(button => button.GetComponent<CanvasGroup>().alpha > 0f) == 3,
                    "Title shows exactly three commands: " + command.name);
                Check(command.GetComponent<CanvasGroup>().alpha == 1f,
                    "Selected title command is brightest: " + command.name);
            }
            yield return Capture("title-last-command");
            var eventSystem = UnityEngine.EventSystems.EventSystem.current;
            GameObject last = eventSystem.currentSelectedGameObject;
            var move = new UnityEngine.EventSystems.AxisEventData(eventSystem)
            {
                moveDir = UnityEngine.EventSystems.MoveDirection.Down,
                moveVector = Vector2.down
            };
            UnityEngine.EventSystems.ExecuteEvents.Execute(last, move,
                UnityEngine.EventSystems.ExecuteEvents.moveHandler);
            Check(eventSystem.currentSelectedGameObject != last,
                "Keyboard/gamepad navigation moves to another real title command");
            GameObject beforeScroll = eventSystem.currentSelectedGameObject;
            var scroll = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
            {
                scrollDelta = Vector2.up
            };
            carousel.OnScroll(scroll);
            Check(eventSystem.currentSelectedGameObject != beforeScroll, "Mouse wheel changes title selection");
            yield return Capture("title-scroll");
            title.StartNewGame();
            float deadline = Time.realtimeSinceStartup + 60f;
            var creator = UnityEngine.Object.FindFirstObjectByType<TitleScreenCharacterCreationManager>();
            while (creator?.IsReady != true && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                creator = UnityEngine.Object.FindFirstObjectByType<TitleScreenCharacterCreationManager>();
            }
            Check(creator?.IsReady == true, "Creation preview finishes loading without committing a save");
            yield return Capture("creation-origin");
            var originDocument = creator.GetComponentsInChildren<UIDocument>()
                .Single(item => item.gameObject.name == "Character Origin Toolkit");
            var previousInputSettings = UnityEngine.InputSystem.InputSystem.settings;
            var verificationInputSettings = UnityEngine.Object.Instantiate(previousInputSettings);
            verificationInputSettings.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
            verificationInputSettings.editorInputBehaviorInPlayMode =
                UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            UnityEngine.InputSystem.InputSystem.settings = verificationInputSettings;
            var originKeyboard = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>("Origin UI check");
            var originGamepad = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Gamepad>("Origin UI check");
            try
            {
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(originKeyboard,
                    new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.F));
                UnityEngine.InputSystem.InputSystem.Update();
                yield return null;
                yield return null;
                Check(originDocument.rootVisualElement.Query(className: "origin-card__stats").ToList().Count == 3,
                    "F shows source attributes for all three visible origins; keyboard enabled=" + originKeyboard.enabled);
                yield return Capture("creation-origin-stats");
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(originKeyboard,
                    new UnityEngine.InputSystem.LowLevel.KeyboardState());
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(originGamepad,
                    new UnityEngine.InputSystem.LowLevel.GamepadState().WithButton(UnityEngine.InputSystem.LowLevel.GamepadButton.North));
                UnityEngine.InputSystem.InputSystem.Update();
                yield return null;
                yield return null;
                Check(originDocument.rootVisualElement.Query(className: "origin-card__stats").ToList().Count == 0,
                    "Gamepad Y hides origin attributes");
            }
            finally
            {
                UnityEngine.InputSystem.InputSystem.RemoveDevice(originKeyboard);
                UnityEngine.InputSystem.InputSystem.RemoveDevice(originGamepad);
                UnityEngine.InputSystem.InputSystem.settings = previousInputSettings;
                UnityEngine.Object.Destroy(verificationInputSettings);
            }
            creator.ConfirmOrigin(0, true);
            yield return Capture("creation-basics");
            CharacterCreationView view = UnityEngine.Object.FindFirstObjectByType<CharacterCreationView>();
            view.ShowSection("详细外貌");
            yield return Capture("creation-appearance");
            title.ReturnFromCharacterCreation();
            yield return Capture("title-return");
            File.AppendAllText(k_Folder + "/report.txt", "FRONTEND PASSED\n");
        }

        private static IEnumerator Observe()
        {
            Directory.CreateDirectory(k_Folder);
            File.WriteAllText(k_Folder + "/report.txt", "RUNNING\n");
            PlayerUIManager ui = PlayerUIManager.Instance;
            int runes = ui.LocalPlayer.PlayerStatsManager.Runes;
            int owned = ui.LocalPlayer.InventoryManager.ItemsInInventory.Count;
            ui.PlayerUICharacterMenuManager.OpenCharacterMenu();
            yield return Capture("character-menu");
            var characterMenu = UnityEngine.Object.FindFirstObjectByType<CharacterMenuView>();
            Submit(characterMenu.GetComponent<UIDocument>().rootVisualElement.Q<Button>("status"));
            yield return Capture("status");
            characterMenu.CloseStatus();

            ui.PlayerUIEquipmentManager.OpenWeaponMenu();
            yield return Capture("weapons-overview");
            EquipmentMenuView equipment = UnityEngine.Object.FindFirstObjectByType<EquipmentMenuView>();
            Submit(equipment.GetComponent<UIDocument>().rootVisualElement.Q<Button>("equipment-slot-0"));
            yield return Capture("weapons-selection");
            VisualElement equipmentRoot = equipment.GetComponent<UIDocument>().rootVisualElement;
            Submit(equipmentRoot.Q<Button>("switch-display"));
            Check(equipmentRoot.Q(className: "menu-overlay").ClassListContains("menu--description"),
                "Weapon description command changes the middle display");
            yield return Capture("weapons-description");
            Submit(equipmentRoot.Q<Button>("switch-display"));
            ui.PlayerUIEquipmentManager.OpenEquipmentManagerMenu();
            yield return Capture("armor-overview");
            equipment = UnityEngine.Object.FindFirstObjectByType<EquipmentMenuView>();
            Submit(equipment.GetComponent<UIDocument>().rootVisualElement.Q<Button>("equipment-slot-7"));
            yield return Capture("armor-selection");

            ui.PlayerUIInventoryManager.OpenInventory();
            yield return Capture("inventory");
            var inventory = UnityEngine.Object.FindFirstObjectByType<InventoryMenuView>();
            VisualElement inventoryRoot = inventory.GetComponent<UIDocument>().rootVisualElement;
            Submit(inventoryRoot.Q<Button>("switch-view"));
            Check(inventoryRoot.Q(className: "menu-overlay").ClassListContains("menu--simple"),
                "Item right-display command hides only the status column");
            yield return Capture("inventory-simple");
            Submit(inventoryRoot.Q<Button>("switch-view"));

            ui.PlayerUISiteOfGraceManager.OpenSiteOfGraceMenu();
            yield return Capture("grace");
            var grace = UnityEngine.Object.FindFirstObjectByType<SiteOfGraceView>();
            Submit(grace.GetComponent<UIDocument>().rootVisualElement.Q<Button>("memorize"));
            Check(ui.PlayerUISiteOfGraceManager.IsMemoryOpen, "Grace opens a separate memorize-spells page");
            yield return Capture("memorize");
            ui.PlayerUISiteOfGraceManager.CloseMemory();
            Check(ui.PlayerUISiteOfGraceManager.IsMenuOpen && !ui.PlayerUISiteOfGraceManager.IsMemoryOpen,
                "Leaving memory keeps grace menu input ownership");
            ui.PlayerUILevelUpManager.OpenMenu();
            yield return Capture("level-up");
            ui.PlayerUIStorageManager.OpenStorageMenu();
            yield return Capture("storage");
            ui.PlayerUITeleportLocationManager.OpenTeleportLocationMenu();
            yield return Capture("travel");
            ui.PlayerUIWeaponUpgradeManager.OpenWeaponUpgradeMenu();
            yield return Capture("weapon-upgrade");
            ui.PlayerUISaveGameManager.OpenSaveGameMenu();
            yield return Capture("save");

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Prefabs/Characters/AI/Blacksmith NPC.prefab");
            GameObject merchant = UnityEngine.Object.Instantiate(prefab,
                ui.LocalPlayer.transform.position + ui.LocalPlayer.transform.forward,
                ui.LocalPlayer.transform.rotation);
            try
            {
                AICharacterInventoryManager stock = merchant.GetComponent<AICharacterInventoryManager>();
                Check(stock.EnsureShopInventory(), "Merchant runtime stock initializes");
                ui.PlayerUIShopManager.OpenInteractionMenu(
                    merchant.GetComponentInChildren<AICharacterSoundFXManager>());
                yield return Capture("merchant-context");
                ui.PlayerUIShopManager.OpenBuyMenu();
                yield return Capture("shop");
                Item item = stock.ItemsInInventory.FirstOrDefault();
                if (item != null)
                {
                    // Only the temporary merchant's cloned stock is changed; no trade is committed.
                    var serialized = new SerializedObject(item);
                    serialized.FindProperty("m_itemValue").intValue = 0;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    ui.PlayerUIShopManager.SelectItem(item);
                    var shop = UnityEngine.Object.FindFirstObjectByType<ShopMenuView>();
                    Submit(shop.GetComponent<UIDocument>().rootVisualElement.Q<Button>("trade"));
                    Check(shop.IsConfirmationOpen, "Trade opens a separate confirmation layer");
                    yield return Capture("shop-confirmation");
                    ui.PlayerUIShopManager.NavigateBack();
                    Check(!shop.IsConfirmationOpen && ui.PlayerUIShopManager.IsMenuOpen,
                        "Cancel closes only transaction confirmation");
                }
            }
            finally
            {
                ui.PlayerUIShopManager.CloseMenu();
                UnityEngine.Object.Destroy(merchant);
            }
            Check(ui.LocalPlayer.PlayerStatsManager.Runes == runes, "Menu browsing does not spend runes");
            Check(ui.LocalPlayer.InventoryManager.ItemsInInventory.Count == owned,
                "Menu browsing does not add or remove owned items");
            ui.PlayerUICharacterMenuManager.OpenCharacterMenu();
            ui.PlayerUICharacterMenuManager.OpenSettings();
            yield return null;
            var settings = UnityEngine.Object.FindFirstObjectByType<GameSettingsPanel>();
            foreach (string category in new[] { "audio", "controls", "display", "impact" })
            {
                Submit(settings.GetComponent<UIDocument>().rootVisualElement.Q<Button>("tab-" + category));
                yield return Capture("settings-" + category);
            }
            settings.Close();
            ui.PlayerUILoadingScreenManager.ActivateLoadingScreen();
            yield return Capture("loading");
            ui.PlayerUILoadingScreenManager.DeactivateLoadingScreen(0f, 0f);
            yield return null;
            File.AppendAllText(k_Folder + "/report.txt", "ALL PASSED\n");
        }

        private static IEnumerator Capture(string name)
        {
            yield return null;
            yield return null;
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(k_Folder + "/" + name + ".png"));
            yield return new WaitForEndOfFrame();
            File.AppendAllText(k_Folder + "/report.txt", "CAPTURED: " + name + "\n");
        }

        private static void Submit(Button button)
        {
            if (button == null)
            {
                throw new InvalidOperationException("The expected native command is missing.");
            }
            button.Focus();
            using NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled();
            submit.target = button;
            button.SendEvent(submit);
        }

        private static void Check(bool condition, string message)
        {
            File.AppendAllText(k_Folder + "/report.txt", (condition ? "PASS: " : "FAIL: ") + message + "\n");
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
