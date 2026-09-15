using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Everything a resolver needs about one contact, and nothing about how it should be settled.
    /// The detector fills this in; <see cref="CombatResolver"/> decides what it means.
    /// </summary>
    public readonly struct HitCandidate
    {
        public readonly DamageCollider Detector;
        public readonly CharacterManager Attacker;
        public readonly CharacterManager Defender;
        public readonly HurtBox HurtBox;
        public readonly CombatActionInstance Action;
        public readonly Vector3 ContactPoint;
        public readonly int HitGroup;
        private readonly uint m_actionSequence;

        public HitCandidate(DamageCollider detector, CharacterManager attacker, Collider collider)
        {
            Detector = detector;
            Attacker = attacker;
            HurtBox = collider != null ? collider.GetComponent<HurtBox>() : null;
            Defender = HurtBox != null
                ? HurtBox.Owner
                : collider != null ? collider.GetComponentInParent<CharacterManager>() : null;
            ContactPoint = collider != null && detector != null
                ? collider.ClosestPointOnBounds(detector.transform.position)
                : Vector3.zero;
            CombatActionInstance action = attacker != null
                ? attacker.CharacterCombatManager?.ActionRunner.Current
                : null;
            // Projectiles outlive their launch action. They must not inherit a later sword swing's
            // guard rules or hit registry, and idle characters have no active strike to share.
            Action = action?.IsRunning == true &&
                (detector is MeleeWeaponDamageCollider || detector is AIDamageCollider)
                    ? action
                    : null;
            HitGroup = detector != null ? detector.HitGroup : 0;
            m_actionSequence = Action?.Sequence ?? 0u;
        }

        public bool IsValid => Detector != null && Defender != null &&
            (Action == null || Action.IsRunning && Action.Sequence == m_actionSequence);

        /// <summary>Deduplicates against the running action's registry, or the detector's own registry.</summary>
        public bool TryRegisterHit(out int hitIndex)
        {
            if (!IsValid)
            {
                hitIndex = -1;
                return false;
            }

            HitRegistry registry = Action != null ? Action.Hits : Detector?.Registry;
            if (registry == null)
            {
                hitIndex = -1;
                return false;
            }

            return registry.TryRegister(Defender, HitGroup, out hitIndex);
        }

        /// <summary>The payload for this contact: the committed snapshot when one exists.</summary>
        public DamagePayload CreatePayload()
        {
            return Action != null && Detector != null && Detector.UsesActionSnapshot &&
                Action.Snapshot.IsValid
                ? Action.Snapshot.CreatePayload(HurtBox)
                : Detector.CreatePayload(HurtBox);
        }
    }
}
