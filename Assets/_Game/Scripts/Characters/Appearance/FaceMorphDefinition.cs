using System;
using UnityEngine;

namespace ZZ
{
    /// <summary>Stable, ordered semantic controls imported from the original FaceGen control basis.</summary>
    public sealed class FaceMorphDefinition : ScriptableObject
    {
        [SerializeField] private Control[] m_controls = Array.Empty<Control>();
        public int Count => m_controls.Length;
        public Control GetControl(int index) => m_controls[index];

        [Serializable]
        public sealed class Control
        {
            public string SourceName;
            public string Label;
            public string Group;
            public bool IsSymmetric;
        }
    }
}
