using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.SceneManagement;

namespace ZZ
{
    /// <summary>
    /// Generates the LV01 greybox from a <see cref="LV01GreyboxLayout"/> asset into
    /// the existing Region x Slice streaming scenes.
    /// </summary>
    /// <remarks>
    /// This tool only ever deletes GameObjects whose name starts with
    /// <c>PB_</c> under an Area root it created, so hand-authored content in the
    /// slice scenes is never at risk. Regenerating is the recovery path for a bad
    /// layout edit, and no confirmation prompt is needed for that reason.
    /// </remarks>
    public sealed class LV01GreyboxGenerator : EditorWindow
    {
        private const string k_LayoutAssetPath =
            "Assets/_Game/Data/LevelDesign/LV01_GreyboxLayout.asset";
        private const string k_GeneratedPrefix = "PB_";

        private static readonly string[] s_sliceNames =
        {
            "Base", "Props", "Effects", "Spawners"
        };

        [SerializeField] private LV01GreyboxLayout m_layout;

        [ZZTool("关卡设计", "打开灰盒生成器", 10)]
        public static void OpenWindow()
        {
            LV01GreyboxGenerator window = GetWindow<LV01GreyboxGenerator>("LV01 Greybox");
            window.m_layout = EnsureLayoutAsset();
        }

        [ZZTool("关卡设计", "从规范重建布局资源", 20)]
        public static void RebuildLayoutAsset()
        {
            LV01GreyboxLayout layout = EnsureLayoutAsset();
            layout.SetPlayerMetrics(
                LV01GreyboxSpec.PlayerHeight,
                LV01GreyboxSpec.PlayerRadius,
                LV01GreyboxSpec.CameraPivotHeight,
                LV01GreyboxSpec.CameraDistance);
            layout.SetBoxes(LV01GreyboxSpec.Build());

            EditorUtility.SetDirty(layout);
            AssetDatabase.SaveAssets();
            Debug.Log(
                $"[LV01Greybox] Layout rebuilt: {layout.Boxes.Count} boxes at scale " +
                $"{layout.ScaleFactor:0.####}.");
        }

        [ZZTool("关卡设计", "生成灰盒几何", 30)]
        public static void GenerateGeometry()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPaused)
            {
                Debug.LogError("[LV01Greybox] Exit Play Mode before generating geometry.");
                return;
            }

            LV01GreyboxLayout layout = EnsureLayoutAsset();
            if (layout.Boxes.Count == 0)
            {
                Debug.LogWarning(
                    "[LV01Greybox] The layout is empty. Run 'Build Layout From Spec' first.");
                return;
            }

            int created = 0;
            int removed = 0;
            foreach (KeyValuePair<SceneKey, Dictionary<string, List<GreyboxBox>>> sceneGroup in
                     GroupBoxesByScene(layout))
            {
                string path = WorldScenePathLayout.GetScenePath(
                    sceneGroup.Key.RegionIndex,
                    sceneGroup.Key.AreaIndex,
                    sceneGroup.Key.SliceIndex);
                if (!System.IO.File.Exists(path))
                {
                    Debug.LogError($"[LV01Greybox] Missing slice scene '{path}'.");
                    continue;
                }

                bool wasOpen = IsSceneOpen(path);
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                foreach (KeyValuePair<string, List<GreyboxBox>> areaGroup in sceneGroup.Value)
                {
                    GameObject areaRoot = FindOrCreateAreaRoot(scene, areaGroup.Key);
                    removed += DestroyGeneratedChildren(areaRoot);
                    foreach (GreyboxBox box in areaGroup.Value)
                    {
                        GameObject generated = CreateBox(box);
                        generated.transform.SetParent(areaRoot.transform, true);
                        created++;
                    }
                }

                RefreshRendererManagers(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[LV01Greybox] Wrote {scene.name}.");

                if (!wasOpen)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log(
                $"[LV01Greybox] Generated {created} volumes, replaced {removed} previous ones.");
        }

        [ZZTool("关卡设计", "删除灰盒几何", 40, "将删除当前灰盒生成内容。是否继续？")]
        public static void DeleteGeneratedGeometry()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPaused)
            {
                Debug.LogError("[LV01Greybox] Exit Play Mode before deleting geometry.");
                return;
            }

