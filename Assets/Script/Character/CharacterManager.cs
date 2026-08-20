using Unity.Netcode;
using UnityEngine;

namespace ZZ
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(CharacterNetworkManager))]
    public class CharacterManager : NetworkBehaviour
    {
        [SerializeField] private Animator m_animator;
        [SerializeField] private CharacterAnimatorManager m_characterAnimatorManager;
        [SerializeField] private CharacterNetworkManager m_characterNetworkManager;

        public CharacterNetworkManager CharacterNetworkManager => m_characterNetworkManager;

        protected virtual void Awake()
        {
            m_animator = GetComponent<Animator>();
            if (m_animator == null)
            {
                m_animator = GetComponentInChildren<Animator>(true);
            }

            m_characterAnimatorManager = GetComponent<CharacterAnimatorManager>();
            m_characterNetworkManager = GetComponent<CharacterNetworkManager>();
            m_characterAnimatorManager?.Initialize(m_animator);
        }
    }
}
