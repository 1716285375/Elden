using System;

namespace ZZ
{
    /// <summary>Defines the concrete Unity-serializable world-item loot map.</summary>
    [Serializable]
    public sealed class WorldItemLootDictionary : SerializableDictionary<int, bool>
    {
    }
}
