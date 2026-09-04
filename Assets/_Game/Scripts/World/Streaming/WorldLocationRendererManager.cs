using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace ZZ
{
    /// <summary>
    /// Controls only the visual presentation of one additive Scene while leaving
    /// its colliders and gameplay behaviours loaded.
    /// </summary>
    [DefaultExecutionOrder(-8300)]
    [DisallowMultipleComponent]
    public class WorldLocationRendererManager : MonoBehaviour
    {
        [SerializeField] private int m_rendererSceneID = -1;
        [SerializeField] private bool m_manageRootObjects = true;
        [SerializeField] private List<GameObject> m_rootObjects = new();
        [SerializeField] private List<MeshRenderer> m_meshRenderers = new();

        private Coroutine m_rootObjectsCoroutine;

        /// <summary>Gets the Build Settings index of the managed Scene.</summary>
        public int RendererSceneID => m_rendererSceneID;

        /// <summary>Gets whether this Scene permits staged Root activation.</summary>
        public bool ManageRootObjects => m_manageRootObjects;

        /// <summary>Gets the cached Scene roots excluding this Manager.</summary>
        public IReadOnlyList<GameObject> RootObjects => m_rootObjects;

        /// <summary>Gets the cached MeshRenderers owned by this Scene.</summary>
        public IReadOnlyList<MeshRenderer> MeshRenderers => m_meshRenderers;

        private void Awake()
        {
            UpdateRendererSceneID();
        }

        private void OnEnable()
        {
            WorldLocationManager.Instance?.RegisterRendererManager(this);
        }

        private IEnumerator Start()
        {
            yield return null;
            WorldLocationManager.Instance?.RegisterRendererManager(this);
            if (Application.isPlaying)
            {
                EnableRootObjectsForRuntime();
                WorldSceneManager.Instance?.CheckForRequiredRenderers();
            }
        }

        private void OnDisable()
        {
            StopRootObjectCoroutine();
            WorldLocationManager.Instance?.UnregisterRendererManager(this);
        }

        /// <summary>Updates the Build Index and Root/Renderer reference caches.</summary>
        public void RefreshSceneObjects()
        {
            Scene managedScene = gameObject.scene;
            if (!managedScene.IsValid())
            {
                return;
            }

            UpdateRendererSceneID();
            m_rootObjects.Clear();
            GameObject managerRoot = transform.root.gameObject;
            foreach (GameObject rootObject in managedScene.GetRootGameObjects())
            {
                if (rootObject != null && rootObject != managerRoot &&
                    !m_rootObjects.Contains(rootObject))
                {
                    m_rootObjects.Add(rootObject);
                }
            }

            m_meshRenderers.Clear();
            MeshRenderer[] renderers = Object.FindObjectsByType<MeshRenderer>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            foreach (MeshRenderer meshRenderer in renderers)
            {
                if (meshRenderer != null &&
                    meshRenderer.gameObject.scene == managedScene &&
                    !m_meshRenderers.Contains(meshRenderer))
                {
                    m_meshRenderers.Add(meshRenderer);
                }
            }

            MarkEditorDirty(this);
        }

        /// <summary>Configures this Manager for one generated Scene slice.</summary>
        public void ConfigureScene(int rendererSceneID, bool manageRootObjects)
        {
            m_rendererSceneID = rendererSceneID;
            m_manageRootObjects = manageRootObjects;
            MarkEditorDirty(this);
        }

        /// <summary>
        /// Disables visual content and, where safe, Scene roots before Play Mode.
        /// </summary>
        public void PrepareForGameMode()
        {
            RefreshSceneObjects();
            ToggleAllMeshRenderers(false);
            ToggleAllRootObjects(false);
        }

        /// <summary>Enables all authored content for editing and light baking.</summary>
        public void PrepareForLightBakeMode()
        {
            RefreshSceneObjects();
            ToggleAllRootObjects(true);
            ToggleAllMeshRenderers(true);
        }

        /// <summary>Enables or disables every cached MeshRenderer immediately.</summary>
        public void ToggleAllMeshRenderers(bool status)
        {
            RemoveMissingReferences();
            foreach (MeshRenderer meshRenderer in m_meshRenderers)
            {
                if (meshRenderer == null)
                {
                    continue;
                }

                meshRenderer.enabled = status;
                MarkEditorDirty(meshRenderer);
            }

            MarkEditorDirty(this);
        }

        /// <summary>
        /// Enables or disables cached Roots. Spawner Scenes reject Root disabling
        /// so scene-authored NetworkObjects remain active when the Scene loads.
        /// </summary>
        public void ToggleAllRootObjects(bool status)
        {
            StopRootObjectCoroutine();
            if (!status && !m_manageRootObjects)
            {
                return;
            }

            RemoveMissingReferences();
            foreach (GameObject rootObject in m_rootObjects)
            {
                if (rootObject == null)
                {
                    continue;
                }

                rootObject.SetActive(status);
                MarkEditorDirty(rootObject);
            }

            MarkEditorDirty(this);
        }

        /// <summary>Starts safe immediate or frame-staged Root activation.</summary>
        public void EnableRootObjectsForRuntime()
        {
            if (!Application.isPlaying || !m_manageRootObjects)
            {
                return;
            }

            RemoveMissingReferences();
            bool requiresActivation = false;
            foreach (GameObject rootObject in m_rootObjects)
            {
                if (rootObject != null && !rootObject.activeSelf)
                {
                    requiresActivation = true;
                    break;
                }
            }

            if (!requiresActivation)
            {
                return;
            }

            if (LoadingScreenIsActive())
            {
                StopRootObjectCoroutine();
                ToggleAllRootObjects(true);
                return;
            }

            if (m_rootObjectsCoroutine == null)
            {
                m_rootObjectsCoroutine = StartCoroutine(
                    ToggleAllRootObjectsOverTime(true));
            }
        }

        /// <summary>Toggles one Root per frame to distribute initialization cost.</summary>
        public IEnumerator ToggleAllRootObjectsOverTime(bool status)
        {
            if (!status && !m_manageRootObjects)
            {
                yield break;
            }

            RemoveMissingReferences();
            foreach (GameObject rootObject in m_rootObjects)
            {
                if (rootObject == null)
                {
                    continue;
                }

                rootObject.SetActive(status);
                yield return null;
            }

            m_rootObjectsCoroutine = null;
        }

        /// <summary>
        /// Provides an optional frame-staged Renderer path for profiler-driven use.
        /// </summary>
        public IEnumerator ToggleAllMeshRenderersOverTime(bool status)
        {
            RemoveMissingReferences();
            foreach (MeshRenderer meshRenderer in m_meshRenderers)
            {
                if (meshRenderer == null)
                {
                    continue;
                }

                meshRenderer.enabled = status;
                yield return null;
            }
        }

        private void UpdateRendererSceneID()
        {
            Scene managedScene = gameObject.scene;
            if (managedScene.IsValid() && managedScene.buildIndex >= 0)
            {
                m_rendererSceneID = managedScene.buildIndex;
            }
        }

        private void RemoveMissingReferences()
        {
            m_rootObjects.RemoveAll(rootObject => rootObject == null);
            m_meshRenderers.RemoveAll(meshRenderer => meshRenderer == null);
        }

        private void StopRootObjectCoroutine()
        {
            if (m_rootObjectsCoroutine == null)
            {
                return;
            }

            StopCoroutine(m_rootObjectsCoroutine);
            m_rootObjectsCoroutine = null;
        }

        private static bool LoadingScreenIsActive()
        {
            return PlayerUIManager.Instance?.PlayerUILoadingScreenManager
                ?.IsLoadingScreenActive == true;
        }

        private static void MarkEditorDirty(Object target)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying && target != null)
            {
                EditorUtility.SetDirty(target);
                if (target is Component component &&
                    component.gameObject.scene.IsValid())
                {
                    EditorSceneManager.MarkSceneDirty(
                        component.gameObject.scene);
                }
                else if (target is GameObject gameObject &&
                    gameObject.scene.IsValid())
                {
                    EditorSceneManager.MarkSceneDirty(gameObject.scene);
                }
            }
#endif
        }
    }
}
