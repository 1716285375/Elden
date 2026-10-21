using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZZ.Editor
{
    public static class PresentationAudioSetup
    {
        [MenuItem("Tools/ZZ/Audio/Install Original Title And Repair Import Settings")]
        public static void Apply()
        {
            if (Application.isPlaying) { throw new InvalidOperationException("Stop Play Mode first."); }
            const string output = "Assets/_Game/Art/Audio/EldenRing/Title";
            Directory.CreateDirectory(output);
            foreach (string name in new[] { "TitleIntro.wav", "TitleLoop.wav" })
            {
                File.Copy("F:/MyProject/RE/EldenRing/Converted/AudioPresentation/" + name, output + "/" + name, true);
            }
            AssetDatabase.Refresh();
            int streaming = 0;
            int shortSounds = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/_Game/Audio", "Assets/_Game/Art/Audio" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (importer == null) { continue; }
                bool longClip = clip.length >= 10f;
                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                AudioClipLoadType loadType = longClip ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
                if (settings.loadType == loadType && settings.preloadAudioData == !longClip &&
                    importer.loadInBackground == longClip) { continue; }
                settings.loadType = loadType;
                settings.preloadAudioData = !longClip;
                importer.defaultSampleSettings = settings;
                importer.loadInBackground = longClip;
                importer.SaveAndReimport();
                if (longClip) { streaming++; } else { shortSounds++; }
            }
            Scene scene = EditorSceneManager.GetSceneByPath(WorldScenePathLayout.MainMenuScenePath);
            if (!scene.isLoaded) { scene = EditorSceneManager.OpenScene(WorldScenePathLayout.MainMenuScenePath); }
            GameObject root = scene.GetRootGameObjects().FirstOrDefault(item => item.name == "Original Title Music");
            if (root == null)
            {
                root = new GameObject("Original Title Music");
                SceneManager.MoveGameObjectToScene(root, scene);
            }
            var intro = ConfigureSource(root, "Introduction", output + "/TitleIntro.wav");
            var loop = ConfigureSource(root, "Loop", output + "/TitleLoop.wav");
            ScheduledMusicPlayer player = root.GetComponent<ScheduledMusicPlayer>();
            if (player == null) { player = root.AddComponent<ScheduledMusicPlayer>(); }
            var data = new SerializedObject(player);
            data.FindProperty("m_intro").objectReferenceValue = intro;
            data.FindProperty("m_loop").objectReferenceValue = loop;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Docs/PresentationRepair");
            File.WriteAllText("Docs/PresentationRepair/audio-install.txt",
                $"Original Play_m000000010 introduction and loop installed; streaming changes={streaming}; short clip changes={shortSounds}.\n");
        }

        private static AudioSource ConfigureSource(GameObject root, string name, string clipPath)
        {
            Transform child = root.transform.Find(name);
            if (child == null)
            {
                child = new GameObject(name).transform;
                child.SetParent(root.transform, false);
            }
            AudioSource source = child.GetComponent<AudioSource>();
            if (source == null) { source = child.gameObject.AddComponent<AudioSource>(); }
            source.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0.3f;
            return source;
        }
    }
}
