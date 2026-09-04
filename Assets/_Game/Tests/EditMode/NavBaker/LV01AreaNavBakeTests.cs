using System.IO;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// Bakes each LV01 Area Base scene's NavMeshSurface into its per-Area NavMeshData asset.
/// Loads the always-resident master terrain beside each Base slice so Volume collection
/// includes the real ground while preserving the per-Area volume configured in the scene.
/// </summary>
public sealed class LV01AreaNavBakeTests
{
    private const string k_Root = "Assets/_Game/Scenes/Levels/LV01_AbandonedMonastery";
    private const string k_MasterScene = "/SCN_LV01_AbandonedMonastery.unity";
    private const float k_Margin = 3f;

    private static void BakeArea(string areaScene, string navAssetPath)
    {
        EditorSceneManager.OpenScene(k_Root + k_MasterScene, OpenSceneMode.Single);
        Scene area = EditorSceneManager.OpenScene(k_Root + areaScene, OpenSceneMode.Additive);
        SceneManager.SetActiveScene(area);

        GameObject navRoot = FindRoot(area, "_Navigation");
        if (navRoot == null)
        {
            navRoot = new GameObject("_Navigation");
            SceneManager.MoveGameObjectToScene(navRoot, area);
        }

        NavMeshSurface surface = navRoot.GetComponent<NavMeshSurface>();
        if (surface == null)
        {
            surface = navRoot.AddComponent<NavMeshSurface>();
            surface.agentTypeID = 0;
            surface.collectObjects = CollectObjects.Volume;
            surface.layerMask = 1 << 0;
        }

        Bounds geometry = ComputeGeometryBounds();
        Assert.IsTrue(geometry.size.sqrMagnitude > 0f, $"No active geometry found in {areaScene}");
        if (surface.size.sqrMagnitude <= Mathf.Epsilon)
        {
            surface.center = geometry.center;
            surface.size = geometry.size + new Vector3(k_Margin, k_Margin, k_Margin);
        }

        // BuildNavMesh() replaces m_NavMeshData with a fresh in-memory instance,
        // so bake first, then transfer the result into the asset and re-assign.
        surface.BuildNavMesh();
        NavMeshData built = surface.navMeshData;
        Assert.IsNotNull(built, $"Bake produced no NavMeshData (empty volume?) for {areaScene}");

        string directory = Path.GetDirectoryName(navAssetPath);
        if (!string.IsNullOrEmpty(directory) && !AssetDatabase.IsValidFolder(directory))
        {
            Directory.CreateDirectory(directory);
            AssetDatabase.Refresh();
        }

        NavMeshData data = AssetDatabase.LoadAssetAtPath<NavMeshData>(navAssetPath);
        if (data == null)
        {
            data = built;
            AssetDatabase.CreateAsset(data, navAssetPath);
        }
        else
        {
            EditorUtility.CopySerialized(built, data);
        }

        surface.navMeshData = data;
        EditorUtility.SetDirty(data);
        EditorUtility.SetDirty(surface);
        AssetDatabase.SaveAssets();
        bool saved = EditorSceneManager.SaveScene(area);

        string scenePath = area.path;
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(data, out string guid, out long localId);

        Assert.IsTrue(File.Exists(navAssetPath), $"NavMeshData asset not created: {navAssetPath}");
        Assert.IsTrue(saved, "Scene save returned false");
        Assert.IsTrue(File.ReadAllText(scenePath).Contains(guid), $"Scene file missing NAV reference: {scenePath}");
        Debug.Log($"NAV bake OK: {navAssetPath} (bounds {geometry.center} / {geometry.size})");
    }

    private static GameObject FindRoot(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == name)
            {
                return root;
            }
        }

        return null;
    }

    private static Bounds ComputeGeometryBounds()
    {
        Bounds bounds = new Bounds();
        bool hasBounds = false;
        MeshRenderer[] renderers = Object.FindObjectsByType<MeshRenderer>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        foreach (MeshRenderer renderer in renderers)
        {
            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        Terrain[] terrains = Object.FindObjectsByType<Terrain>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        foreach (Terrain terrain in terrains)
        {
            if (terrain.terrainData == null)
            {
                continue;
            }

            Vector3 size = terrain.terrainData.size;
            Bounds terrainBounds = new(terrain.transform.position + (size * 0.5f), size);
            if (!hasBounds)
            {
                bounds = terrainBounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(terrainBounds);
            }
        }

        return bounds;
    }

    [Test]
    public void BakeA01NavMeshData()
    {
        BakeArea(
            "/Regions/R01_MonasteryOutskirts/SCN_LV01_R01_A01_Base.unity",
            k_Root + "/Regions/R01_MonasteryOutskirts/A01_CliffPath/Navigation/NAV_LV01_R01_A01.asset");
    }

    [Test]
    public void BakeA02NavMeshData()
    {
        BakeArea(
            "/Regions/R01_MonasteryOutskirts/SCN_LV01_R01_A02_Base.unity",
            k_Root + "/Regions/R01_MonasteryOutskirts/A02_Graveyard/Navigation/NAV_LV01_R01_A02.asset");
    }

    [Test]
    public void BakeA03NavMeshData()
    {
        BakeArea(
            "/Regions/R01_MonasteryOutskirts/SCN_LV01_R01_A03_Base.unity",
            k_Root + "/Regions/R01_MonasteryOutskirts/A03_MainGate/Navigation/NAV_LV01_R01_A03.asset");
    }

    [Test]
    public void BakeA04NavMeshData()
    {
        BakeArea(
            "/Regions/R01_MonasteryOutskirts/SCN_LV01_R01_A04_Base.unity",
            k_Root + "/Regions/R01_MonasteryOutskirts/A04_GateTower/Navigation/NAV_LV01_R01_A04.asset");
    }

    [Test]
    public void BakeR02A01NavMeshData()
    {
        BakeArea(
            "/Regions/R02_MonasteryInterior/SCN_LV01_R02_A01_Base.unity",
            k_Root + "/Regions/R02_MonasteryInterior/Navigation/NAV_LV01_R02_A01.asset");
    }

    [Test]
    public void BakeR03A01NavMeshData()
    {
        BakeArea(
            "/Regions/R03_Catacombs/SCN_LV01_R03_A01_Base.unity",
            k_Root + "/Regions/R03_Catacombs/Navigation/NAV_LV01_R03_A01.asset");
    }

    [Test]
    public void BakeR04A01NavMeshData()
    {
        BakeArea(
            "/Regions/R04_BellTower/SCN_LV01_R04_A01_Base.unity",
            k_Root + "/Regions/R04_BellTower/Navigation/NAV_LV01_R04_A01.asset");
    }

    [Test]
    public void BakeR05A01NavMeshData()
    {
        BakeArea(
            "/Regions/R05_BossSanctum/SCN_LV01_R05_A01_Base.unity",
            k_Root + "/Regions/R05_BossSanctum/Navigation/NAV_LV01_R05_A01.asset");
    }
}
