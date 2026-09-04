using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZZ
{
    /// <summary>
    /// Builds <c>SCN_Test_LV01_GreyboxTraversal</c>, the Scene used to walk the LV01
    /// blockout with the real player, camera, and movement.
    /// </summary>
    /// <remarks>
    /// The Scene deliberately holds no level geometry. It contributes the spawn
    /// override and the debug tooling, and the Region slices are opened alongside
    /// it so the normal streaming system stays in charge of the world.
    /// </remarks>
    public static class LV01GreyboxTestSceneSetup
    {
        private const string k_TestScenePath =
            "Assets/_Game/Scenes/Levels/LV01_AbandonedMonastery/Dev/Test/" +
            "SCN_Test_LV01_GreyboxTraversal.unity";

        private static readonly (int From, string FromArea, int To, string ToArea)[]
            s_route =
            {
                (LV01GreyboxSpec.RegionOutskirts, LV01GreyboxSpec.CliffPath,
                    LV01GreyboxSpec.RegionOutskirts, LV01GreyboxSpec.Graveyard),
                (LV01GreyboxSpec.RegionOutskirts, LV01GreyboxSpec.Graveyard,
                    LV01GreyboxSpec.RegionOutskirts, LV01GreyboxSpec.MainGate),
                (LV01GreyboxSpec.RegionOutskirts, LV01GreyboxSpec.Graveyard,
                    LV01GreyboxSpec.RegionOutskirts, LV01GreyboxSpec.GateTower),
                (LV01GreyboxSpec.RegionOutskirts, LV01GreyboxSpec.GateTower,
                    LV01GreyboxSpec.RegionOutskirts, LV01GreyboxSpec.MainGate),
                (LV01GreyboxSpec.RegionOutskirts, LV01GreyboxSpec.MainGate,
                    LV01GreyboxSpec.RegionInterior, LV01GreyboxSpec.EntranceHall),
                (LV01GreyboxSpec.RegionInterior, LV01GreyboxSpec.EntranceHall,
                    LV01GreyboxSpec.RegionInterior, LV01GreyboxSpec.Cloister)
            };

        [ZZTool("关卡设计", "构建灰盒测试场景", 60)]
        public static void BuildTestScene()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPaused)
            {
                Debug.LogError("[LV01Greybox] Exit Play Mode before building the test Scene.");
                return;
            }

            LV01GreyboxLayout layout = AssetDatabase.LoadAssetAtPath<LV01GreyboxLayout>(
                "Assets/_Game/Data/LevelDesign/LV01_GreyboxLayout.asset");
            if (layout == null)
            {
                Debug.LogError(
                    "[LV01Greybox] 请先在 ZZ 工具面板中执行“从规范重建布局资源”。");
                return;
            }

            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene, NewSceneMode.Additive);

            CreateTestBootstrap(scene);
            CreatePlayerSpawn(scene);
            CreateDebugUi(scene, layout);
            CreateDebugGizmos(scene, layout);
            CreateLighting(scene);

            bool saved = EditorSceneManager.SaveScene(scene, k_TestScenePath);
            if (!saved)
            {
                Debug.LogError($"[LV01Greybox] Failed to save the test Scene to {k_TestScenePath}.");
                return;
            }

            Debug.Log($"[LV01Greybox] Test Scene written to {k_TestScenePath}.");
        }

        /// <summary>
        /// Opens the master Scene, the test Scene, and every R01/R02 slice so the
        /// blockout can be walked straight away.
        /// </summary>
        [ZZTool("关卡设计", "打开灰盒试玩场景", 70)]
        public static void OpenPlaytestScenes()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPaused)
            {
                Debug.LogError("[LV01Greybox] Exit Play Mode before opening the playtest Scenes.");
                return;
            }

            OpenAdditive(WorldScenePathLayout.MasterScenePath);
            OpenAdditive(k_TestScenePath);

            // R01 and R02 only: R03 to R05 stay unbuilt for this pass.
            for (int region = 0; region < 2; region++)
            {
                for (int area = 0; area < WorldScenePathLayout.GetAreaCount(region); area++)
                {
                    for (int slice = 0; slice < 4; slice++)
                    {
                        OpenAdditive(WorldScenePathLayout.GetScenePath(region, area, slice));
                    }
                }
            }

            Debug.Log(
                "[LV01Greybox] Playtest Scenes open. Press Play, start a host from the main " +
                "menu if needed, and the bootstrap will place you at the greybox start.");
        }

        private static void OpenAdditive(string path)
        {
            if (!System.IO.File.Exists(path))
            {
                Debug.LogWarning($"[LV01Greybox] Skipping missing Scene '{path}'.");
                return;
            }

            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                if (EditorSceneManager.GetSceneAt(i).path == path)
                {
                    return;
                }
            }

            EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        }

        private static void CreateTestBootstrap(Scene scene)
        {
            GameObject bootstrap = new("TestBootstrap");
            SceneManager.MoveGameObjectToScene(bootstrap, scene);
            bootstrap.AddComponent<GreyboxTestBootstrap>();
        }

        private static void CreatePlayerSpawn(Scene scene)
        {
            GameObject spawnRoot = new("PlayerSpawn");
            SceneManager.MoveGameObjectToScene(spawnRoot, scene);
            spawnRoot.transform.position = LV01GreyboxSpec.PlayerSpawn;

            GameObject spawnPoint = new("Player Spawn Point");
            spawnPoint.transform.SetParent(spawnRoot.transform, false);
            spawnPoint.transform.position = LV01GreyboxSpec.PlayerSpawn;
            spawnPoint.transform.rotation = Quaternion.identity;
        }

        private static void CreateDebugUi(Scene scene, LV01GreyboxLayout layout)
        {
            GameObject debugUi = new("Debug UI");
            SceneManager.MoveGameObjectToScene(debugUi, scene);
            GreyboxDebugHud hud = debugUi.AddComponent<GreyboxDebugHud>();
            SerializedObject serialized = new(hud);
            serialized.FindProperty("m_layout").objectReferenceValue = layout;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateDebugGizmos(Scene scene, LV01GreyboxLayout layout)
        {
            GameObject gizmosRoot = new("Debug Gizmos");
            SceneManager.MoveGameObjectToScene(gizmosRoot, scene);
            GreyboxDebugGizmos gizmos = gizmosRoot.AddComponent<GreyboxDebugGizmos>();

            SerializedObject serialized = new(gizmos);
            serialized.FindProperty("m_layout").objectReferenceValue = layout;

            SerializedProperty links = serialized.FindProperty("m_areaLinks");
            links.arraySize = s_route.Length;
            for (int i = 0; i < s_route.Length; i++)
            {
                SerializedProperty element = links.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("m_fromRegionIndex").intValue = s_route[i].From;
                element.FindPropertyRelative("m_fromArea").stringValue = s_route[i].FromArea;
                element.FindPropertyRelative("m_toRegionIndex").intValue = s_route[i].To;
                element.FindPropertyRelative("m_toArea").stringValue = s_route[i].ToArea;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateLighting(Scene scene)
        {
            GameObject lighting = new("Lighting");
            SceneManager.MoveGameObjectToScene(lighting, scene);

            GameObject sun = new("Directional Light");
            sun.transform.SetParent(lighting.transform, false);
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            Light light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.6f;
            light.color = new Color(1f, 0.96f, 0.88f);

            // Flat ambient keeps greybox faces readable without a lighting pass.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.38f, 0.4f, 0.45f);
            RenderSettings.fog = false;
        }
    }
}
