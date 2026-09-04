using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Creates the unified LV01 LightingSettings asset (inheriting the project's
/// current configuration) and assigns it to every LV01 scene, so all Areas
/// bake with identical settings.
/// </summary>
public sealed class LV01LightingSettingsTests
{
    private const string k_Root = "Assets/_Game/Scenes/Levels/LV01_AbandonedMonastery";
    private const string k_SettingsAsset = k_Root + "/Shared/Lighting/Settings/LGT_LV01_LightingSettings.asset";

    [Test]
    public void CreateAndAssignUnifiedLightingSettings()
    {
        LightingSettings settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(k_SettingsAsset);
        if (settings == null)
        {
            // The project currently uses the built-in default lighting settings
            // (all scenes have m_LightingSettings fileID 0), so a fresh
            // LightingSettings carries exactly the current configuration.
            settings = new LightingSettings();
            settings.name = "LGT_LV01_LightingSettings";
            AssetDatabase.CreateAsset(settings, k_SettingsAsset);
            AssetDatabase.SaveAssets();
        }

        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(settings, out string guid, out long localId);
        Assert.IsTrue(File.Exists(k_SettingsAsset), "LightingSettings asset not created");

        string[] scenes =
        {
            "/SCN_LV01_AbandonedMonastery.unity",
            "/Regions/R01_MonasteryOutskirts/SCN_LV01_R01_A01_Base.unity",
            "/Regions/R01_MonasteryOutskirts/SCN_LV01_R01_A01_Props.unity",
            "/Regions/R01_MonasteryOutskirts/SCN_LV01_R01_A01_Effects.unity",
            "/Regions/R01_MonasteryOutskirts/SCN_LV01_R01_A01_Spawners.unity",
            "/Regions/R01_MonasteryOutskirts/SCN_LV01_R01_A02_Base.unity",
            "/Regions/R01_MonasteryOutskirts/SCN_LV01_R01_A02_Props.unity",
            "/Regions/R01_MonasteryOutskirts/SCN_LV01_R01_A02_Effects.unity",
            "/Regions/R01_MonasteryOutskirts/SCN_LV01_R01_A02_Spawners.unity",
            "/Regions/R01_MonasteryOutskirts/SCN_LV01_R01_A03_Base.unity",
            "/Regions/R01_MonasteryOutskirts/SCN_LV01_R01_A03_Props.unity",
            "/Regions/R01_MonasteryOutskirts/SCN_LV01_R01_A03_Effects.unity",
            "/Regions/R01_MonasteryOutskirts/SCN_LV01_R01_A03_Spawners.unity",
            "/Regions/R01_MonasteryOutskirts/SCN_LV01_R01_A04_Base.unity",
            "/Regions/R01_MonasteryOutskirts/SCN_LV01_R01_A04_Props.unity",
            "/Regions/R01_MonasteryOutskirts/SCN_LV01_R01_A04_Effects.unity",
            "/Regions/R01_MonasteryOutskirts/SCN_LV01_R01_A04_Spawners.unity",
        };

        foreach (string scenePath in scenes)
        {
            string fullPath = k_Root + scenePath;
            if (!File.Exists(fullPath))
            {
                continue;
            }
            EditorSceneManager.OpenScene(fullPath, OpenSceneMode.Single);
            Lightmapping.lightingSettings = settings;
            Assert.AreEqual(settings, Lightmapping.lightingSettings, $"LightingSettings not applied in memory: {fullPath}");
            bool saved = EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Assert.IsTrue(saved, $"Scene save failed: {fullPath}");
            EditorSceneManager.OpenScene(fullPath, OpenSceneMode.Single);
            Assert.AreEqual(settings, Lightmapping.lightingSettings, $"LightingSettings reference lost on disk: {fullPath}");
        }

        Debug.Log($"LightingSettings OK: {k_SettingsAsset} (guid {guid}), assigned to {scenes.Length} scenes");
    }

    private static bool ContainsText(byte[] haystack, string text)
    {
        byte[] needle = System.Text.Encoding.ASCII.GetBytes(text);
        for (int i = 0; i <= haystack.Length - needle.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j])
                {
                    match = false;
                    break;
                }
            }
            if (match)
            {
                return true;
            }
        }
        return false;
    }
}
