using System;
using UnityEngine;

namespace ZZ
{
    /// <summary>Places extracted equipment using its regulation-selected FLVER dummy frame.</summary>
    public sealed class OriginalWeaponAttachment : MonoBehaviour
    {
        [SerializeField] private Placement m_rightHand;
        [SerializeField] private Placement m_leftHand;
        [SerializeField] private Placement m_twoHandRight;
        [SerializeField] private Placement m_twoHandLeft;
        [SerializeField] private Placement m_storedRight;
        [SerializeField] private Placement m_storedLeft;

        /// <summary>Applies the regulation-selected hand, two-hand, or stored placement to a player model.</summary>
        public bool Apply(WeaponModelInstantiationSlot slot, CharacterManager owner)
        {
            if (owner is not PlayerManager player)
            {
                return false;
            }

            PlayerNetworkManager network = player.PlayerNetworkManager;
            bool isStored = slot.WeaponModelSlot == WeaponModelSlot.BackSlot || slot.WeaponModelSlot == WeaponModelSlot.HipSlot;
            Placement placement;
            if (isStored)
            {
                placement = network?.IsTwoHandingLeftWeapon.Value == true ? m_storedRight : m_storedLeft;
            }
            else if (network?.IsTwoHandingWeapon.Value == true)
            {
                placement = network.IsTwoHandingLeftWeapon.Value ? m_twoHandLeft : m_twoHandRight;
            }
            else
            {
                placement = slot.WeaponModelSlot == WeaponModelSlot.RightHandSlot ? m_rightHand : m_leftHand;
            }
            foreach (Transform bone in owner.GetComponentsInChildren<Transform>(true))
            {
                if (bone.name != "ER_" + placement.Bone)
                {
                    continue;
                }

                slot.transform.SetParent(bone, false);
                slot.transform.localPosition = Vector3.zero;
                slot.transform.localRotation = Quaternion.identity;
                slot.transform.localScale = Vector3.one;
                transform.localPosition = placement.Position;
                transform.localRotation = placement.Rotation;
                transform.localScale = Vector3.one;
                return true;
            }

            Debug.LogError($"Missing extracted equipment bone '{placement.Bone}' for {name}.", this);
            return false;
        }

        [Serializable]
        private struct Placement
        {
            public string Bone;
            public Vector3 Position;
            public Quaternion Rotation;
        }
    }
}
