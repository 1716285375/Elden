using System;
using UnityEngine;

namespace ZZ
{
    /// <summary>Applies equipped item identities and original draw masks to a shared player skeleton.</summary>
    public sealed class PlayerSkinnedEquipment : MonoBehaviour
    {
        [SerializeField] private Part[] m_parts = Array.Empty<Part>();
        [SerializeField] private ArmorMask[] m_armorMasks = Array.Empty<ArmorMask>();
        private readonly bool[] m_hiddenMasks = new bool[128];

        private void Awake()
        {
            Refresh(true, -1, -1, -1, -1);
        }

        /// <summary>Refreshes presentation only when equipment, body type or preview visibility changes.</summary>
        public void Refresh(bool isMale, int headId, int bodyId, int handId, int legId)
        {
            Array.Clear(m_hiddenMasks, 0, m_hiddenMasks.Length);
            foreach (ArmorMask armor in m_armorMasks)
            {
                if (!IsEquipped(armor.ItemId, headId, bodyId, handId, legId))
                {
                    continue;
                }

                foreach (int mask in armor.HiddenMasks)
                {
                    if (mask >= 0 && mask < m_hiddenMasks.Length)
                    {
                        m_hiddenMasks[mask] = true;
                    }
                }
            }

            foreach (Part part in m_parts)
            {
                bool matchesBody = part.BodyType == 0 || part.BodyType == (isMale ? 1 : 2);
                bool matchesItem = part.ItemId < 0 ||
                    IsEquipped(part.ItemId, headId, bodyId, handId, legId);
                bool isHidden = part.Mask >= 0 && part.Mask < m_hiddenMasks.Length && m_hiddenMasks[part.Mask];
                if (part.Renderer != null)
                {
                    part.Renderer.enabled = matchesBody && matchesItem && !isHidden;
                }
            }
        }

        private static bool IsEquipped(int itemId, int headId, int bodyId, int handId, int legId)
        {
            return itemId >= 0 &&
                (itemId == headId || itemId == bodyId || itemId == handId || itemId == legId);
        }

        [Serializable]
        private sealed class Part
        {
            public SkinnedMeshRenderer Renderer;
            public int ItemId = -1;
            public int BodyType;
            public int Mask = -1;
        }

        [Serializable]
        private sealed class ArmorMask
        {
            public int ItemId;
            public int[] HiddenMasks = Array.Empty<int>();
        }
    }
}
