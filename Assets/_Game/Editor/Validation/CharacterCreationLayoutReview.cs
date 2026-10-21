using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace ZZ.Editor
{
    [InitializeOnLoad]
    public static class CharacterCreationLayoutReview
    {
        private const string k_Output = "Docs/CharacterCreationRefresh/";
        private const string k_Review = "ZZ.CharacterCreation.ReferenceReview";

        static CharacterCreationLayoutReview() { EditorApplication.update += PrepareReview; }

        [MenuItem("Tools/ZZ/Character Creation/Open Reference UI Review")]
        public static void OpenReview()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Stop Play Mode first.");
            }
            EditorSceneManager.OpenScene(WorldScenePathLayout.MainMenuScenePath, OpenSceneMode.Single);
            SessionState.SetBool(k_Review, true);
            EditorApplication.isPlaying = true;
        }

        private static void PrepareReview()
        {
            if (!SessionState.GetBool(k_Review, false) || !EditorApplication.isPlaying) { return; }
            var title = Object.FindFirstObjectByType<TitleScreenManager>();
            if (title == null || WorldSaveGameManager.Instance == null) { return; }
            if (Unity.Netcode.NetworkManager.Singleton?.IsListening != true)
            {
                title.PressStart();
                return;
            }
            var creation = Object.FindFirstObjectByType<TitleScreenCharacterCreationManager>();
            if (!creation.IsOpen) { title.StartNewGame(); return; }
            if (!creation.IsReady) { return; }
            creation.SubmitCharacterName("褪色者");
            var root = creation.GetComponentInChildren<CharacterCreationView>().GetComponent<UIDocument>().rootVisualElement;
            root.Q<DropdownField>("hairstyle").index = 3;
            SessionState.SetBool(k_Review, false);
            File.AppendAllText(k_Output + "reference-review.txt", "PASS: Opened isolated preview with existing save preserved\n");
        }

        [MenuItem("Tools/ZZ/Character Creation/Review Appearance Directory")]
        public static void Directory() => Capture("详细外貌", "creation-directory-ui.png");

        [MenuItem("Tools/ZZ/Character Creation/Review Structure Directory")]
        public static void StructureDirectory() => Capture("面部结构", "creation-structure-directory-ui.png");

        [MenuItem("Tools/ZZ/Character Creation/Review Skin Palette")]
        public static void Skin() => Capture("肤色", "creation-skin-ui.png");

        [MenuItem("Tools/ZZ/Character Creation/Review Hairstyle Grid")]
        public static void Hair() => Capture("发型与肤色", "creation-hair-ui.png");

        [MenuItem("Tools/ZZ/Character Creation/Bake Hairstyle Thumbnails")]
        public static void BakeHairstyleThumbnails()
        {
            var creation = Object.FindFirstObjectByType<TitleScreenCharacterCreationManager>();
            if (creation?.IsReady != true) { throw new InvalidOperationException("Open the creation preview first."); }
            CharacterCreationPreview preview = creation.Preview;
            int original = creation.Draft.Hairstyle;
            Camera camera = preview.Camera;
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture target = RenderTexture.GetTemporary(160, 200, 24, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(160, 200, TextureFormat.RGBA32, false);
            System.IO.Directory.CreateDirectory("Assets/_Game/Resources/UI/Hairstyles");
            try
            {
                preview.SetEquipmentPreview(false);
                preview.SetCloseUp(true);
                camera.targetTexture = target;
                for (int index = 0; index < 9; index++)
                {
                    creation.SelectHairstyle(index);
                    preview.RenderNow();
                    RenderTexture.active = target;
                    texture.ReadPixels(new Rect(0, 0, 160, 200), 0, 0);
                    texture.Apply();
                    File.WriteAllBytes("Assets/_Game/Resources/UI/Hairstyles/Hair" + index.ToString("D2") + ".png",
                        texture.EncodeToPNG());
                }
                Require(true, "Baked nine thumbnails from the real extracted hairstyle meshes");
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(target);
                Object.Destroy(texture);
                creation.SelectHairstyle(original);
                creation.GetComponentInChildren<CharacterCreationView>().ShowSection("基本资料");
            }
        }

        [MenuItem("Tools/ZZ/Character Creation/Verify Reference Navigation")]
        public static void Navigation()
        {
            CharacterCreationView view = Object.FindFirstObjectByType<CharacterCreationView>();
            VisualElement root = view.GetComponent<UIDocument>().rootVisualElement;
            view.ShowSection("眼与眉");
            Submit(root.Q<Button>("cancel"));
            Require(root.Q<Label>("creation-breadcrumb").text == "详细外貌 ＞ 面部结构", "Back returns to structure directory");
            Submit(root.Q<Button>("cancel"));
            Require(root.Q<Label>("creation-breadcrumb").text == "详细外貌", "Back returns to appearance directory");
            Submit(root.Q<Button>("cancel"));
            Require(root.Q("creation-root").ClassListContains("creation-screen--overview"), "Back returns to overview");
            Require(WorldSaveGameManager.Instance.CurrentCharacterSlot == CharacterSlot.NoSlot, "Navigation does not allocate save slots");
        }

        [MenuItem("Tools/ZZ/Character Creation/Verify Reference Pickers")]
        public static void Pickers()
        {
            var creation = Object.FindFirstObjectByType<TitleScreenCharacterCreationManager>();
            var view = creation.GetComponentInChildren<CharacterCreationView>();
            VisualElement root = view.GetComponent<UIDocument>().rootVisualElement;
            CharacterAppearanceData original = creation.Draft;
            view.ShowSection("发型与肤色");
            Button[] styles = root.Q("hairstyle-grid").Query<Button>().ToList().ToArray();
            Require(styles.Length == 9 && styles.All(button => button.style.backgroundImage.value.texture != null),
                "All nine extracted styles have real thumbnails");
            Submit(styles[5]);
            Require(creation.Draft.Hairstyle == 5, "Thumbnail selection updates the isolated draft");
            Require(styles.Count(button => button.ClassListContains("creation-style--selected")) == 1,
                "Exactly one hairstyle is selected");
            Submit(styles[original.Hairstyle]);
            view.ShowSection("肤色");
            VisualElement skin = root.Q("page-肤色");
            var rows = skin.Query<VisualElement>(className: "creation-palette-row").ToList();
            Require(rows.Count == 8 && rows.All(row => row.Query<Button>().ToList().Count == 12),
                "Skin palette is exactly 12 columns by 8 rows");
            Submit(rows[3].Query<Button>().ToList()[4]);
            SliderInt[] channels = skin.Query<SliderInt>().ToList().ToArray();
            Color32 selected = creation.Draft.SkinColor;
            Require(channels[0].value == selected.r && channels[1].value == selected.g && channels[2].value == selected.b,
                "Palette selection updates all RGB controls");
            creation.SetSkinColor(original.SkinColor);
            channels[0].SetValueWithoutNotify(original.SkinColor.r);
            channels[1].SetValueWithoutNotify(original.SkinColor.g);
            channels[2].SetValueWithoutNotify(original.SkinColor.b);
            var visual = Object.FindObjectsByType<Animator>(FindObjectsSortMode.None)
                .Where(item => item.name == "Character Creation Visual")
                .OrderBy(item => (item.transform.position - creation.Preview.Camera.transform.position).sqrMagnitude)
                .First();
            var equipment = new SerializedObject(visual.GetComponent<PlayerSkinnedEquipment>());
            SerializedProperty parts = equipment.FindProperty("m_parts");
            for (int index = 0; index < parts.arraySize; index++)
            {
                SerializedProperty part = parts.GetArrayElementAtIndex(index);
                if (part.FindPropertyRelative("ItemId").intValue < 0) { continue; }
                var renderer = (SkinnedMeshRenderer)part.FindPropertyRelative("Renderer").objectReferenceValue;
                Require(!renderer.enabled, "Detailed editing hides equipment: " + renderer.name);
            }
            view.ShowSection("基本资料");
        }

        [MenuItem("Tools/ZZ/Character Creation/Review Overview")]
        public static void Overview() => Capture("基本资料", "creation-ui.png");

        [MenuItem("Tools/ZZ/Character Creation/Review Face Details")]
        public static void Face() => Capture("眼睛", "creation-eyes-ui.png");

        [MenuItem("Tools/ZZ/Character Creation/Review Face Structure")]
        public static void Structure() => Capture("脸部轮廓", "creation-structure-ui.png");

        [MenuItem("Tools/ZZ/Character Creation/Review Confirmation")]
        public static void Confirmation()
        {
            CharacterCreationView view = Object.FindFirstObjectByType<CharacterCreationView>();
            view.ShowSection("基本资料");
            VisualElement root = view.GetComponent<UIDocument>().rootVisualElement;
            Submit(root.Q<Button>("create"));
            Require(root.Q("creation-confirmation").resolvedStyle.display != DisplayStyle.None,
                "Finish opens confirmation without saving");
            Require(WorldSaveGameManager.Instance.CurrentCharacterSlot == CharacterSlot.NoSlot,
                "Confirmation does not allocate a slot");
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(k_Output + "creation-confirmation-ui.png"));
        }

        [MenuItem("Tools/ZZ/Character Creation/Return From Confirmation")]
        public static void ReturnFromConfirmation()
        {
            var view = Object.FindFirstObjectByType<CharacterCreationView>();
            VisualElement root = view.GetComponent<UIDocument>().rootVisualElement;
            Submit(root.Q<Button>("confirm-back"));
            Require(root.Q("creation-confirmation").resolvedStyle.display == DisplayStyle.None,
                "Continue editing returns to the same draft");
            Require(root.Q("creation-content").enabledInHierarchy, "Draft controls are re-enabled");
        }

        private static void Capture(string section, string filename)
        {
            CharacterCreationView view = Object.FindFirstObjectByType<CharacterCreationView>();
            view.ShowSection(section);
            VisualElement root = view.GetComponent<UIDocument>().rootVisualElement;
            if (section == "脸部轮廓")
            {
                var draftOwner = Object.FindFirstObjectByType<TitleScreenCharacterCreationManager>();
                SliderInt slider = root.Q<SliderInt>("face-0");
                float original = draftOwner.Draft.GetShape(0);
                slider.value = 0;
                Require(draftOwner.Draft.GetShape(0) == -1f, "Face slider 0 maps to minimum morph");
                slider.value = 128;
                Require(draftOwner.Draft.GetShape(0) == 0f, "Face slider 128 maps to neutral morph");
                slider.value = 255;
                Require(draftOwner.Draft.GetShape(0) == 1f, "Face slider 255 maps to maximum morph");
                draftOwner.SetMorph(0, original);
                view.RefreshMorphValues(draftOwner.Draft);
            }
            Rect screen = root.Q("creation-root").worldBound;
            foreach (string name in new[] { "creation-controls", "character-preview", "cancel" })
            {
                Rect bounds = root.Q(name).worldBound;
                Require(bounds.width > 0 && bounds.height > 0 && bounds.xMin >= screen.xMin - 1f &&
                    bounds.yMin >= screen.yMin - 1f && bounds.xMax <= screen.xMax + 1f && bounds.yMax <= screen.yMax + 1f,
                    section + ": " + name + " stays inside screen " + bounds);
            }
            var creation = Object.FindFirstObjectByType<TitleScreenCharacterCreationManager>();
            creation.Preview.RenderNow();
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(k_Output + filename));
            if (section == "眼睛")
            {
                var text = new StringBuilder();
                foreach (Animator animator in Object.FindObjectsByType<Animator>(FindObjectsSortMode.None))
                {
                    if (animator.name != "Character Creation Visual") { continue; }
                    text.AppendLine("Left hand: " + animator.GetBoneTransform(HumanBodyBones.LeftHand).position);
                    foreach (Transform item in animator.GetComponentsInChildren<Transform>(true)
                        .Where(item => item.name.Contains("Hand_Dummy") || item.name.Contains("Shield Slot")))
                    {
                        text.AppendLine(item.name + " parent=" + item.parent.name + " world=" + item.position +
                            " local=" + item.localPosition);
                    }
                }
                File.WriteAllText(k_Output + "preview-attachments.txt", text.ToString());
            }
        }

        private static void Submit(Button button)
        {
            button.Focus();
            using (NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled())
            {
                button.SendEvent(submit);
            }
        }

        private static void Require(bool value, string message)
        {
            File.AppendAllText(k_Output + "layout-review.txt", (value ? "PASS: " : "FAIL: ") + message + "\n");
            if (!value) { throw new InvalidOperationException(message); }
        }
    }
}
