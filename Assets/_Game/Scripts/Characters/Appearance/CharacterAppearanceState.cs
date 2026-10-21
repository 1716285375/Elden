using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace ZZ
{
    /// <summary>One bounded, quantized owner-authored appearance update, independent of rendering.</summary>
    public struct CharacterAppearanceState : INetworkSerializable, IEquatable<CharacterAppearanceState>
    {
        private FixedList512Bytes<sbyte> m_shapes;
        private uint m_skin;
        private uint m_hair;
        private byte m_hairstyle;
        private uint m_eyes;
        private byte m_eyebrow;
        private byte m_beard;
        private byte m_irisSize;

        public static CharacterAppearanceState FromData(CharacterAppearanceData data)
        {
            data ??= new CharacterAppearanceData();
            var state = new CharacterAppearanceState
            {
                m_skin = Pack(data.SkinColor), m_hair = Pack(data.HairColor), m_hairstyle = (byte)data.Hairstyle,
                m_eyes = Pack(data.EyeColor), m_eyebrow = (byte)data.Eyebrow, m_beard = (byte)data.Beard,
                m_irisSize = (byte)Mathf.RoundToInt((data.IrisSize - 0.7f) / 0.6f * 255f)
            };
            for (int index = 0; index < Mathf.Min(data.ShapeCount, CharacterAppearanceData.MaximumShapeCount); index++)
            {
                state.m_shapes.Add((sbyte)Mathf.RoundToInt(data.GetShape(index) * 127f));
            }
            return state;
        }

        public CharacterAppearanceData ToData()
        {
            var data = new CharacterAppearanceData
            {
                Hairstyle = m_hairstyle, SkinColor = Unpack(m_skin), HairColor = Unpack(m_hair),
                EyeColor = Unpack(m_eyes), Eyebrow = m_eyebrow, Beard = m_beard,
                IrisSize = 0.7f + m_irisSize / 255f * 0.6f
            };
            for (int index = 0; index < m_shapes.Length; index++)
            {
                data.SetShape(index, m_shapes[index] / 127f);
            }
            return data;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref m_skin);
            serializer.SerializeValue(ref m_hair);
            serializer.SerializeValue(ref m_hairstyle);
            serializer.SerializeValue(ref m_eyes);
            serializer.SerializeValue(ref m_eyebrow);
            serializer.SerializeValue(ref m_beard);
            serializer.SerializeValue(ref m_irisSize);
            byte count = (byte)m_shapes.Length;
            serializer.SerializeValue(ref count);
            if (count > CharacterAppearanceData.MaximumShapeCount)
            {
                throw new InvalidOperationException("Appearance shape count exceeds the supported schema.");
            }
            if (serializer.IsReader)
            {
                m_shapes.Clear();
            }
            for (int index = 0; index < count; index++)
            {
                sbyte value = serializer.IsReader ? (sbyte)0 : m_shapes[index];
                serializer.SerializeValue(ref value);
                if (serializer.IsReader)
                {
                    m_shapes.Add(value);
                }
            }
        }

        public bool Equals(CharacterAppearanceState other) => m_skin == other.m_skin && m_hair == other.m_hair &&
            m_hairstyle == other.m_hairstyle && m_eyes == other.m_eyes && m_eyebrow == other.m_eyebrow &&
            m_beard == other.m_beard && m_irisSize == other.m_irisSize && m_shapes.Equals(other.m_shapes);
        public override bool Equals(object other) => other is CharacterAppearanceState state && Equals(state);
        public override int GetHashCode() => HashCode.Combine(m_skin, m_hair, m_hairstyle, m_eyes,
            m_eyebrow, m_beard, m_irisSize, m_shapes.Length);
        private static uint Pack(Color32 color) => (uint)(color.r | color.g << 8 | color.b << 16);
        private static Color32 Unpack(uint color) => new((byte)color, (byte)(color >> 8), (byte)(color >> 16), 255);
    }
}
