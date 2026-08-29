using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Defines defensive values and modular presentation shared by wearable armor.
    /// </summary>
    public abstract class ArmorItem : EquipmentItem
    {
        [Header("Damage Absorption")]
        [SerializeField, Range(0f, 100f)] private float m_physicalAbsorption;
        [SerializeField, Range(0f, 100f)] private float m_magicAbsorption;
        [SerializeField, Range(0f, 100f)] private float m_fireAbsorption;
        [SerializeField, Range(0f, 100f)] private float m_lightningAbsorption;
        [SerializeField, Range(0f, 100f)] private float m_holyAbsorption;

        [Header("Resistance")]
        [SerializeField, Min(0f)] private float m_immunity;
        [SerializeField, Min(0f)] private float m_robustness;
        [SerializeField, Min(0f)] private float m_focus;
        [SerializeField, Min(0f)] private float m_vitality;
        [SerializeField, Min(0f)] private float m_poise;

        [Header("Equipment Models")]
        [SerializeField] private EquipmentModel[] m_equipmentModels =
            System.Array.Empty<EquipmentModel>();

        /// <summary>Gets Physical absorption supplied by this armor piece.</summary>
        public float PhysicalAbsorption => Mathf.Clamp(m_physicalAbsorption, 0f, 100f);

        /// <summary>Gets Magic absorption supplied by this armor piece.</summary>
        public float MagicAbsorption => Mathf.Clamp(m_magicAbsorption, 0f, 100f);

        /// <summary>Gets Fire absorption supplied by this armor piece.</summary>
        public float FireAbsorption => Mathf.Clamp(m_fireAbsorption, 0f, 100f);

        /// <summary>Gets Lightning absorption supplied by this armor piece.</summary>
        public float LightningAbsorption => Mathf.Clamp(m_lightningAbsorption, 0f, 100f);

        /// <summary>Gets Holy absorption supplied by this armor piece.</summary>
        public float HolyAbsorption => Mathf.Clamp(m_holyAbsorption, 0f, 100f);

        /// <summary>Gets Immunity resistance supplied by this armor piece.</summary>
        public float Immunity => Mathf.Max(0f, m_immunity);

        /// <summary>Gets Robustness resistance supplied by this armor piece.</summary>
        public float Robustness => Mathf.Max(0f, m_robustness);

        /// <summary>Gets Focus resistance supplied by this armor piece.</summary>
        public float Focus => Mathf.Max(0f, m_focus);

        /// <summary>Gets Vitality resistance supplied by this armor piece.</summary>
        public float Vitality => Mathf.Max(0f, m_vitality);

        /// <summary>Gets passive Poise supplied by this armor piece.</summary>
        public float Poise => Mathf.Max(0f, m_poise);

        /// <summary>Gets modular model records loaded for the selected body type.</summary>
        public EquipmentModel[] EquipmentModels => m_equipmentModels;
    }
}