            int removed = 0;
            for (int region = 0; region < WorldScenePathLayout.RegionCount; region++)
            {
                for (int area = 0; area < WorldScenePathLayout.GetAreaCount(region); area++)
                {
                    for (int slice = 0; slice < s_sliceNames.Length; slice++)
                    {
                        string path = WorldScenePathLayout.GetScenePath(region, area, slice);
                        if (!System.IO.File.Exists(path))
                        {
                            continue;
                        }

                        bool wasOpen = IsSceneOpen(path);
                        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                        bool dirty = false;
                        foreach (GameObject root in scene.GetRootGameObjects())
                        {
                            int count = DestroyGeneratedChildren(root);
                            removed += count;
                            dirty |= count > 0;
                        }

                        if (dirty)
                        {
                            RefreshRendererManagers(scene);
                            EditorSceneManager.SaveScene(scene);
                        }

                        if (!wasOpen)
                        {
                            EditorSceneManager.CloseScene(scene, true);
                        }
                    }
                }
            }

            Debug.Log($"[LV01Greybox] Deleted {removed} generated volumes.");
        }

        private void OnGUI()
        {
            m_layout = EditorGUILayout.ObjectField(
                "Layout", m_layout, typeof(LV01GreyboxLayout), false) as LV01GreyboxLayout;

            if (m_layout == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign or build the LV01 greybox layout asset.",
                    MessageType.Info);
                if (GUILayout.Button("Build Layout From Spec"))
                {
                    RebuildLayoutAsset();
                    m_layout = EnsureLayoutAsset();
                }

                return;
            }

            EditorGUILayout.HelpBox(
                $"Player {m_layout.PlayerHeight:0.##} m / " +
                $"reference {m_layout.ReferenceHeight:0.##} m -> " +
                $"scale {m_layout.ScaleFactor:0.####}\n" +
                $"Boxes: {m_layout.Boxes.Count}",
                MessageType.None);

            EditorGUILayout.Space();
            if (GUILayout.Button("Build Layout From Spec"))
            {
                RebuildLayoutAsset();
            }

            if (GUILayout.Button("Generate Geometry"))
            {
                GenerateGeometry();
            }

            if (GUILayout.Button("Delete Generated Geometry"))
            {
                DeleteGeneratedGeometry();
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Editing a box in the layout asset and pressing Generate Geometry is the " +
                "intended iteration loop. Only objects named PB_* are ever replaced.",
                MessageType.Info);
        }

        // ---- Layout asset ----------------------------------------------------

        private static LV01GreyboxLayout EnsureLayoutAsset()
        {
            LV01GreyboxLayout layout =
                AssetDatabase.LoadAssetAtPath<LV01GreyboxLayout>(k_LayoutAssetPath);
            if (layout != null)
            {
                return layout;
            }

            EnsureFolderExists("Assets/_Game/Data/LevelDesign");
            layout = CreateInstance<LV01GreyboxLayout>();
            AssetDatabase.CreateAsset(layout, k_LayoutAssetPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[LV01Greybox] Created layout asset at {k_LayoutAssetPath}.");
            return layout;
        }

        // ---- Grouping --------------------------------------------------------

        private static Dictionary<SceneKey, Dictionary<string, List<GreyboxBox>>> GroupBoxesByScene(
            LV01GreyboxLayout layout)
        {
            Dictionary<SceneKey, Dictionary<string, List<GreyboxBox>>> byScene = new();
            foreach (GreyboxBox box in layout.Boxes)
            {
                int sliceIndex = SliceIndex(box.Slice);
                if (sliceIndex < 0)
                {
                    Debug.LogError(
                        $"[LV01Greybox] '{box.ObjectName}' names unknown slice '{box.Slice}'.");
                    continue;
                }

                int areaIndex = AreaIndex(box.Area);
                if (areaIndex < 0)
                {
                    Debug.LogError(
                        $"[LV01Greybox] '{box.ObjectName}' names unknown area '{box.Area}'.");
                    continue;
                }

                SceneKey key = new(box.RegionIndex, areaIndex, sliceIndex);
                if (!byScene.TryGetValue(key, out Dictionary<string, List<GreyboxBox>> areas))
                {
                    areas = new Dictionary<string, List<GreyboxBox>>();
                    byScene.Add(key, areas);
                }

                if (!areas.TryGetValue(box.Area, out List<GreyboxBox> boxes))
                {
                    boxes = new List<GreyboxBox>();
                    areas.Add(box.Area, boxes);
                }

                boxes.Add(box);
            }

            return byScene;
        }

        private static int SliceIndex(string slice)
        {
            for (int i = 0; i < s_sliceNames.Length; i++)
            {
                if (s_sliceNames[i] == slice)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Derives the zero-based Area index from an authored Area name such as
        /// "A01_CliffPath" or "A02_Graveyard".
        /// </summary>
        private static int AreaIndex(string area)
        {
            if (area.Length < 3 || area[0] != 'A')
            {
                return -1;
            }

            return int.TryParse(area.Substring(1, 2), out int areaNumber)
                ? areaNumber - 1
                : -1;
        }

        // ---- Scene authoring -------------------------------------------------

        private static GameObject FindOrCreateAreaRoot(Scene scene, string areaName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == areaName)
                {
                    return root;
                }
            }

            GameObject areaRoot = new(areaName);
            SceneManager.MoveGameObjectToScene(areaRoot, scene);
            return areaRoot;
        }

        private static int DestroyGeneratedChildren(GameObject parent)
        {
            int removed = 0;
            for (int i = parent.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.transform.GetChild(i);
                if (!child.name.StartsWith(k_GeneratedPrefix))
                {
                    continue;
                }

                DestroyImmediate(child.gameObject);
                removed++;
            }

            return removed;
        }

        private static GameObject CreateBox(GreyboxBox box)
        {
            ProBuilderMesh mesh = ShapeGenerator.GenerateCube(PivotLocation.Center, box.Size);
            mesh.gameObject.name = box.ObjectName;
            mesh.transform.SetPositionAndRotation(
                box.Position, Quaternion.Euler(box.Rotation));

            mesh.ToMesh();
            mesh.Refresh();

            Material material = LV01GreyboxMaterials.Get(box.Role);
            mesh.SetMaterial(mesh.faces, material);
            mesh.ToMesh();
            mesh.Refresh();

            MeshRenderer renderer = mesh.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterials = new[] { material };
            }

            MeshCollider collider = mesh.GetComponent<MeshCollider>();
            if (collider == null)
            {
                collider = mesh.gameObject.AddComponent<MeshCollider>();
            }

            MeshFilter filter = mesh.GetComponent<MeshFilter>();
            collider.sharedMesh = filter != null ? filter.sharedMesh : null;
            collider.isTrigger = box.Role == GreyboxRole.Trigger;
            return mesh.gameObject;
        }

        /// <summary>
        /// Keeps the streaming visibility system in step with freshly generated
        /// content, so newly created renderers are actually toggled at runtime.
        /// </summary>
        private static void RefreshRendererManagers(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                WorldLocationRendererManager manager =
                    root.GetComponentInChildren<WorldLocationRendererManager>(true);
                manager?.RefreshSceneObjects();
            }
        }

        private static bool IsSceneOpen(string path)
        {
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                if (EditorSceneManager.GetSceneAt(i).path == path)
                {
                    return true;
                }
            }

            return false;
        }

        private static void EnsureFolderExists(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private readonly struct SceneKey
        {
            public SceneKey(int regionIndex, int areaIndex, int sliceIndex)
            {
                RegionIndex = regionIndex;
                AreaIndex = areaIndex;
                SliceIndex = sliceIndex;
            }

            public int RegionIndex { get; }

            public int AreaIndex { get; }

            public int SliceIndex { get; }
        }
    }
}
