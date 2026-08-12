using UnityEngine;

namespace ZZ
{
    [RequireComponent(typeof(CharacterController))]
    public class CharacterLocomotionManager : MonoBehaviour
    {
        [HideInInspector] public CharacterController characterController;

        protected virtual void Awake()
        {
            characterController = GetComponent<CharacterController>();
        }
    }
}
