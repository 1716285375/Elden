using UnityEditor;
using UnityEngine;

namespace ZZ.Editor
{
    /// <summary>Builds the isolated EP173-174 portrait prefab and render template.</summary>
    public static class CharacterProfileIconSystemSetup
    {
        private const string k_PlayerPrefabPath =
            "Assets/_Game/Prefabs/Characters/Player/Player.prefab";
        private const string k_PrefabDirectory =
            "Assets/_Game/Resources/UI";
        private const string k_ProfileIconPrefabPath =
            k_PrefabDirectory + "/Profile Icon Maker.prefab";
        private const string k_RenderTexturePath =
            k_PrefabDirectory + "/Icon Render Texture.renderTexture";
        private const int k_ProfileIconLayer = 31;

        [InitializeOnLoadMethod]
        private static void BuildMissingProfileIconAssets()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(
                    k_ProfileIconPrefabPath) != null &&
                AssetDatabase.LoadAssetAtPath<RenderTexture>(
                    k_RenderTexturePath) != null)
            {
                return;
            }

            EditorApplication.delayCall += BuildCharacterProfileIconSystem;
        }

        [MenuItem("Tools/ZZ/UI/Build Character Profile Icon System")]
        public static void BuildCharacterProfileIconSystem()
        {
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                k_PlayerPrefabPath);
            if (playerPrefab == null)
            {
                Debug.LogError($"Missing player prefab: {k_PlayerPrefabPath}");
                return;
            }

            EnsureAssetFolder(k_PrefabDirectory);
            RenderTexture renderTexture = CreateOrUpdateRenderTexture();
            GameObject makerRoot = new("Profile Icon Maker");
            try
            {
                makerRoot.transform.position = new Vector3(0f, -1000f, 0f);
                GameObject dummy = CreateDummy(playerPrefab, makerRoot.transform);
                ProfileIconMakerManager dummyManager =
                    dummy.AddComponent<ProfileIconMakerManager>();
                Camera portraitCamera = CreatePortraitCamera(
                    makerRoot.transform,
                    renderTexture);
                CreatePortraitLight(makerRoot.transform);
                CharacterProfileIconMaker iconMaker =
                    makerRoot.AddComponent<CharacterProfileIconMaker>();
                AssignReferences(
                    iconMaker,
                    dummyManager,
                    portraitCamera,
                    renderTexture);

                PrefabUtility.SaveAsPrefabAsset(
                    makerRoot,
                    k_ProfileIconPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(makerRoot);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"[EP173-174] Profile icon prefab built at " +
                k_ProfileIconPrefabPath);
        }

        private static GameObject CreateDummy(
            GameObject playerPrefab,
            Transform parent)
        {
            GameObject playerSource = (GameObject)PrefabUtility.InstantiatePrefab(
                playerPrefab);
            PrefabUtility.UnpackPrefabInstance(
                playerSource,
                PrefabUnpackMode.Completely,
                InteractionMode.AutomatedAction);
            GameObject dummy = new("Dummy Character");
            dummy.transform.SetParent(parent, false);
            dummy.transform.localPosition = Vector3.zero;
            dummy.transform.localRotation = Quaternion.identity;

            while (playerSource.transform.childCount > 0)
            {
                playerSource.transform.GetChild(0).SetParent(
                    dummy.transform,
                    true);
            }

            Animator sourceAnimator = playerSource.GetComponent<Animator>();
            if (sourceAnimator != null)
            {
                Animator dummyAnimator = dummy.AddComponent<Animator>();
                EditorUtility.CopySerialized(sourceAnimator, dummyAnimator);
            }

            Object.DestroyImmediate(playerSource);

            Component[] components = dummy.GetComponentsInChildren<Component>(
                true);
            for (int componentIndex = components.Length - 1;
                componentIndex >= 0;
                componentIndex--)
            {
                Component component = components[componentIndex];
                if (component == null || ShouldKeepVisualComponent(component))
                {
                    continue;
                }

                Object.DestroyImmediate(component);
            }

            SetLayerRecursively(dummy.transform, k_ProfileIconLayer);
            Animator animator = dummy.GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.applyRootMotion = false;
            }

            dummy.AddComponent<ProfileIconMakerBodyManager>();
            dummy.AddComponent<ProfileIconMakerEquipmentManager>();
            return dummy;
        }

        private static bool ShouldKeepVisualComponent(Component component)
        {
            return component is Transform ||
                component is Animator ||
                component is Renderer ||
                component is MeshFilter ||
                component is LODGroup;
        }

        private static Camera CreatePortraitCamera(
            Transform parent,
            RenderTexture renderTexture)
        {
            GameObject cameraObject = new("Profile Icon Camera");
            cameraObject.transform.SetParent(parent, false);
            cameraObject.transform.localPosition = new Vector3(0f, 1.52f, 2.7f);
            Vector3 target = new(0f, 1.42f, 0f);
            cameraObject.transform.localRotation = Quaternion.LookRotation(
                target - cameraObject.transform.localPosition,
                Vector3.up);

            Camera portraitCamera = cameraObject.AddComponent<Camera>();
            portraitCamera.enabled = false;
            portraitCamera.clearFlags = CameraClearFlags.SolidColor;
            portraitCamera.backgroundColor = new Color(0.025f, 0.02f, 0.02f, 0f);
            portraitCamera.fieldOfView = 28f;
            portraitCamera.nearClipPlane = 0.1f;
            portraitCamera.farClipPlane = 10f;
            portraitCamera.cullingMask = 1 << k_ProfileIconLayer;
            portraitCamera.targetTexture = renderTexture;
            return portraitCamera;
        }

        private static void CreatePortraitLight(Transform parent)
        {
            GameObject lightObject = new("Profile Icon Light");
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.localPosition = new Vector3(0.75f, 1.9f, 1.4f);
            Light portraitLight = lightObject.AddComponent<Light>();
            portraitLight.type = LightType.Point;
            portraitLight.color = new Color(1f, 0.88f, 0.75f);
            portraitLight.intensity = 4f;
            portraitLight.range = 6f;
            portraitLight.shadows = LightShadows.Soft;
            portraitLight.cullingMask = 1 << k_ProfileIconLayer;
        }

        private static RenderTexture CreateOrUpdateRenderTexture()
        {
            RenderTexture renderTexture =
                AssetDatabase.LoadAssetAtPath<RenderTexture>(
                    k_RenderTexturePath);
            if (renderTexture == null)
            {
                renderTexture = new RenderTexture(
                    600,
                    600,
                    24,
                    RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.sRGB)
                {
                    name = "Icon Render Texture"
                };
                AssetDatabase.CreateAsset(renderTexture, k_RenderTexturePath);
            }

            renderTexture.Release();
            renderTexture.width = 600;
            renderTexture.height = 600;
            renderTexture.depth = 24;
            renderTexture.antiAliasing = 4;
            renderTexture.useDynamicScale = true;
            renderTexture.filterMode = FilterMode.Bilinear;
            renderTexture.wrapMode = TextureWrapMode.Clamp;
            EditorUtility.SetDirty(renderTexture);
            return renderTexture;
        }

        private static void AssignReferences(
            CharacterProfileIconMaker iconMaker,
            ProfileIconMakerManager dummyManager,
            Camera portraitCamera,
            RenderTexture renderTexture)
        {
            SerializedObject serializedMaker = new(iconMaker);
            serializedMaker.FindProperty("m_dummyManager").objectReferenceValue =
                dummyManager;
            serializedMaker.FindProperty("m_profileIconCamera").objectReferenceValue =
                portraitCamera;
            serializedMaker.FindProperty("m_iconRenderTextureTemplate")
                .objectReferenceValue = renderTexture;
            serializedMaker.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetLayerRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            foreach (Transform child in root)
            {
                SetLayerRecursively(child, layer);
            }
        }

        private static void EnsureAssetFolder(string folderPath)
        {
            string normalizedPath = folderPath.Replace('\\', '/');
            string currentPath = "Assets";
            string[] segments = normalizedPath.Substring("Assets/".Length)
                .Split('/');
            foreach (string segment in segments)
            {
                string childPath = currentPath + "/" + segment;
                if (!AssetDatabase.IsValidFolder(childPath))
                {
                    AssetDatabase.CreateFolder(currentPath, segment);
                }

                currentPath = childPath;
            }
        }
    }
}
