using System.Collections.Generic;
using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Maintains the local peer's Renderer Managers independently from the
    /// server-authoritative Scene loading decision system.
    /// </summary>
    [DefaultExecutionOrder(-8350)]
    [DisallowMultipleComponent]
    public class WorldLocationManager : MonoBehaviour
    {
        private static WorldLocationManager s_instance;

        private readonly List<WorldLocationRendererManager>
            m_worldLocationRenderers = new();

        /// <summary>Gets the local world presentation manager.</summary>
        public static WorldLocationManager Instance => s_instance;

        /// <summary>Gets Renderer Managers registered by loaded additive Scenes.</summary>
        public IReadOnlyList<WorldLocationRendererManager>
            WorldLocationRenderers => m_worldLocationRenderers;

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                Destroy(this);
                return;
            }

            s_instance = this;
        }

        private void OnDestroy()
        {
            if (s_instance == this)
            {
                s_instance = null;
            }
        }

        /// <summary>Registers one loaded Scene Manager and removes stale entries.</summary>
        public void RegisterRendererManager(
            WorldLocationRendererManager rendererManager)
        {
            RemoveMissingRendererManagers();
            if (rendererManager == null ||
                m_worldLocationRenderers.Contains(rendererManager))
            {
                return;
            }

            m_worldLocationRenderers.Add(rendererManager);
            if (Application.isPlaying)
            {
                WorldSceneManager.Instance?.CheckForRequiredRenderers();
            }
        }

        /// <summary>Removes a Renderer Manager whose additive Scene is unloading.</summary>
        public void UnregisterRendererManager(
            WorldLocationRendererManager rendererManager)
        {
            m_worldLocationRenderers.Remove(rendererManager);
            RemoveMissingRendererManagers();
        }

        /// <summary>Rebuilds the list from all currently loaded Scenes.</summary>
        public void RefreshLoadedRendererManagers()
        {
            m_worldLocationRenderers.Clear();
            WorldLocationRendererManager[] rendererManagers =
                Object.FindObjectsByType<WorldLocationRendererManager>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            foreach (WorldLocationRendererManager rendererManager in
                rendererManagers)
            {
                if (rendererManager != null &&
                    !m_worldLocationRenderers.Contains(rendererManager))
                {
                    m_worldLocationRenderers.Add(rendererManager);
                }
            }
        }

        /// <summary>Prepares all loaded location Scenes for a runtime session.</summary>
        public void EnableGameMode()
        {
            RefreshLoadedRendererManagers();
            foreach (WorldLocationRendererManager rendererManager in
                m_worldLocationRenderers)
            {
                rendererManager.PrepareForGameMode();
            }
        }

        /// <summary>Enables all loaded location content for editing or baking.</summary>
        public void EnableLightBakeMode()
        {
            RefreshLoadedRendererManagers();
            foreach (WorldLocationRendererManager rendererManager in
                m_worldLocationRenderers)
            {
                rendererManager.PrepareForLightBakeMode();
            }
        }

        private void RemoveMissingRendererManagers()
        {
            m_worldLocationRenderers.RemoveAll(
                rendererManager => rendererManager == null);
        }
    }
}
