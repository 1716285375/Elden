using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TextCore;
using UnityEngine.UI;

namespace ZZ.Editor
{
    /// <summary>Builds the TMP atlas from supplied artwork and migrates authored hint labels.</summary>
    public static class InputHintSetup
    {
        private const string k_Output = "Assets/_Game/Resources/InputHints";
        private const string k_Art = "Assets/_Game/Art/UI/HUD/Input Button";
        private static readonly Dictionary<string, string> s_templates = new()
        {
            { "Y / R", "{Interact}" },
            { "Y / R   Continue", "{Interact}   Continue" },
            { "ESC / MENU   CLOSE", "{Open Character Menu}   CLOSE" },
            { "A / ENTER   SELECT        B / ESC   CLOSE", "{Submit}   SELECT        {Cancel}   CLOSE" },
            { "X / SQUARE   UNEQUIP", "{Unequip Item}   UNEQUIP" },
            { "A  SELECT      X  DELETE", "{Submit}   SELECT      {Delete}   DELETE" },
            { "B  BACK", "{Cancel}   BACK" },
            { "PRESS ANY BUTTON", "{Any}   START" }
        };

        [MenuItem("Tools/ZZ/Build Input Hints")]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                throw new System.InvalidOperationException("Stop Play Mode before migrating UI assets.");
            }
            Directory.CreateDirectory(k_Output);
            AssetDatabase.Refresh();
            TMP_SpriteAsset icons = BuildAtlas();
            InputHintCatalog catalog = AssetDatabase.LoadAssetAtPath<InputHintCatalog>(k_Output + "/Catalog.asset");
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<InputHintCatalog>();
                AssetDatabase.CreateAsset(catalog, k_Output + "/Catalog.asset");
            }
            var data = new SerializedObject(catalog);
            data.FindProperty("m_icons").objectReferenceValue = icons;
            data.FindProperty("m_actions").objectReferenceValue = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
                "Assets/_Game/Settings/Input/PlayerControls.inputactions");
            data.ApplyModifiedPropertiesWithoutUndo();
            int count = 0;
            string path = "Assets/_Game/Prefabs/World/Managers/Player UI Manager.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                count += Migrate(root, catalog);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != "Assets/_Game/Scenes/Frontend/SCN_MainMenu.unity")
            {
                throw new System.InvalidOperationException("Open SCN_MainMenu to migrate its hints.");
            }
            foreach (GameObject sceneRoot in scene.GetRootGameObjects())
            {
                count += Migrate(sceneRoot, catalog);
            }
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Input Hints] Migrated {count} labels; {icons.spriteCharacterTable.Count} supplied icons.");
        }

        [MenuItem("Tools/ZZ/Audit Menu Wiring")]
        public static void Audit()
        {
            var lines = new List<string>();
            foreach (Button button in Object.FindObjectsByType<Button>(FindObjectsInactive.Include,
                FindObjectsSortMode.None))
            {
                string path = AnimationUtility.CalculateTransformPath(button.transform, null);
                if (!path.Contains("Character Menu") && !path.Contains("Upgrade") && !path.Contains("Load Game"))
                {
                    continue;
                }
                lines.Add($"{path} active={button.gameObject.activeInHierarchy} enabled={button.interactable}");
                for (int index = 0; index < button.onClick.GetPersistentEventCount(); index++)
                {
                    lines.Add($"  {button.onClick.GetPersistentTarget(index)} :: " +
                        $"{button.onClick.GetPersistentMethodName(index)} [{button.onClick.GetPersistentListenerState(index)}]");
                }
            }
            foreach (PlayerUIMenu menu in Object.FindObjectsByType<PlayerUIMenu>(FindObjectsInactive.Include,
                FindObjectsSortMode.None))
            {
                var data = new SerializedObject(menu);
                lines.Add($"MENU {menu.GetType().Name}: {data.FindProperty("m_menuWindow").objectReferenceValue}");
            }
            File.WriteAllLines(".utmp/menu-wiring.txt", lines);
        }

        private static int Migrate(GameObject root, InputHintCatalog catalog)
        {
            int count = 0;
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                InputHintText existing = text.GetComponent<InputHintText>();
                string template = existing?.Template;
                if (existing == null && !s_templates.TryGetValue(text.text, out template))
                {
                    continue;
                }
                InputHintText hint = text.GetComponent<InputHintText>() ?? text.gameObject.AddComponent<InputHintText>();
                var data = new SerializedObject(hint);
                data.FindProperty("m_template").stringValue = template;
                data.FindProperty("m_icons").objectReferenceValue = catalog.Icons;
                data.FindProperty("m_actions").objectReferenceValue = catalog.Actions;
                data.ApplyModifiedPropertiesWithoutUndo();
                hint.Refresh();
                text.enableAutoSizing = false;
                text.fontSize = Mathf.Max(24, text.fontSize);
                EditorUtility.SetDirty(text);
                count++;
            }
            return count;
        }

        private static TMP_SpriteAsset BuildAtlas()
        {
            string[] paths = Directory.GetFiles(k_Art + "/Keyboard & Mouse/Default", "*.png")
                .Concat(Directory.GetFiles(k_Art + "/Xbox Series/Default", "*.png"))
                .Where(path => !path.Contains("outline")).OrderBy(path => path).ToArray();
            Texture2D[] textures = paths.Select(path =>
            {
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                texture.LoadImage(File.ReadAllBytes(path));
                return texture;
            }).ToArray();
            var atlas = new Texture2D(2048, 2048, TextureFormat.RGBA32, false);
            Rect[] rectangles = atlas.PackTextures(textures, 4, 2048);
            string atlasPath = k_Output + "/InputButtons.png";
            File.WriteAllBytes(atlasPath, atlas.EncodeToPNG());
            AssetDatabase.ImportAsset(atlasPath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(atlasPath);
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            string assetPath = k_Output + "/InputButtons.asset";
            TMP_SpriteAsset asset = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(assetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
                AssetDatabase.CreateAsset(asset, assetPath);
            }
            asset.spriteSheet = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);
            var serializedAsset = new SerializedObject(asset);
            serializedAsset.FindProperty("m_Version").stringValue = "1.1.0";
            serializedAsset.ApplyModifiedPropertiesWithoutUndo();
            if (asset.material == null)
            {
                asset.material = new Material(Shader.Find("TextMeshPro/Sprite"));
                AssetDatabase.AddObjectToAsset(asset.material, asset);
            }
            asset.material.mainTexture = asset.spriteSheet;
            asset.spriteCharacterTable.Clear();
            asset.spriteGlyphTable.Clear();
            for (int index = 0; index < paths.Length; index++)
            {
                Rect rectangle = rectangles[index];
                var glyphRect = new GlyphRect(Mathf.RoundToInt(rectangle.x * atlas.width),
                    Mathf.RoundToInt(rectangle.y * atlas.height), Mathf.RoundToInt(rectangle.width * atlas.width),
                    Mathf.RoundToInt(rectangle.height * atlas.height));
                var glyph = new TMP_SpriteGlyph((uint)index,
                    new GlyphMetrics(glyphRect.width, glyphRect.height, 0, glyphRect.height * 0.85f, glyphRect.width),
                    glyphRect, 1, 0);
                asset.spriteGlyphTable.Add(glyph);
                asset.spriteCharacterTable.Add(new TMP_SpriteCharacter(0xFFFE, glyph)
                {
                    name = Path.GetFileNameWithoutExtension(paths[index]), scale = 1
                });
                Object.DestroyImmediate(textures[index]);
            }
            asset.UpdateLookupTables();
            EditorUtility.SetDirty(asset);
            EditorUtility.SetDirty(asset.material);
            Object.DestroyImmediate(atlas);
            return asset;
        }
    }
}
