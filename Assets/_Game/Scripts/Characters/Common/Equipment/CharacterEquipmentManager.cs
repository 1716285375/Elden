using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Provides shared character ownership context to equipment presentation implementations.
    /// </summary>
    public class CharacterEquipmentManager : MonoBehaviour
    {
        protected CharacterManager Character { get; private set; }

        protected virtual void Awake()
        {
            Character = GetComponent<CharacterManager>();
        }
    }
}
