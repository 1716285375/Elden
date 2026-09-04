using System;
using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// One generated greybox volume. This is the authored record of a single piece
    /// of level geometry: where it lives in the Area x Slice hierarchy, where it
    /// sits in the world, and why it exists.
    /// </summary>
    [Serializable]
    public sealed class GreyboxBox
    {
        [SerializeField] private int m_regionIndex;
        [SerializeField] private string m_area;
        [SerializeField] private string m_slice;
        [SerializeField] private string m_objectName;
        [SerializeField] private GreyboxRole m_role;
        [SerializeField] private Vector3 m_position;
        [SerializeField] private Vector3 m_rotation;
        [SerializeField] private Vector3 m_size;
        [SerializeField, TextArea] private string m_purpose;

        public GreyboxBox(
            int regionIndex,
            string area,
            string slice,
            string objectName,
            GreyboxRole role,
            Vector3 position,
            Vector3 rotation,
            Vector3 size,
            string purpose)
        {
            m_regionIndex = regionIndex;
            m_area = area;
            m_slice = slice;
            m_objectName = objectName;
            m_role = role;
            m_position = position;
            m_rotation = rotation;
            m_size = size;
            m_purpose = purpose;
        }

        /// <summary>Zero-based index into <see cref="WorldScenePathLayout"/> region folders.</summary>
        public int RegionIndex => m_regionIndex;

        /// <summary>Area root name, for example <c>A02_Graveyard</c>.</summary>
        public string Area => m_area;

        /// <summary>Streaming slice name: Base, Props, Effects, or Spawners.</summary>
        public string Slice => m_slice;

        /// <summary>Name given to the generated GameObject.</summary>
        public string ObjectName => m_objectName;

        /// <summary>Spatial function used to pick the debug material.</summary>
        public GreyboxRole Role => m_role;

        /// <summary>World position of the volume centre.</summary>
        public Vector3 Position => m_position;

        /// <summary>World rotation in Euler degrees.</summary>
        public Vector3 Rotation => m_rotation;

        /// <summary>Full world size in metres.</summary>
        public Vector3 Size => m_size;

        /// <summary>Design intent, kept next to the numbers so the layout stays reviewable.</summary>
        public string Purpose => m_purpose;

        /// <summary>Returns the Area root name qualified by region, for example <c>R01/A02_Graveyard</c>.</summary>
        public string QualifiedArea => $"R{m_regionIndex + 1:00}/{m_area}";

        public override string ToString() =>
            $"{QualifiedArea}/{m_slice}/{m_objectName} [{m_role}] {m_size}";
    }
}
