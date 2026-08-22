using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Instantiates and aligns one runtime weapon model beneath a character bone.
    /// </summary>
    public class WeaponModelInstantiationSlot : MonoBehaviour
    {
        [SerializeField] private WeaponModelSlot m_weaponModelSlot;

        private GameObject m_currentWeaponModel;

        /// <summary>Gets the hand represented by this attachment point.</summary>
        public WeaponModelSlot WeaponModelSlot => m_weaponModelSlot;

        /// <summary>Gets the currently instantiated weapon model.</summary>
        public GameObject CurrentWeaponModel => m_currentWeaponModel;

        /// <summary>Gets the weapon manager of the currently instantiated weapon model.</summary>
        public WeaponManager CurrentWeaponManager { get; private set; }

        /// <summary>
        /// Replaces the current model with a locally aligned runtime weapon prefab.
        /// </summary>
        public void LoadWeaponModel(WeaponItem weapon, CharacterManager weaponOwner)
        {
            UnloadWeaponModel();
            if (weapon == null || weapon.WeaponModel == null)
            {
                Debug.LogError("A valid WeaponItem and weapon model prefab are required.", this);
                return;
            }

            m_currentWeaponModel = Instantiate(weapon.WeaponModel, transform);
            Transform modelTransform = m_currentWeaponModel.transform;
            modelTransform.localPosition = Vector3.zero;
            modelTransform.localRotation = Quaternion.identity;
            modelTransform.localScale = Vector3.one;

            WeaponManager weaponManager =
                m_currentWeaponModel.GetComponentInChildren<WeaponManager>(true);
            if (weaponManager == null)
            {
                Debug.LogError(
                    $"Weapon prefab {weapon.WeaponModel.name} is missing WeaponManager.",
                    m_currentWeaponModel);
                return;
            }

            weaponManager.Initialize(weaponOwner, weapon);
            CurrentWeaponManager = weaponManager;
        }

        /// <summary>
        /// Removes the currently instantiated weapon model from this slot.
        /// </summary>
        public void UnloadWeaponModel()
        {
            if (m_currentWeaponModel == null)
            {
                return;
            }

            Destroy(m_currentWeaponModel);
            m_currentWeaponModel = null;
            CurrentWeaponManager = null;
        }
    }
}
