using System.Collections.Generic;
using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Tracks scene-authored world objects that other gameplay systems must resolve by ID.
    /// </summary>
    [DefaultExecutionOrder(-8500)]
    public class WorldObjectManager : MonoBehaviour
    {
        private static WorldObjectManager s_instance;

        private readonly List<SiteOfGraceInteractable> m_sitesOfGrace = new();

        /// <summary>Gets the registry for the currently loaded gameplay scene.</summary>
        public static WorldObjectManager Instance => s_instance;

        /// <summary>Gets every spawned Site of Grace registered on this peer.</summary>
        public IReadOnlyList<SiteOfGraceInteractable> SitesOfGrace =>
            m_sitesOfGrace;

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                Destroy(gameObject);
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

        /// <summary>Registers a spawned Site of Grace without allowing duplicates.</summary>
        public void RegisterSiteOfGrace(SiteOfGraceInteractable siteOfGrace)
        {
            if (siteOfGrace != null && !m_sitesOfGrace.Contains(siteOfGrace))
            {
                m_sitesOfGrace.Add(siteOfGrace);
            }
        }

        /// <summary>Removes a despawned Site of Grace from the local world registry.</summary>
        public void UnregisterSiteOfGrace(SiteOfGraceInteractable siteOfGrace)
        {
            m_sitesOfGrace.Remove(siteOfGrace);
        }

        /// <summary>Finds one spawned Site of Grace by its stable save identifier.</summary>
        public SiteOfGraceInteractable GetSiteOfGraceByID(int siteOfGraceID)
        {
            m_sitesOfGrace.RemoveAll(site => site == null);
            return m_sitesOfGrace.Find(
                site => site.SiteOfGraceID == siteOfGraceID);
        }

        /// <summary>Finds the saved checkpoint or falls back to the first valid Site.</summary>
        public SiteOfGraceInteractable GetRespawnSiteOfGrace(
            int preferredSiteOfGraceID)
        {
            m_sitesOfGrace.RemoveAll(site => site == null);
            SiteOfGraceInteractable preferredSite =
                GetSiteOfGraceByID(preferredSiteOfGraceID);
            return preferredSite != null
                ? preferredSite
                : m_sitesOfGrace.Count > 0
                    ? m_sitesOfGrace[0]
                    : null;
        }
    }
}
