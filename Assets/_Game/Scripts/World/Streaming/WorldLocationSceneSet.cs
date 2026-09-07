using System.Collections.Generic;
using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Describes the physical additive Scenes and visible neighbour locations for
    /// one player-facing world location.
    /// </summary>
    [GameAsset(
        FileName = "World Location Scene Set",
        MenuName = "World/World Location Scene Set")]
    public class WorldLocationSceneSet : ScriptableObject
    {
        [SerializeField] private string m_locationID;
        [SerializeField] private WorldSceneLocation m_legacyLocation;
        [SerializeField] private List<string> m_scenesRequiredForThisLocation =
            new();
        [SerializeField] private List<WorldLocationSceneSet> m_requiredLocations =
            new();

        /// <summary>Gets the stable designer-facing location identifier.</summary>
        public string LocationID => m_locationID;

        /// <summary>Gets the former enum value used only to migrate old triggers.</summary>
        public WorldSceneLocation LegacyLocation => m_legacyLocation;

        /// <summary>Gets the physical Scenes owned by this logical location.</summary>
        public IReadOnlyList<string> ScenesRequiredForThisLocation =>
            m_scenesRequiredForThisLocation;

        /// <summary>Gets directly visible neighbouring locations.</summary>
        public IReadOnlyList<WorldLocationSceneSet> RequiredLocations =>
            m_requiredLocations;

        /// <summary>Gets the preferred Scene for local lighting activation.</summary>
        public string PrimarySceneID
        {
            get
            {
                foreach (string sceneID in m_scenesRequiredForThisLocation)
                {
                    if (!string.IsNullOrWhiteSpace(sceneID))
                    {
                        return sceneID.Trim();
                    }
                }

                return string.Empty;
            }
        }

        /// <summary>
        /// Builds the deduplicated union of this location's Scenes and each
        /// directly required location's own Scenes.
        /// </summary>
        public List<string> GetRequiredSceneIDsForWorldLocation()
        {
            List<string> requiredSceneIDs = new();
            AddUniqueSceneIDs(
                requiredSceneIDs,
                m_scenesRequiredForThisLocation);
            foreach (WorldLocationSceneSet requiredLocation in
                m_requiredLocations)
            {
                if (requiredLocation == null || requiredLocation == this)
                {
                    continue;
                }

                AddUniqueSceneIDs(
                    requiredSceneIDs,
                    requiredLocation.m_scenesRequiredForThisLocation);
            }

            return requiredSceneIDs;
        }

        private static void AddUniqueSceneIDs(
            ICollection<string> destination,
            IEnumerable<string> sceneIDs)
        {
            if (sceneIDs == null)
            {
                return;
            }

            foreach (string sceneID in sceneIDs)
            {
                string normalizedSceneID = sceneID?.Trim();
                if (!string.IsNullOrEmpty(normalizedSceneID) &&
                    !destination.Contains(normalizedSceneID))
                {
                    destination.Add(normalizedSceneID);
                }
            }
        }
    }
}
