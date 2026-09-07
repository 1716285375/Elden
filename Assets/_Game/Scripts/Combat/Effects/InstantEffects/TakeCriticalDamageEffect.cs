using UnityEngine;

namespace ZZ
{
    [GameAsset(
        FileName = "Take Critical Damage Effect",
        MenuName = "ZZ/Character Effects/Instant/Take Critical Damage")]
    public class TakeCriticalDamageEffect : TakeDamageEffect
    {
        /// <summary>Creates a transient critical payload for delayed animation-frame damage.</summary>
        public TakeCriticalDamageEffect CreateRuntimeCriticalDamageEffect(
            CharacterManager characterCausingDamage,
            float physicalDamage,
            float magicDamage,
            float fireDamage,
            float lightningDamage,
            float holyDamage,
            Vector3 contactPoint,
            float poiseDamage)
        {
            TakeCriticalDamageEffect runtimeEffect =
                (TakeCriticalDamageEffect)CreateRuntimeInstance();
            runtimeEffect.ConfigureRuntimeDamage(
                characterCausingDamage,
                physicalDamage,
                magicDamage,
                fireDamage,
                lightningDamage,
                holyDamage,
                contactPoint,
                poiseDamage);
            return runtimeEffect;
        }

        /// <inheritdoc />
        public override void ProcessDamage(
            CharacterManager character,
            DamageProcessingMode processingMode)
        {
            if (character == null ||
                character.IsDead ||
                processingMode != DamageProcessingMode.Authoritative)
            {
                return;
            }

            int pendingDamage = CalculateDamage(character.CharacterStatsManager);
            character.CharacterCombatManager?.SetPendingCriticalDamage(
                pendingDamage,
                CharacterCausingDamage);
        }
    }
}
