using UnityEngine;

namespace ZZ
{
    /// <summary>Exposes the authored origin used by one equipped catalyst.</summary>
    public class SpellInstantiationLocation : MonoBehaviour
    {
        [SerializeField] private Transform m_instantiationTransform;

        /// <summary>Gets the transform used for warm-up effects and released spells.</summary>
        public Transform InstantiationTransform => m_instantiationTransform != null
            ? m_instantiationTransform
            : transform;
    }
}
