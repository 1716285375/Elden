using Unity.Netcode;
using UnityEngine;

namespace ZZ
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(CharacterNetworkManager))]
    public class CharacterManager : NetworkBehaviour
    {
        [HideInInspector] public CharacterController characterController;
        [HideInInspector] public CharacterNetworkManager characterNetworkManager;

        protected virtual void Awake()
        {
            characterController = GetComponent<CharacterController>();
            characterNetworkManager = GetComponent<CharacterNetworkManager>();

        }

        protected virtual void Update()
        {
        }

        protected virtual void LateUpdate()
        {
        }
    }
}
