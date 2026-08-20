using UnityEngine;

namespace ZZ
{
    [RequireComponent(typeof(CharacterController))]
    public class CharacterLocomotionManager : MonoBehaviour
    {
        protected CharacterController m_characterController;

        protected virtual void Awake()
        {
            m_characterController = GetComponent<CharacterController>();
        }
    }
}
