using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Provides shared character ownership context to inventory implementations.
    /// </summary>
    public class CharacterInventoryManager : MonoBehaviour
    {
        protected CharacterManager Character { get; private set; }

        protected virtual void Awake()
        {
            Character = GetComponent<CharacterManager>();
        }
    }
}
