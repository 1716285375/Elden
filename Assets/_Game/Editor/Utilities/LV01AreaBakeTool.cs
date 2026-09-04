using System.Collections.Generic;
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace ZZ.Editor
{
    /// <summary>
    /// Per-Area authoring tool for the LV01 Area streaming architecture.
    /// Owns scene-set opening, NavMesh baking, lighting settings and validation.
    /// Navigation baking reuses the same flow as LV01AreaNavBaker.
    /// </summary>
    public sealed class LV01AreaBakeTool : EditorWindow
    {
        private const string k_LevelRoot = "Assets/_Game/Scenes/Levels/LV01_AbandonedMonastery";
        private const string k_MasterScene = k_LevelRoot + "/SCN_LV01_AbandonedMonastery.unity";
        private const float k_Margin = 3f;

        private static readonly string[] k_Regions =
        {
            "R01_MonasteryOutskirts",
        };

        private static readonly Dictionary<string, string[]> k_RegionAreas = new Dictionary<string, string[]>
        {
            { "R01_MonasteryOutskirts", new[] { "A01_CliffPath", "A02_Graveyard", "A03_MainGate", "A04_GateTower" } },
        };

        private string m_selectedRegion = k_Regions[0];
        private string m_selectedArea = k_RegionAreas[k_Regions[0]][0];

        [ZZTool("世界与导航", "打开区域烘焙工具", 100)]
        private static void OpenWindow()
        {
            LV01AreaBakeTool window = GetWindow<LV01AreaBakeTool>("LV01 Area Bake");
            window.minSize = new Vector2(360f, 320f);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Level: LV01", EditorStyles.boldLabel);
            m_selectedRegion = k_Regions[EditorGUILayout.Popup("Region", IndexOf(k_Regions, m_selectedRegion), k_Regions)];
            string[] areas = k_RegionAreas[m_selectedRegion];
            m_selectedArea = areas[EditorGUILayout.Popup("Area", IndexOf(areas, m_selectedArea), areas)];

            EditorGUILayout.Space(8f);
            string regionPath = k_LevelRoot + "/Regions/" + m_selectedRegion;
            string areaPath = regionPath + "/" + m_selectedArea;

            if (GUILayout.Button("Open Area For Authoring"))
            {
                OpenAreaForAuthoring(regionPath, m_selectedArea);
            }
            if (GUILayout.Button("Bake Navigation"))
            {
                BakeAreaNavMesh(areaPath);
            }
            if (GUILayout.Button("Bake Lighting"))
            {
                BakeAreaLighting(regionPath, m_selectedArea);
            }
            if (GUILayout.Button("Validate Area"))
            {
                string report = ValidateArea(areaPath, m_selectedArea);
                EditorUtility.DisplayDialog("Area Validation", report, "OK");
            }
            EditorGUILayout.Space(4f);
            if (GUILayout.Button("Bake All R01 Navigation"))
            {
                foreach (string area in k_RegionAreas["R01_MonasteryOutskirts"])
                {
                    BakeAreaNavMesh(k_LevelRoot + "/Regions/R01_MonasteryOutskirts/" + area);
                }
            }
        }

        private static void BakeAreaLighting(string regionPath, string area)
        {
            string[] slices = { "Base", "Props", "Effects", "Spawners" };
            EditorSceneManager.OpenScene(k_MasterScene, OpenSceneMode.Single);
            foreach (string slice in slices)
            {
                string path = ScenePath(regionPath, area, slice);
                if (File.Exists(path))
                {
                    EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                }
            }
            EditorSceneManager.SetActiveScene(SceneManager.GetSceneByPath(ScenePath(regionPath, area, "Base")));

            Lightmapping.Clear();
            Lightmapping.Bake();
            if (Lightmapping.isRunning)
            {
                Debug.LogError("[LV01AreaBakeTool] Bake did not finish");
                return;
            }

            string bakeFolder = regionPath + "/" + AreaFolder(area) + "/Lighting/Baked";
            if (!AssetDatabase.IsValidFolder(bakeFolder))
            {
                AssetDatabase.CreateFolder(regionPath + "/" + AreaFolder(area) + "/Lighting", "Baked");
            }
            int moved = MoveBakeOutputs(regionPath, bakeFolder);
            foreach (Scene scene in GetAllScenes())
            {
                EditorSceneManager.SaveScene(scene);
            }
            Debug.Log($"[LV01AreaBakeTool] Lighting baked for {area}: {moved} files moved to {bakeFolder}");
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

        private static int MoveBakeOutputs(string sourceFolder, string targetFolder)
        {
            List<string> searchFolders = new List<string> { sourceFolder };
            foreach (string sub in Directory.GetDirectories(sourceFolder))
            {
                if (Path.GetFileName(sub).StartsWith("SCN_LV01_R01_"))
                {
                    searchFolders.Add(sub);
                }
            }
            string[] patterns = { "LightingData.asset", "Lightmap-*.exr", "Lightmap-*.png", "ReflectionProbe-*.exr", "ReflectionProbe-*.png" };
            int moved = 0;
            foreach (string folder in searchFolders)
            {
                foreach (string pattern in patterns)
                {
                    foreach (string file in Directory.GetFiles(folder, pattern))
                    {
                        string error = AssetDatabase.MoveAsset(
                            file.Replace('\\', '/'),
                            targetFolder + "/" + Path.GetFileName(file));
                        if (string.IsNullOrEmpty(error))
                        {
                            moved++;
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

        private static int IndexOf(string[] array, string value)
        {
            for (int i = 0; i < array.Length; i++)
            {
                if (array[i] == value)
                {
                    return i;
                }
            }
            return 0;
        }

        private static string ScenePath(string regionPath, string area, string slice)
        {
            string areaName = area.Split('_')[0];
            return regionPath + "/SCN_LV01_R01_" + areaName + "_" + slice + ".unity";
        }

        private static void OpenAreaForAuthoring(string regionPath, string area)
        {
            EditorSceneManager.OpenScene(k_MasterScene, OpenSceneMode.Single);
            string[] slices = { "Base", "Props", "Effects", "Spawners" };
            foreach (string slice in slices)
            {
                string path = ScenePath(regionPath, area, slice);
                if (File.Exists(path))
                {
                    EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                }
                else
                {
                    Debug.LogWarning($"[LV01AreaBakeTool] Missing slice: {path}");
                }
            }
            EditorSceneManager.SetActiveScene(SceneManager.GetSceneByPath(ScenePath(regionPath, area, "Base")));
        }

        private static void BakeAreaNavMesh(string areaPath)
        {
            string regionPath = Directory.GetParent(areaPath).FullName.Replace('\\', '/');
            string areaName = areaPath.Substring(areaPath.LastIndexOf('/') + 1).Split('_')[0];
            string baseScene = regionPath + "/SCN_LV01_R01_" + areaName + "_Base.unity";
            string navAssetPath = areaPath + "/Navigation/NAV_LV01_R01_" + areaName + ".asset";

            EditorSceneManager.OpenScene(baseScene, OpenSceneMode.Single);

            GameObject navRoot = GameObject.Find("_Navigation");
            if (navRoot == null)
            {
                navRoot = new GameObject("_Navigation");
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
            surface.center = geometry.center;
            surface.size = geometry.size + new Vector3(k_Margin, k_Margin, k_Margin);

            surface.BuildNavMesh();
            NavMeshData built = surface.navMeshData;
            if (built == null)
            {
                Debug.LogError($"[LV01AreaBakeTool] Bake produced no data for {areaName}");
                return;
            }
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
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Debug.Log($"[LV01AreaBakeTool] NavMesh baked: {navAssetPath}");
        }

        private static Bounds ComputeGeometryBounds()
        {
            Bounds bounds = new Bounds();
            bool hasBounds = false;
            MeshRenderer[] renderers = Object.FindObjectsOfType<MeshRenderer>();
            foreach (MeshRenderer renderer in renderers)
            {
                if (!renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }
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
            return bounds;
        }

        private static readonly string[] k_LinkOwnerAreas = { "A01", "A02", "A04" };

        private static string ValidateArea(string areaPath, string areaName)
        {
            List<string> issues = new List<string>();
            string regionPath = Directory.GetParent(areaPath).FullName.Replace('\\', '/');
            string baseScene = regionPath + "/SCN_LV01_R01_" + areaName + "_Base.unity";
            string navAssetPath = areaPath + "/Navigation/NAV_LV01_R01_" + areaName + ".asset";

            foreach (string slice in new[] { "Base", "Props", "Effects", "Spawners" })
            {
                if (!File.Exists(ScenePath(regionPath, areaName, slice)))
                {
                    issues.Add($"Missing slice scene: {slice}");
                }
            }
            if (!File.Exists(navAssetPath))
            {
                issues.Add("Missing NavMeshData asset");
            }

            EditorSceneManager.OpenScene(baseScene, OpenSceneMode.Single);
            GameObject navRoot = GameObject.Find("_Navigation");
            if (navRoot == null)
            {
                issues.Add("Missing _Navigation root");
            }
            else
            {
                NavMeshSurface surface = navRoot.GetComponent<NavMeshSurface>();
                if (surface == null)
                {
                    issues.Add("Missing NavMeshSurface");
                }
                else if (surface.navMeshData == null)
                {
                    issues.Add("NavMeshSurface has no NavMeshData assigned");
                }
                else if (surface.size.sqrMagnitude <= 0f)
                {
                    issues.Add("NavMeshSurface volume size is invalid");
                }
            }
            if (IndexOf(k_LinkOwnerAreas, areaName) >= 0 && GameObject.Find("_Navigation/Links") == null)
            {
                issues.Add("Missing _Navigation/Links container");
            }

            string lightingBaked = areaPath + "/Lighting/Baked";
            if (!File.Exists(lightingBaked + "/LightingData.asset"))
            {
                issues.Add("Missing baked LightingData.asset");
            }
            if (Directory.GetFiles(lightingBaked, "Lightmap-*").Length == 0)
            {
                issues.Add("Missing lightmaps");
            }

            LightingSettings unified = AssetDatabase.LoadAssetAtPath<LightingSettings>(
                k_LevelRoot + "/Shared/Lighting/Settings/LGT_LV01_LightingSettings.asset");
            if (unified == null)
            {
                issues.Add("Missing unified LightingSettings asset");
            }
            else if (Lightmapping.lightingSettings != unified)
            {
                issues.Add("Scene does not use the unified LightingSettings");
            }

            string spawnerScene = ScenePath(regionPath, areaName, "Spawners");
            if (File.Exists(spawnerScene))
            {
                EditorSceneManager.OpenScene(spawnerScene, OpenSceneMode.Single);
                foreach (MeshRenderer renderer in Object.FindObjectsOfType<MeshRenderer>())
                {
                    if (renderer.gameObject.activeInHierarchy
                        && (GameObjectUtility.GetStaticEditorFlags(renderer.gameObject) & StaticEditorFlags.ContributeGI) != 0)
                    {
                        issues.Add($"Spawner object contributes GI: {renderer.name}");
                        break;
                    }
                }
            }

            EditorSceneManager.OpenScene(k_MasterScene, OpenSceneMode.Single);
            int masterGeometry = 0;
            foreach (MeshRenderer renderer in Object.FindObjectsOfType<MeshRenderer>())
            {
                if (renderer.gameObject.activeInHierarchy)
                {
                    masterGeometry++;
                }
            }
            if (masterGeometry > 0)
            {
                issues.Add($"Master scene contains {masterGeometry} geometry renderers");
            }

            if (issues.Count == 0)
            {
                return $"Validation PASSED for {areaName}";
            }
            return "Validation FAILED:\n- " + string.Join("\n- ", issues.ToArray());
        }
    }
}
