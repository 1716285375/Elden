using UnityEngine;

namespace ZZ
{
    /// <summary>Identifies a damageable body part independently of the character's movement collider.</summary>
    [DisallowMultipleComponent]
    public sealed class HurtBox : MonoBehaviour
    {
        [SerializeField] private CharacterManager m_owner;
        [SerializeField] private BodyPart m_bodyPart = BodyPart.Torso;
        [SerializeField, Min(0f)] private float m_damageMultiplier = 1f;
        [SerializeField, Min(0f)] private float m_poiseMultiplier = 1f;
        [SerializeField] private bool m_isMovementOnly;

        public CharacterManager Owner => m_owner;
        public BodyPart BodyPart => m_bodyPart;
        public float DamageMultiplier => Mathf.Max(0f, m_damageMultiplier);
        public float PoiseMultiplier => Mathf.Max(0f, m_poiseMultiplier);
        public bool IsMovementOnly => m_isMovementOnly;

        private void Awake()
        {
            if (m_owner == null)
            {
                m_owner = GetComponentInParent<CharacterManager>();
            }
        }

        /// <summary>Restores the body part recorded as text by assets authored before the enum existed.</summary>
        public static BodyPart ParseBodyPart(string bodyPart)
        {
            if (string.IsNullOrWhiteSpace(bodyPart))
            {
                return BodyPart.Torso;
            }

            foreach (BodyPart candidate in System.Enum.GetValues(typeof(BodyPart)))
            {
                if (string.Equals(candidate.ToString(), bodyPart.Trim(),
                        System.StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }

            return BodyPart.Torso;
        }
    }
}
