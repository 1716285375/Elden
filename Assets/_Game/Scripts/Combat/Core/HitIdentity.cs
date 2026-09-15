using Unity.Netcode;

namespace ZZ
{
    /// <summary>
    /// Stable identity for one contact: who swung, which attack, which swing of the combo, which
    /// strike group inside that swing, and who was struck. Used for deduplication, networking
    /// validation and the combat debugger.
    /// </summary>
    public readonly struct HitIdentity
    {
        public readonly ulong AttackerId;
        public readonly uint AttackId;
        public readonly uint Sequence;
        public readonly ulong TargetId;
        public readonly int HitGroup;
        public readonly int HitIndex;

        public HitIdentity(ulong attackerId, uint attackId, uint sequence, ulong targetId,
            int hitGroup, int hitIndex)
        {
            AttackerId = attackerId;
            AttackId = attackId;
            Sequence = sequence;
            TargetId = targetId;
            HitGroup = hitGroup;
            HitIndex = hitIndex;
        }

        public static HitIdentity For(CharacterManager attacker, CombatActionInstance action,
            CharacterManager target, int hitGroup, int hitIndex)
        {
            return new HitIdentity(
                attacker != null && attacker.IsSpawned ? attacker.NetworkObjectId : 0ul,
                action?.AttackId ?? 0u,
                action?.Sequence ?? 0u,
                target != null && target.IsSpawned ? target.NetworkObjectId : 0ul,
                hitGroup,
                hitIndex);
        }
    }
}
