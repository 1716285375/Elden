using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Maps one armor component to the matching embedded male and female modular meshes.
    /// </summary>
    [CreateAssetMenu(fileName = "Equipment Model", menuName = "ZZ/Items/Armor/Equipment Model")]
    public class EquipmentModel : ScriptableObject
    {
        [SerializeField] private EquipmentModelType m_equipmentModelType;
        [SerializeField] private string m_maleModelName = string.Empty;
        [SerializeField] private string m_femaleModelName = string.Empty;

        /// <summary>Gets the independently replaceable mesh group targeted by this record.</summary>
        public EquipmentModelType EquipmentModelType => m_equipmentModelType;

        /// <summary>Gets the embedded male modular mesh name.</summary>
        public string MaleModelName => m_maleModelName;

        /// <summary>Gets the embedded female modular mesh name.</summary>
        public string FemaleModelName => m_femaleModelName;

        /// <summary>Loads the body-type-specific model through its actual presentation owner.</summary>
        public bool LoadModel(
            PlayerEquipmentManager equipmentManager,
            bool isMale)
        {
            if (equipmentManager == null)
            {
                return false;
            }

            string modelName = isMale ? m_maleModelName : m_femaleModelName;
            return equipmentManager.LoadArmorModel(
                m_equipmentModelType,
                modelName);
        }
    }
}
