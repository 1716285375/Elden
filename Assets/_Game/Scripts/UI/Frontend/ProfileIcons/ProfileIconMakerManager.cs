using UnityEngine;

namespace ZZ
{
    /// <summary>Completely rebuilds one reusable portrait dummy from save data.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ProfileIconMakerBodyManager))]
    [RequireComponent(typeof(ProfileIconMakerEquipmentManager))]
    public sealed class ProfileIconMakerManager : MonoBehaviour
    {
        [SerializeField] private ProfileIconMakerBodyManager m_bodyManager;
        [SerializeField]
        private ProfileIconMakerEquipmentManager m_equipmentManager;

        public ProfileIconMakerBodyManager BodyManager => m_bodyManager;
        public ProfileIconMakerEquipmentManager EquipmentManager =>
            m_equipmentManager;

        private void Awake()
        {
            m_bodyManager ??= GetComponent<ProfileIconMakerBodyManager>();
            m_equipmentManager ??=
                GetComponent<ProfileIconMakerEquipmentManager>();
            m_bodyManager?.InitializeBodyModels();
            m_equipmentManager?.InitializeArmorModels();
        }

        /// <summary>Overwrites sex, hair, color, head, and body from one save.</summary>
        public bool EquipDummy(CharacterSaveData characterData)
        {
            if (characterData == null ||
                m_bodyManager == null ||
                m_equipmentManager == null)
            {
                return false;
            }

            m_bodyManager.ChangeSex(characterData.IsMale);
            m_bodyManager.SetHairstyle(characterData.HairstyleID);
            m_bodyManager.SetHairColor(
                characterData.HairColorRed,
                characterData.HairColorGreen,
                characterData.HairColorBlue);

            WorldItemDatabase database = WorldItemDatabase.Instance;
            HeadEquipmentItem headEquipment = characterData.HeadEquipmentID >= 0
                ? database?.GetHeadEquipmentByID(characterData.HeadEquipmentID)
                : null;
            BodyEquipmentItem bodyEquipment = characterData.BodyEquipmentID >= 0
                ? database?.GetBodyEquipmentByID(characterData.BodyEquipmentID)
                : null;

            // Null is intentional: every slot must clear the previous dummy state.
            m_equipmentManager.LoadHeadEquipment(headEquipment);
            m_equipmentManager.LoadBodyEquipment(bodyEquipment);
            return true;
        }
    }
}
