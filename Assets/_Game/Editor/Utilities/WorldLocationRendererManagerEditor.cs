using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ZZ.Editor
{
    /// <summary>Provides one-click maintenance for a Scene Renderer Manager.</summary>
    [CustomEditor(typeof(WorldLocationRendererManager))]
    public sealed class WorldLocationRendererManagerEditor : UnityEditor.Editor
    {
        /// <inheritdoc />
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            WorldLocationRendererManager rendererManager =
                (WorldLocationRendererManager)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                "Scene Presentation",
                EditorStyles.boldLabel);
            if (GUILayout.Button("Enable All Renderers"))
            {
                RefreshAndToggleRenderers(rendererManager, true);
            }

            if (GUILayout.Button("Disable All Renderers"))
            {
                RefreshAndToggleRenderers(rendererManager, false);
            }

            if (GUILayout.Button("Enable All Root Objects"))
            {
                RefreshAndToggleRoots(rendererManager, true);
            }

            using (new EditorGUI.DisabledScope(
                !rendererManager.ManageRootObjects))
            {
                if (GUILayout.Button("Disable All Root Objects"))
                {
                    RefreshAndToggleRoots(rendererManager, false);
                }
            }
        }

        private static void RefreshAndToggleRenderers(
            WorldLocationRendererManager rendererManager,
            bool status)
        {
            Undo.RecordObject(rendererManager, "Refresh Scene Renderers");
            rendererManager.RefreshSceneObjects();
            foreach (MeshRenderer meshRenderer in rendererManager.MeshRenderers)
            {
                if (meshRenderer != null)
                {
                    Undo.RecordObject(meshRenderer, "Toggle Scene Renderer");
                }
            }

            rendererManager.ToggleAllMeshRenderers(status);
            MarkManagerSceneDirty(rendererManager);
        }

        private static void RefreshAndToggleRoots(
            WorldLocationRendererManager rendererManager,
            bool status)
        {
            Undo.RecordObject(rendererManager, "Refresh Scene Roots");
            rendererManager.RefreshSceneObjects();
            foreach (GameObject rootObject in rendererManager.RootObjects)
            {
                if (rootObject != null)
                {
                    Undo.RecordObject(rootObject, "Toggle Scene Root");
                }
            }

            rendererManager.ToggleAllRootObjects(status);
            MarkManagerSceneDirty(rendererManager);
        }

        private static void MarkManagerSceneDirty(
            WorldLocationRendererManager rendererManager)
        {
            EditorUtility.SetDirty(rendererManager);
            if (rendererManager.gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(
                    rendererManager.gameObject.scene);
            }
        }
    }

    /// <summary>Exposes the world editing and runtime preparation modes.</summary>
    [CustomEditor(typeof(WorldLocationManager))]
    public sealed class WorldLocationManagerEditor : UnityEditor.Editor
    {
        /// <inheritdoc />
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            WorldLocationManager locationManager =
                (WorldLocationManager)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("World Editing Mode", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Open the required additive Scenes before applying a mode. " +
                "Each Scene Manager is rescanned and marked dirty.",
                MessageType.Info);
            if (GUILayout.Button("Game Mode"))
            {
                locationManager.EnableGameMode();
            }

            if (GUILayout.Button("Light Bake Mode"))
            {
                locationManager.EnableLightBakeMode();
            }
        }
    }
}
