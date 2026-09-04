using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Bakes lighting per Area: opens the Master plus the Area's four slices,
/// bakes with the unified LightingSettings, then moves the generated
/// LightingData/Lightmaps/ReflectionProbe output into the Area's Lighting/Baked
/// folder via AssetDatabase (references remapped automatically).
/// </summary>
public sealed class LV01LightingBakeTests
{
    private const string k_Root = "Assets/_Game/Scenes/Levels/LV01_AbandonedMonastery";
    private const string k_Region = k_Root + "/Regions/R01_MonasteryOutskirts";
    private const string k_SettingsAsset = k_Root + "/Shared/Lighting/Settings/LGT_LV01_LightingSettings.asset";
    private static readonly string[] k_Slices = { "Base", "Props", "Effects", "Spawners" };

    [Test]
    public void BakeA01Lighting()
    {
        BakeArea("A01");
    }

    [Test]
    public void BakeA02Lighting()
    {
        BakeArea("A02");
    }

    [Test]
    public void BakeA03Lighting()
    {
        BakeArea("A03");
    }

    [Test]
    public void BakeA04Lighting()
    {
        BakeArea("A04");
    }

    private static void BakeArea(string area)
    {
        EditorSceneManager.OpenScene(k_Root + "/SCN_LV01_AbandonedMonastery.unity", OpenSceneMode.Single);
        foreach (string slice in k_Slices)
        {
            EditorSceneManager.OpenScene($"{k_Region}/SCN_LV01_R01_{area}_{slice}.unity", OpenSceneMode.Additive);
        }
        EditorSceneManager.SetActiveScene(SceneManager.GetSceneByPath($"{k_Region}/SCN_LV01_R01_{area}_Base.unity"));

        LightingSettings settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(k_SettingsAsset);
        Assert.IsNotNull(settings, "Unified LightingSettings missing");
        Assert.IsTrue(Lightmapping.lightingSettings == settings, $"Scene lighting settings mismatch for {area}");

        Lightmapping.Clear();
        Lightmapping.Bake();
        Assert.IsFalse(Lightmapping.isRunning, $"Bake incomplete for {area}");

        string bakeFolder = $"{k_Region}/{AreaFolder(area)}/Lighting/Baked";
        if (!AssetDatabase.IsValidFolder(bakeFolder))
        {
            AssetDatabase.CreateFolder($"{k_Region}/{AreaFolder(area)}/Lighting", "Baked");
        }
        List<string> moved = MoveBakeOutputs(k_Region, bakeFolder);
        Assert.IsNotEmpty(moved, $"No bake output found for {area}");

        foreach (Scene scene in GetAllScenes())
        {
            EditorSceneManager.SaveScene(scene);
        }
        Debug.Log($"Lighting baked for {area}: {string.Join(", ", moved.ToArray())}");
    }

    private static string AreaFolder(string area)
    {
        switch (area)
        {
            case "A01": return "A01_CliffPath";
            case "A02": return "A02_Graveyard";
            case "A03": return "A03_MainGate";
            case "A04": return "A04_GateTower";
            default: return area;
        }
    }

    private static List<string> MoveBakeOutputs(string sourceFolder, string targetFolder)
    {
        // Unity 6 writes per-scene bake output into a folder named after the scene.
        List<string> searchFolders = new List<string> { sourceFolder };
        foreach (string sub in Directory.GetDirectories(sourceFolder))
        {
            string name = Path.GetFileName(sub);
            if (name.StartsWith("SCN_LV01_R01_"))
            {
                searchFolders.Add(sub);
            }
        }

        List<string> moved = new List<string>();
        string[] patterns = { "LightingData.asset", "Lightmap-*.exr", "Lightmap-*.png", "ReflectionProbe-*.exr", "ReflectionProbe-*.png" };
        foreach (string folder in searchFolders)
        {
            foreach (string pattern in patterns)
            {
                foreach (string file in Directory.GetFiles(folder, pattern))
                {
                    string fileName = Path.GetFileName(file);
                    string sourceAsset = file.Replace('\\', '/');
                    string targetAsset = targetFolder + "/" + fileName;
                    string error = AssetDatabase.MoveAsset(sourceAsset, targetAsset);
                    if (string.IsNullOrEmpty(error))
                    {
                        moved.Add(fileName);
                    }
                    else
                    {
                        Debug.LogWarning($"Move failed {fileName}: {error}");
                    }
                }
            }
        }
        for (int i = 1; i < searchFolders.Count; i++)
        {
            if (Directory.GetFiles(searchFolders[i]).Length == 0)
            {
                AssetDatabase.DeleteAsset(searchFolders[i].Replace('\\', '/'));
            }
        }
        return moved;
    }

    private static Scene[] GetAllScenes()
    {
        int count = SceneManager.sceneCount;
        Scene[] scenes = new Scene[count];
        for (int i = 0; i < count; i++)
        {
            scenes[i] = SceneManager.GetSceneAt(i);
        }
        return scenes;
    }
}
