using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ZZ
{
    /// <summary>Coordinates title-screen character creation and its runtime player preview.</summary>
    public class TitleScreenCharacterCreationManager : MonoBehaviour
    {
        public const int MaximumCharacterNameLength = 15;

        [Header("OWNERS")]
        [SerializeField] private TitleScreenManager m_titleScreenManager;
        [SerializeField] private GameObject m_characterCreationRoot;
        [SerializeField] private GameObject m_creationOptions;

        [Header("SUB MENUS")]
        [SerializeField] private GameObject m_classMenu;
        [SerializeField] private GameObject m_hairMenu;
        [SerializeField] private GameObject m_hairColorMenu;
        [SerializeField] private GameObject m_nameMenu;

        [Header("SELECTION")]
        [SerializeField] private Button m_firstCreationButton;
        [SerializeField] private Button m_firstClassButton;
        [SerializeField] private Button m_firstHairButton;
        [SerializeField] private Button m_firstHairColorButton;
        [SerializeField] private Selectable m_nameInputButton;

        [Header("VALUES")]
        [SerializeField] private TMP_InputField m_characterNameInput;
        [SerializeField] private TMP_Text m_characterNameValue;
        [SerializeField] private TMP_Text m_classValue;
        [SerializeField] private TMP_Text m_hairValue;
        [SerializeField] private TMP_Text m_sexValue;
        [SerializeField] private Image m_hairColorValue;
        [SerializeField] private Slider m_hairColorRedSlider;
        [SerializeField] private Slider m_hairColorGreenSlider;
        [SerializeField] private Slider m_hairColorBlueSlider;
        [SerializeField] private TitleScreenPlayerPreviewRotator m_previewRotator;

        [Header("PREVIEW")]
        [SerializeField] private RawImage m_previewImage;

        private PlayerManager m_previewPlayer;
        private Coroutine m_resolvePlayerCoroutine;
        private int m_selectedClassIndex;
        private int m_selectedHairstyleID;
        private Color32 m_selectedHairColor = new(79, 53, 35, 255);
        private bool m_isInitializingColorSliders;
        private bool m_areEventsBound;
        private Camera m_previewCamera;
        private Light m_previewLight;
        private RenderTexture m_previewTexture;
        private readonly Dictionary<GameObject, int> m_previewOriginalLayers = new();

        /// <summary>Gets whether the character-creation root currently owns title UI.</summary>
        public bool IsOpen => m_characterCreationRoot != null &&
            m_characterCreationRoot.activeSelf;

        private void Awake()
        {
            m_titleScreenManager ??= FindFirstObjectByType<TitleScreenManager>();
            BindUIEvents();
        }

        private void BindUIEvents()
        {
            if (m_areEventsBound)
            {
                return;
            }

            if (m_characterNameInput == null &&
                m_hairColorRedSlider == null &&
                m_hairColorGreenSlider == null &&
                m_hairColorBlueSlider == null)
            {
                return;
            }

            if (m_characterNameInput != null)
            {
                m_characterNameInput.characterLimit = MaximumCharacterNameLength;
                m_characterNameInput.onEndEdit.AddListener(SubmitCharacterName);
            }

            ConfigureColorSlider(m_hairColorRedSlider);
            ConfigureColorSlider(m_hairColorGreenSlider);
            ConfigureColorSlider(m_hairColorBlueSlider);
            m_hairColorRedSlider?.onValueChanged.AddListener(OnHairColorSliderChanged);
            m_hairColorGreenSlider?.onValueChanged.AddListener(OnHairColorSliderChanged);
            m_hairColorBlueSlider?.onValueChanged.AddListener(OnHairColorSliderChanged);
            m_areEventsBound = true;
        }

        private void OnDestroy()
        {
            if (m_characterNameInput != null)
            {
                m_characterNameInput.onEndEdit.RemoveListener(SubmitCharacterName);
            }

            m_hairColorRedSlider?.onValueChanged.RemoveListener(OnHairColorSliderChanged);
            m_hairColorGreenSlider?.onValueChanged.RemoveListener(OnHairColorSliderChanged);
            m_hairColorBlueSlider?.onValueChanged.RemoveListener(OnHairColorSliderChanged);
            if (m_previewTexture != null)
            {
                m_previewTexture.Release();
                Destroy(m_previewTexture);
            }

            if (m_previewCamera != null)
            {
                Destroy(m_previewCamera.gameObject);
            }
        }

        /// <summary>Builds the default three-column creation UI when no authored UI is assigned.</summary>
        public void ConfigureRuntime(
            TitleScreenManager titleScreenManager,
            Button styleSource)
        {
            m_titleScreenManager = titleScreenManager;
            if (m_characterCreationRoot == null)
            {
                BuildRuntimeInterface(styleSource);
            }

            EnsurePreviewInfrastructure();
            BindUIEvents();
        }

        /// <summary>Opens creation, waits for the host player, and establishes UI focus.</summary>
        public void OpenCharacterCreation()
        {
            m_characterCreationRoot?.SetActive(true);
            ShowCreationOptions(m_firstCreationButton);
            PlayerInputManager.Instance?.EnableMenuCameraInput();
            if (m_resolvePlayerCoroutine != null)
            {
                StopCoroutine(m_resolvePlayerCoroutine);
            }

            m_resolvePlayerCoroutine = StartCoroutine(ResolvePreviewPlayer());
        }

        /// <summary>Closes creation and restores any temporarily hidden head equipment.</summary>
        public void CloseCharacterCreation()
        {
            if (m_resolvePlayerCoroutine != null)
            {
                StopCoroutine(m_resolvePlayerCoroutine);
                m_resolvePlayerCoroutine = null;
            }

            SetHeadEquipmentEditing(false);
            RestorePreviewLayers();
            m_previewRotator?.BindTarget(null);
            m_previewPlayer = null;
            CloseAllSubMenus();
            m_characterCreationRoot?.SetActive(false);
            PlayerInputManager.Instance?.DisableMenuCameraInput();
        }

        /// <summary>Returns to the title menu without reserving a save slot.</summary>
        public void CancelCharacterCreation()
        {
            m_titleScreenManager?.ReturnFromCharacterCreation();
        }

        /// <summary>Opens class selection and previews its first option.</summary>
        public void OpenClassMenu()
        {
            ShowSubMenu(m_classMenu, m_firstClassButton, false);
            PreviewClass(m_selectedClassIndex);
        }

        /// <summary>Opens hairstyle selection and reveals the character's head.</summary>
        public void OpenHairMenu()
        {
            ShowSubMenu(m_hairMenu, m_firstHairButton, true);
            PreviewHairstyle(m_selectedHairstyleID);
        }

        /// <summary>Opens RGB hair editing and reveals the character's head.</summary>
        public void OpenHairColorMenu()
        {
            ShowSubMenu(m_hairColorMenu, m_firstHairColorButton, true);
            SetColorSliders(m_selectedHairColor);
            PreviewHairColor(m_selectedHairColor);
        }

        /// <summary>Opens keyboard text entry with a strict 15-character limit.</summary>
        public void OpenNameMenu()
        {
            ShowSubMenu(m_nameMenu, m_nameInputButton, false);
            if (m_characterNameInput == null)
            {
                return;
            }

            m_characterNameInput.text = ResolveCharacterName();
            m_characterNameInput.Select();
            m_characterNameInput.ActivateInputField();
        }

        /// <summary>Returns from the active sub-menu and restores committed preview state.</summary>
        public void CloseSubMenu()
        {
            SetHeadEquipmentEditing(false);
            ApplySelectedClass();
            ApplySelectedAppearance();
            ShowCreationOptions(m_firstCreationButton);
        }

        /// <summary>Previews a class without changing the committed class index.</summary>
        public void PreviewClass(int classIndex)
        {
            CharacterClass characterClass = GetStartingClass(classIndex);
            if (characterClass == null)
            {
                return;
            }

            m_previewPlayer?.ApplyCharacterClass(characterClass);
            if (m_classValue != null)
            {
                m_classValue.text = characterClass.ClassName;
            }
        }

        /// <summary>Commits the selected class and closes its menu.</summary>
        public void SelectClass(int classIndex)
        {
            if (GetStartingClass(classIndex) == null)
            {
                return;
            }

            m_selectedClassIndex = classIndex;
            ApplySelectedClass();
            ShowCreationOptions(m_firstCreationButton);
        }

        /// <summary>Previews a hairstyle locally without publishing owner state.</summary>
        public void PreviewHairstyle(int hairstyleID)
        {
            m_previewPlayer?.BodyManager?.SetHairstyle(hairstyleID);
            if (m_hairValue != null)
            {
                m_hairValue.text = hairstyleID == 0
                    ? "Bald"
                    : $"Style {hairstyleID:00}";
            }
        }

        /// <summary>Commits a hairstyle to the owner NetworkVariable and closes its menu.</summary>
        public void SelectHairstyle(int hairstyleID)
        {
            int maximumIndex = Mathf.Max(
                0,
                (m_previewPlayer?.BodyManager?.HairstyleCount ?? 1) - 1);
            m_selectedHairstyleID = Mathf.Clamp(hairstyleID, 0, maximumIndex);
            if (m_previewPlayer?.PlayerNetworkManager != null)
            {
                m_previewPlayer.PlayerNetworkManager.HairstyleID.Value =
                    m_selectedHairstyleID;
            }

            SetHeadEquipmentEditing(false);
            ApplySelectedAppearance();
            ShowCreationOptions(m_firstCreationButton);
        }

        /// <summary>Previews one exact RGB hair swatch locally.</summary>
        public void PreviewHairColor(Color32 hairColor)
        {
            m_previewPlayer?.BodyManager?.SetHairColor(
                hairColor.r,
                hairColor.g,
                hairColor.b);
            if (m_hairColorValue != null)
            {
                m_hairColorValue.color = hairColor;
            }
        }

        /// <summary>Commits one RGB hair swatch to synchronized appearance state.</summary>
        public void SelectHairColor(Color32 hairColor)
        {
            m_selectedHairColor = hairColor;
            PublishSelectedHairColor();
            SetColorSliders(hairColor);
            SetHeadEquipmentEditing(false);
            ShowCreationOptions(m_firstCreationButton);
        }

        /// <summary>Commits the current RGB slider preview.</summary>
        public void CommitSliderHairColor()
        {
            SelectHairColor(new Color32(
                (byte)Mathf.RoundToInt(m_hairColorRedSlider?.value ?? 0f),
                (byte)Mathf.RoundToInt(m_hairColorGreenSlider?.value ?? 0f),
                (byte)Mathf.RoundToInt(m_hairColorBlueSlider?.value ?? 0f),
                255));
        }

        /// <summary>Toggles the owner body type while preserving hair and equipped armor.</summary>
        public void ToggleSex()
        {
            PlayerNetworkManager networkManager =
                m_previewPlayer?.PlayerNetworkManager;
            if (networkManager == null)
            {
                return;
            }

            networkManager.IsMale.Value = !networkManager.IsMale.Value;
            RefreshSexValue();
            ApplySelectedAppearance();
        }

        /// <summary>Sanitizes, publishes, and displays the submitted character name.</summary>
        public void SubmitCharacterName(string characterName)
        {
            string resolvedName = SanitizeCharacterName(characterName);
            if (m_previewPlayer?.PlayerNetworkManager != null)
            {
                m_previewPlayer.PlayerNetworkManager.CharacterName.Value =
                    new FixedString64Bytes(resolvedName);
            }

            if (m_characterNameValue != null)
            {
                m_characterNameValue.text = resolvedName;
            }

            if (m_characterNameInput != null)
            {
                m_characterNameInput.text = resolvedName;
                m_characterNameInput.DeactivateInputField();
            }

            ShowCreationOptions(m_firstCreationButton);
        }

        /// <summary>Creates the initial save snapshot and enters the configured world.</summary>
        public void StartGame()
        {
            if (m_previewPlayer == null || WorldSaveGameManager.Instance == null)
            {
                return;
            }

            ApplySelectedClass();
            ApplySelectedAppearance();
            SubmitNameWithoutClosing();
            CharacterSaveData startingData = m_previewPlayer.CreateCharacterSaveData();
            if (startingData == null || !WorldSaveGameManager.Instance.NewGame(startingData))
            {
                m_titleScreenManager?.DisplayNoFreeCharacterSlotsPopup();
                return;
            }

            CloseCharacterCreation();
        }

        /// <summary>Trims a name, enforces 15 characters, and supplies the default name.</summary>
        public static string SanitizeCharacterName(string characterName)
        {
            string resolvedName = string.IsNullOrWhiteSpace(characterName)
                ? "Unnamed"
                : characterName.Trim();
            return resolvedName.Length <= MaximumCharacterNameLength
                ? resolvedName
                : resolvedName[..MaximumCharacterNameLength];
        }

        /// <summary>
        /// Confirms the authored name input. Exists because a UnityEvent cannot
        /// capture the live input text as a dynamic argument.
        /// </summary>
        public void ConfirmAuthoredName()
        {
            SubmitCharacterName(
                m_characterNameInput != null
                    ? m_characterNameInput.text
                    : ResolveCharacterName());
        }

        private IEnumerator ResolvePreviewPlayer()
        {
            while (IsOpen && m_previewPlayer == null)
            {
                IReadOnlyList<PlayerManager> players =
                    WorldGameSessionManager.Instance?.Players;
                if (players != null)
                {
                    foreach (PlayerManager player in players)
                    {
                        if (player != null && player.IsOwner)
                        {
                            InitializePreviewPlayer(player);
                            break;
                        }
                    }
                }

                if (m_previewPlayer == null)
                {
                    yield return null;
                }
            }

            m_resolvePlayerCoroutine = null;
        }

        private void InitializePreviewPlayer(PlayerManager previewPlayer)
        {
            m_previewPlayer = previewPlayer;
            ApplyPreviewLayer(previewPlayer.gameObject);
            m_previewRotator?.BindTarget(previewPlayer.transform);
            PositionPreviewCamera(previewPlayer.transform);
            PlayerNetworkManager networkManager = previewPlayer.PlayerNetworkManager;
            if (networkManager != null)
            {
                networkManager.IsMale.Value = true;
                networkManager.CharacterName.Value = new FixedString64Bytes("Unnamed");
                int defaultHairstyle = previewPlayer.BodyManager?.HairstyleCount > 1
                    ? 1
                    : 0;
                networkManager.HairstyleID.Value = defaultHairstyle;
                networkManager.HairColorRed.Value = m_selectedHairColor.r;
                networkManager.HairColorGreen.Value = m_selectedHairColor.g;
                networkManager.HairColorBlue.Value = m_selectedHairColor.b;
                m_selectedHairstyleID = defaultHairstyle;
            }

            m_selectedClassIndex = 0;
            ApplySelectedClass();
            ApplySelectedAppearance();
            RefreshSexValue();
            if (m_characterNameValue != null)
            {
                m_characterNameValue.text = "Unnamed";
            }
        }

        private CharacterClass GetStartingClass(int classIndex)
        {
            CharacterClass[] startingClasses = m_titleScreenManager?.StartingClasses;
            return startingClasses != null &&
                classIndex >= 0 &&
                classIndex < startingClasses.Length
                    ? startingClasses[classIndex]
                    : null;
        }

        private void ApplySelectedClass()
        {
            CharacterClass characterClass = GetStartingClass(m_selectedClassIndex);
            m_previewPlayer?.ApplyCharacterClass(characterClass);
            if (m_classValue != null && characterClass != null)
            {
                m_classValue.text = characterClass.ClassName;
            }
        }

        private void ApplySelectedAppearance()
        {
            if (m_previewPlayer?.PlayerNetworkManager != null)
            {
                m_previewPlayer.PlayerNetworkManager.HairstyleID.Value =
                    m_selectedHairstyleID;
            }

            m_previewPlayer?.BodyManager?.SetHairstyle(m_selectedHairstyleID);
            PublishSelectedHairColor();
            PreviewHairColor(m_selectedHairColor);
            if (m_hairValue != null)
            {
                m_hairValue.text = m_selectedHairstyleID == 0
                    ? "Bald"
                    : $"Style {m_selectedHairstyleID:00}";
            }
        }

        private void PublishSelectedHairColor()
        {
            PlayerNetworkManager networkManager =
                m_previewPlayer?.PlayerNetworkManager;
            if (networkManager == null)
            {
                return;
            }

            networkManager.HairColorRed.Value = m_selectedHairColor.r;
            networkManager.HairColorGreen.Value = m_selectedHairColor.g;
            networkManager.HairColorBlue.Value = m_selectedHairColor.b;
        }

        private void OnHairColorSliderChanged(float sliderValue)
        {
            if (m_isInitializingColorSliders)
            {
                return;
            }

            Color32 previewColor = new(
                (byte)Mathf.RoundToInt(m_hairColorRedSlider?.value ?? 0f),
                (byte)Mathf.RoundToInt(m_hairColorGreenSlider?.value ?? 0f),
                (byte)Mathf.RoundToInt(m_hairColorBlueSlider?.value ?? 0f),
                255);
            PreviewHairColor(previewColor);
        }

        private void SetColorSliders(Color32 hairColor)
        {
            m_isInitializingColorSliders = true;
            if (m_hairColorRedSlider != null)
            {
                m_hairColorRedSlider.value = hairColor.r;
            }

            if (m_hairColorGreenSlider != null)
            {
                m_hairColorGreenSlider.value = hairColor.g;
            }

            if (m_hairColorBlueSlider != null)
            {
                m_hairColorBlueSlider.value = hairColor.b;
            }

            m_isInitializingColorSliders = false;
        }

        private void SubmitNameWithoutClosing()
        {
            string resolvedName = SanitizeCharacterName(
                m_characterNameInput != null
                    ? m_characterNameInput.text
                    : ResolveCharacterName());
            if (m_previewPlayer?.PlayerNetworkManager != null)
            {
                m_previewPlayer.PlayerNetworkManager.CharacterName.Value =
                    new FixedString64Bytes(resolvedName);
            }
        }

        private string ResolveCharacterName()
        {
            return m_previewPlayer?.PlayerNetworkManager != null
                ? SanitizeCharacterName(
                    m_previewPlayer.PlayerNetworkManager.CharacterName.Value.ToString())
                : "Unnamed";
        }

        private void RefreshSexValue()
        {
            if (m_sexValue != null)
            {
                m_sexValue.text =
                    m_previewPlayer?.PlayerNetworkManager?.IsMale.Value != false
                        ? "Male"
                        : "Female";
            }
        }

        private void SetHeadEquipmentEditing(bool isEditing)
        {
            m_previewPlayer?.EquipmentManager
                ?.SetHeadEquipmentPresentationHidden(isEditing);
        }

        private void ShowSubMenu(
            GameObject subMenu,
            Selectable firstSelection,
            bool isEditingHead)
        {
            CloseAllSubMenus();
            m_creationOptions?.SetActive(false);
            subMenu?.SetActive(true);
            SetHeadEquipmentEditing(isEditingHead);
            firstSelection?.Select();
        }

        private void ShowCreationOptions(Selectable firstSelection)
        {
            CloseAllSubMenus();
            m_creationOptions?.SetActive(true);
            firstSelection?.Select();
        }

        private void CloseAllSubMenus()
        {
            m_classMenu?.SetActive(false);
            m_hairMenu?.SetActive(false);
            m_hairColorMenu?.SetActive(false);
            m_nameMenu?.SetActive(false);
        }

        private static void ConfigureColorSlider(Slider slider)
        {
            if (slider == null)
            {
                return;
            }

            slider.minValue = 0f;
            slider.maxValue = 255f;
            slider.wholeNumbers = true;
        }

        private void BuildRuntimeInterface(Button styleSource)
        {
            FontStyles titleStyle = FontStyles.SmallCaps;
            GameObject root = CreateUIObject("Character Creation Menu", transform);
            m_characterCreationRoot = root;
            StretchToParent(root.GetComponent<RectTransform>());
            Image backdrop = root.AddComponent<Image>();
            backdrop.color = new Color(0.015f, 0.012f, 0.01f, 0.96f);

            TMP_Text title = CreateText(root.transform, "CHARACTER CREATION", 38f);
            title.fontStyle = titleStyle;
            SetRect(title.rectTransform, new Vector2(0f, 410f), new Vector2(800f, 70f));

            m_creationOptions = CreatePanel(
                root.transform,
                "Creation Options",
                new Vector2(-560f, -20f),
                new Vector2(420f, 650f));
            Button nameButton = CreateButton(
                m_creationOptions.transform,
                "Name: Unnamed",
                styleSource,
                OpenNameMenu);
            m_firstCreationButton = nameButton;
            m_characterNameValue = nameButton.GetComponentInChildren<TMP_Text>();
            Button classButton = CreateButton(
                m_creationOptions.transform,
                "Class: Knight",
                styleSource,
                OpenClassMenu);
            m_classValue = classButton.GetComponentInChildren<TMP_Text>();
            Button hairButton = CreateButton(
                m_creationOptions.transform,
                "Hair: Style 01",
                styleSource,
                OpenHairMenu);
            m_hairValue = hairButton.GetComponentInChildren<TMP_Text>();
            Button hairColorButton = CreateButton(
                m_creationOptions.transform,
                "Hair Color",
                styleSource,
                OpenHairColorMenu);
            m_hairColorValue = hairColorButton.GetComponent<Image>();
            Button sexButton = CreateButton(
                m_creationOptions.transform,
                "Male",
                styleSource,
                ToggleSex);
            m_sexValue = sexButton.GetComponentInChildren<TMP_Text>();
            CreateButton(
                m_creationOptions.transform,
                "START GAME",
                styleSource,
                StartGame);
            CreateButton(
                m_creationOptions.transform,
                "RETURN",
                styleSource,
                CancelCharacterCreation);

            BuildClassMenu(root.transform, styleSource);
            BuildHairMenu(root.transform, styleSource);
            BuildHairColorMenu(root.transform, styleSource);
            BuildNameMenu(root.transform, styleSource);

            m_previewRotator = gameObject.AddComponent<TitleScreenPlayerPreviewRotator>();
            m_characterCreationRoot.SetActive(false);
        }

        private void BuildClassMenu(Transform parent, Button styleSource)
        {
            m_classMenu = CreatePanel(
                parent,
                "Class Menu",
                new Vector2(-70f, 40f),
                new Vector2(400f, 420f));
            Button knightButton = CreateButton(
                m_classMenu.transform,
                "Knight",
                styleSource,
                null);
            knightButton.gameObject.AddComponent<UICharacterClassButton>()
                .Configure(this, 0);
            m_firstClassButton = knightButton;
            Button rangerButton = CreateButton(
                m_classMenu.transform,
                "Ranger",
                styleSource,
                null);
            rangerButton.gameObject.AddComponent<UICharacterClassButton>()
                .Configure(this, 1);
            CreateButton(m_classMenu.transform, "RETURN", styleSource, CloseSubMenu);
            m_classMenu.SetActive(false);
        }

        private void BuildHairMenu(Transform parent, Button styleSource)
        {
            m_hairMenu = CreatePanel(
                parent,
                "Hair Menu",
                new Vector2(-70f, 0f),
                new Vector2(400f, 700f));
            for (int hairstyleID = 0; hairstyleID <= 8; hairstyleID++)
            {
                Button hairButton = CreateButton(
                    m_hairMenu.transform,
                    hairstyleID == 0 ? "Bald" : $"Style {hairstyleID:00}",
                    styleSource,
                    null);
                hairButton.gameObject.AddComponent<UIHairstyleButton>()
                    .Configure(this, hairstyleID);
                m_firstHairButton ??= hairButton;
            }

            CreateButton(m_hairMenu.transform, "RETURN", styleSource, CloseSubMenu);
            m_hairMenu.SetActive(false);
        }

        private void BuildHairColorMenu(Transform parent, Button styleSource)
        {
            m_hairColorMenu = CreatePanel(
                parent,
                "Hair Color Menu",
                new Vector2(-70f, 0f),
                new Vector2(440f, 700f));
            Color32[] colors =
            {
                new(38, 24, 16, 255),
                new(79, 53, 35, 255),
                new(128, 88, 55, 255),
                new(190, 155, 98, 255),
                new(170, 170, 170, 255),
                new(35, 35, 38, 255)
            };
            foreach (Color32 hairColor in colors)
            {
                Button colorButton = CreateButton(
                    m_hairColorMenu.transform,
                    string.Empty,
                    styleSource,
                    null);
                colorButton.GetComponent<Image>().color = hairColor;
                colorButton.gameObject.AddComponent<UIColorButton>()
                    .Configure(this);
                m_firstHairColorButton ??= colorButton;
            }

            m_hairColorRedSlider = CreateSlider(m_hairColorMenu.transform, "RED");
            m_hairColorGreenSlider = CreateSlider(m_hairColorMenu.transform, "GREEN");
            m_hairColorBlueSlider = CreateSlider(m_hairColorMenu.transform, "BLUE");
            CreateButton(
                m_hairColorMenu.transform,
                "APPLY RGB",
                styleSource,
                CommitSliderHairColor);
            CreateButton(m_hairColorMenu.transform, "RETURN", styleSource, CloseSubMenu);
            m_hairColorMenu.SetActive(false);
        }

        private void BuildNameMenu(Transform parent, Button styleSource)
        {
            m_nameMenu = CreatePanel(
                parent,
                "Name Menu",
                new Vector2(-70f, 80f),
                new Vector2(500f, 320f));
            m_characterNameInput = CreateInputField(m_nameMenu.transform);
            m_nameInputButton = m_characterNameInput;
            CreateButton(
                m_nameMenu.transform,
                "CONFIRM",
                styleSource,
                () => SubmitCharacterName(m_characterNameInput.text));
            CreateButton(m_nameMenu.transform, "RETURN", styleSource, CloseSubMenu);
            m_nameMenu.SetActive(false);
        }

        /// <summary>
        /// Guarantees the creation preview exists regardless of whether the creation
        /// UI is authored in the Scene or built at runtime. Only the 3D preview
        /// pieces (RenderTexture, camera, spot light) and a fallback RawImage are
        /// created here; an authored <see cref="RawImage"/> is used as-is.
        /// </summary>
        private void EnsurePreviewInfrastructure()
        {
            if (m_previewImage == null && m_characterCreationRoot != null)
            {
                GameObject previewObject = CreateUIObject(
                    "Player Preview",
                    m_characterCreationRoot.transform);
                m_previewImage = previewObject.AddComponent<RawImage>();
                m_previewImage.raycastTarget = false;
                SetRect(
                    previewObject.GetComponent<RectTransform>(),
                    new Vector2(490f, -10f),
                    new Vector2(620f, 720f));
            }

            if (m_previewTexture == null)
            {
                m_previewTexture = new RenderTexture(512, 512, 16)
                {
                    name = "Character Creation Preview"
                };
                m_previewTexture.Create();
            }

            if (m_previewImage != null)
            {
                m_previewImage.texture = m_previewTexture;
            }

            if (m_previewCamera == null)
            {
                GameObject cameraObject = new("Character Creation Preview Camera");
                cameraObject.transform.SetParent(transform, false);
                m_previewCamera = cameraObject.AddComponent<Camera>();
                m_previewCamera.fieldOfView = 50f;
                m_previewCamera.clearFlags = CameraClearFlags.SolidColor;
                m_previewCamera.backgroundColor = new Color(0.025f, 0.02f, 0.015f, 1f);
                m_previewCamera.cullingMask = 1 << LayerMask.NameToLayer("Player");
                m_previewCamera.targetTexture = m_previewTexture;
            }

            if (m_previewLight == null)
            {
                GameObject lightObject = new("Character Creation Spotlight");
                lightObject.transform.SetParent(m_previewCamera.transform, false);
                m_previewLight = lightObject.AddComponent<Light>();
                m_previewLight.type = LightType.Spot;
                m_previewLight.range = 10f;
                m_previewLight.intensity = 7f;
                m_previewLight.spotAngle = 55f;
                m_previewLight.cullingMask = m_previewCamera.cullingMask;
            }
        }

        private void PositionPreviewCamera(Transform playerTransform)
        {
            if (m_previewCamera == null || playerTransform == null)
            {
                return;
            }

            Vector3 targetPosition = playerTransform.position + Vector3.up * 1.15f;
            m_previewCamera.transform.position =
                playerTransform.position + new Vector3(0f, 1.25f, 3.2f);
            m_previewCamera.transform.LookAt(targetPosition);
            if (m_previewLight != null)
            {
                m_previewLight.transform.localPosition = Vector3.zero;
                m_previewLight.transform.localRotation = Quaternion.identity;
            }
        }

        private void ApplyPreviewLayer(GameObject playerObject)
        {
            int previewLayer = LayerMask.NameToLayer("Player");
            if (playerObject == null || previewLayer < 0)
            {
                return;
            }

            m_previewOriginalLayers.Clear();
            foreach (Transform child in playerObject.GetComponentsInChildren<Transform>(true))
            {
                m_previewOriginalLayers[child.gameObject] = child.gameObject.layer;
                child.gameObject.layer = previewLayer;
            }
        }

        private void RestorePreviewLayers()
        {
            foreach (KeyValuePair<GameObject, int> originalLayer in m_previewOriginalLayers)
            {
                if (originalLayer.Key != null)
                {
                    originalLayer.Key.layer = originalLayer.Value;
                }
            }

            m_previewOriginalLayers.Clear();
        }

        private static GameObject CreatePanel(
            Transform parent,
            string objectName,
            Vector2 position,
            Vector2 size)
        {
            GameObject panel = CreateUIObject(objectName, parent);
            Image image = panel.AddComponent<Image>();
            image.color = new Color(0.035f, 0.028f, 0.02f, 0.88f);
            SetRect(panel.GetComponent<RectTransform>(), position, size);
            VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 24, 24);
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            return panel;
        }

        private static Button CreateButton(
            Transform parent,
            string label,
            Button styleSource,
            UnityAction action)
        {
            GameObject buttonObject = CreateUIObject($"{label} Button", parent);
            Image image = buttonObject.AddComponent<Image>();
            Button button = buttonObject.AddComponent<Button>();
            if (styleSource != null)
            {
                Image styleImage = styleSource.GetComponent<Image>();
                if (styleImage != null)
                {
                    image.sprite = styleImage.sprite;
                    image.type = styleImage.type;
                    image.color = styleImage.color;
                    image.preserveAspect = styleImage.preserveAspect;
                }

                button.transition = styleSource.transition;
                button.colors = styleSource.colors;
                button.spriteState = styleSource.spriteState;
            }
            else
            {
                image.color = new Color(0.15f, 0.11f, 0.07f, 0.95f);
            }

            LayoutElement layout = buttonObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 58f;
            TMP_Text text = CreateText(buttonObject.transform, label, 25f);
            StretchToParent(text.rectTransform);
            if (action != null)
            {
                button.onClick.AddListener(action);
            }

            return button;
        }

        private static Slider CreateSlider(Transform parent, string label)
        {
            GameObject sliderObject = CreateUIObject($"{label} Slider", parent);
            LayoutElement layout = sliderObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 52f;
            Image background = sliderObject.AddComponent<Image>();
            background.color = new Color(0.12f, 0.09f, 0.06f, 1f);
            Slider slider = sliderObject.AddComponent<Slider>();
            slider.targetGraphic = background;
            slider.direction = Slider.Direction.LeftToRight;
            TMP_Text text = CreateText(sliderObject.transform, label, 18f);
            text.alignment = TextAlignmentOptions.Left;
            StretchToParent(text.rectTransform);
            return slider;
        }

        private static TMP_InputField CreateInputField(Transform parent)
        {
            GameObject inputObject = CreateUIObject("Character Name Input", parent);
            Image image = inputObject.AddComponent<Image>();
            image.color = new Color(0.12f, 0.09f, 0.06f, 1f);
            LayoutElement layout = inputObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 70f;
            TMP_InputField inputField = inputObject.AddComponent<TMP_InputField>();
            TMP_Text text = CreateText(inputObject.transform, "", 28f);
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            StretchToParent(text.rectTransform);
            inputField.textComponent = text;
            inputField.textViewport = inputObject.GetComponent<RectTransform>();
            inputField.characterLimit = MaximumCharacterNameLength;
            return inputField;
        }

        private static TMP_Text CreateText(
            Transform parent,
            string value,
            float fontSize)
        {
            GameObject textObject = CreateUIObject("Text", parent);
            TMP_Text text = textObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(0.93f, 0.78f, 0.46f, 1f);
            text.raycastTarget = false;
            return text;
        }

        private static GameObject CreateUIObject(string objectName, Transform parent)
        {
            GameObject uiObject = new(objectName, typeof(RectTransform));
            uiObject.transform.SetParent(parent, false);
            return uiObject;
        }

        private static void StretchToParent(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        private static void SetRect(
            RectTransform rectTransform,
            Vector2 position,
            Vector2 size)
        {
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = position;
            rectTransform.sizeDelta = size;
        }
    }
}
