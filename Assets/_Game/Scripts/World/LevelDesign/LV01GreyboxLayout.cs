using System.Collections.Generic;
using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// The authored greybox record for LV01. Editing a box here and regenerating is
    /// the intended iteration loop; use <c>LV01GreyboxSpec</c> to rebuild the whole
    /// set from the design table.
    /// </summary>
    [GameAsset(
        MenuName = "World/LV01 Greybox Layout",
        FileName = "LV01_GreyboxLayout")]
    public class LV01GreyboxLayout : ScriptableObject
    {
        [Header("Measured Player Metrics")]
        [SerializeField] private float m_playerHeight = 2f;
        [SerializeField] private float m_playerRadius = 0.35f;
        [SerializeField] private float m_cameraPivotHeight = 1.65f;
        [SerializeField] private float m_cameraDistance = 2.5f;

        [Header("Design Scale")]
        [Tooltip("Reference human height the design table was authored against.")]
        [SerializeField] private float m_referenceHeight = 1.8f;

        [Header("Geometry")]
        [SerializeField] private List<GreyboxBox> m_boxes = new();

        private readonly List<(int RegionIndex, string Area, Bounds Bounds)> m_areaIndex = new();
        private bool m_areaIndexValid;

        /// <summary>CharacterController height measured from the Player prefab.</summary>
        public float PlayerHeight => m_playerHeight;

        /// <summary>CharacterController radius measured from the Player prefab.</summary>
        public float PlayerRadius => m_playerRadius;

        /// <summary>Height of the camera pivot above the player's feet.</summary>
        public float CameraPivotHeight => m_cameraPivotHeight;

        /// <summary>Distance the camera sits behind its pivot.</summary>
        public float CameraDistance => m_cameraDistance;

        /// <summary>Height the design table was authored against.</summary>
        public float ReferenceHeight => m_referenceHeight;

        /// <summary>Every dimension in the design table is multiplied by this.</summary>
        public float ScaleFactor =>
            m_referenceHeight > 0f ? m_playerHeight / m_referenceHeight : 1f;

        /// <summary>The full authored geometry set.</summary>
        public IReadOnlyList<GreyboxBox> Boxes => m_boxes;

        /// <summary>Replaces the whole geometry set.</summary>
        public void SetBoxes(IReadOnlyList<GreyboxBox> boxes)
        {
            m_boxes.Clear();
            m_boxes.AddRange(boxes);
            m_areaIndexValid = false;
        }

        /// <summary>Stores the metrics read from the real Player prefab.</summary>
        public void SetPlayerMetrics(
            float playerHeight,
            float playerRadius,
            float cameraPivotHeight,
            float cameraDistance)
        {
            m_playerHeight = playerHeight;
            m_playerRadius = playerRadius;
            m_cameraPivotHeight = cameraPivotHeight;
            m_cameraDistance = cameraDistance;
        }

        /// <summary>
        /// Finds the Area whose geometry contains the supplied world position.
        /// Areas are tested in layout order, so overlapping Areas resolve to the
        /// one authored first.
        /// </summary>
        public bool TryGetAreaAt(Vector3 position, out int regionIndex, out string area)
        {
            EnsureAreaIndex();
            foreach ((int boxRegion, string boxArea, Bounds bounds) in m_areaIndex)
            {
                if (!bounds.Contains(position))
                {
                    continue;
                }

                regionIndex = boxRegion;
                area = boxArea;
                return true;
            }

            regionIndex = -1;
            area = null;
            return false;
        }

        /// <summary>Returns the cached per-Area bounds in layout order.</summary>
        public IReadOnlyList<(int RegionIndex, string Area, Bounds Bounds)> GetAreaBounds()
        {
            EnsureAreaIndex();
            return m_areaIndex;
        }

        /// <summary>
        /// Computes the world-space bounds of every box belonging to one Area.
        /// Returns false when the Area has no geometry.
        /// </summary>
        public bool TryGetAreaBounds(int regionIndex, string area, out Bounds bounds)
        {
            EnsureAreaIndex();
            foreach ((int boxRegion, string boxArea, Bounds areaBounds) in m_areaIndex)
            {
                if (boxRegion != regionIndex || boxArea != area)
                {
                    continue;
                }

                bounds = areaBounds;
                return true;
            }

            bounds = default;
            return false;
        }

        private void EnsureAreaIndex()
        {
            if (m_areaIndexValid)
            {
                return;
            }

            m_areaIndex.Clear();
            foreach (GreyboxBox box in m_boxes)
            {
                Bounds boxBounds = new(box.Position, box.Size);
                int existing = m_areaIndex.FindIndex(
                    entry => entry.RegionIndex == box.RegionIndex && entry.Area == box.Area);
                if (existing < 0)
                {
                    m_areaIndex.Add((box.RegionIndex, box.Area, boxBounds));
                    continue;
                }

                Bounds merged = m_areaIndex[existing].Bounds;
                merged.Encapsulate(boxBounds);
                m_areaIndex[existing] = (box.RegionIndex, box.Area, merged);
            }

            m_areaIndexValid = true;
        }
    }
}
