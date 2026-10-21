using System;
using UnityEngine;

namespace ZZ
{
    /// <summary>Independent appearance snapshot shared by creation, persistence and network conversion.</summary>
    [Serializable]
    public sealed class CharacterAppearanceData
    {
        public const int MaximumShapeCount = 128;
        [SerializeField] private float[] m_shapes = Array.Empty<float>();
        [SerializeField] private int m_hairstyle;
        [SerializeField] private Color32 m_skinColor = new(255, 255, 255, 255);
        [SerializeField] private Color32 m_hairColor = new(79, 53, 35, 255);
        [SerializeField] private Color32 m_eyeColor = new(91, 69, 42, 255);
        [SerializeField] private int m_eyebrow = 1;
        [SerializeField] private int m_beard;
        [SerializeField] private float m_irisSize = 1f;

        public int ShapeCount => m_shapes?.Length ?? 0;
        public int Hairstyle { get => Mathf.Clamp(m_hairstyle, 0, 8); set => m_hairstyle = Mathf.Clamp(value, 0, 8); }
        public Color32 SkinColor { get => m_skinColor; set => m_skinColor = Opaque(value); }
        public Color32 HairColor { get => m_hairColor; set => m_hairColor = Opaque(value); }
        public Color32 EyeColor { get => m_eyeColor; set => m_eyeColor = Opaque(value); }
        public int Eyebrow { get => Mathf.Clamp(m_eyebrow, 0, 4); set => m_eyebrow = Mathf.Clamp(value, 0, 4); }
        public int Beard { get => Mathf.Clamp(m_beard, 0, 4); set => m_beard = Mathf.Clamp(value, 0, 4); }
        public float IrisSize
        {
            get => m_irisSize > 0f && float.IsFinite(m_irisSize) ? Mathf.Clamp(m_irisSize, 0.7f, 1.3f) : 1f;
            set => m_irisSize = float.IsFinite(value) ? Mathf.Clamp(value, 0.7f, 1.3f) : 1f;
        }

        public float GetShape(int index) => index >= 0 && index < ShapeCount ? Sanitize(m_shapes[index]) : 0f;

        public void SetShape(int index, float value)
        {
            if (index < 0 || index >= MaximumShapeCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            if (index >= ShapeCount)
            {
                Array.Resize(ref m_shapes, index + 1);
            }
            m_shapes[index] = Sanitize(value);
        }

        public CharacterAppearanceData Clone()
        {
            var copy = new CharacterAppearanceData
            {
                Hairstyle = Hairstyle, SkinColor = SkinColor, HairColor = HairColor,
                EyeColor = EyeColor, Eyebrow = Eyebrow, Beard = Beard, IrisSize = IrisSize
            };
            for (int index = 0; index < Mathf.Min(ShapeCount, MaximumShapeCount); index++)
            {
                copy.SetShape(index, GetShape(index));
            }
            return copy;
        }

        private static float Sanitize(float value) => float.IsFinite(value) ? Mathf.Clamp(value, -1f, 1f) : 0f;
        private static Color32 Opaque(Color32 color) => new(color.r, color.g, color.b, 255);
    }
}
