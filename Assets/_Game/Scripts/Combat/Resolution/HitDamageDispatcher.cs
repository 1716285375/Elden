using UnityEngine;

namespace ZZ
{
    /// <summary>Routes immutable contact damage through local prediction and network delivery.</summary>
    public static class HitDamageDispatcher
    {
        public static void Dispatch(CharacterManager attacker, CharacterManager target,
            DamagePayload payload, Vector3 contactPoint, bool wasBlocked)
        {
            CharacterNetworkManager networkManager =
                attacker?.CharacterNetworkManager ??
                target?.CharacterNetworkManager;
            if (networkManager != null && networkManager.IsSpawned)
            {
                ApplyDamageLocally(
                    target,
                    contactPoint,
                    wasBlocked,
                    target.IsOwner
                        ? DamageProcessingMode.Authoritative
                        : DamageProcessingMode.PredictedPresentation, attacker, payload);
                networkManager.RequestCharacterDamageServerRpc(
                    target.NetworkObjectId,
                    attacker != null
                        ? attacker.NetworkObjectId
                        : 0,
                    attacker != null,
                    payload.Physical,
                    payload.Magic,
                    payload.Fire,
                    payload.Lightning,
                    payload.Holy,
                    payload.Poise,
                    payload.Poison,
                    payload.Bleed,
                    payload.Frost,
                    contactPoint,
                    wasBlocked, payload.Stance);
                return;
            }

            ApplyDamageLocally(
                target,
                contactPoint,
                wasBlocked,
                DamageProcessingMode.Authoritative, attacker, payload);
        }

        private static void ApplyDamageLocally(
            CharacterManager target,
            Vector3 contactPoint,
            bool wasBlocked,
            DamageProcessingMode processingMode,
            CharacterManager attacker, DamagePayload payload)
        {
            CharacterEffectsManager effectsManager = target.CharacterEffectsManager;
            if (effectsManager == null)
            {
                Debug.LogWarning(
                    "Damage requires a target effects manager.",
                    target);
                return;
            }

            DamageEffect runtimeEffect = CreateRuntimeDamageEffect(
                target,
                contactPoint,
                wasBlocked, attacker, payload);
            if (runtimeEffect == null)
            {
                Debug.LogWarning(
                    "Damage requires the matching world damage template.",
                    target);
                return;
            }

            runtimeEffect.SetStanceDamage(payload.Stance);
            effectsManager.ProcessRuntimeDamageEffect(
                runtimeEffect,
                processingMode);
            if (!wasBlocked &&
                processingMode == DamageProcessingMode.Authoritative)
            {
                effectsManager.ProcessBuildupEffects(
                    payload.Poison,
                    payload.Bleed,
                    payload.Frost);
            }
        }

        private static DamageEffect CreateRuntimeDamageEffect(
            CharacterManager target,
            Vector3 contactPoint,
            bool wasBlocked, CharacterManager attacker, DamagePayload payload)
        {
            WorldCharacterEffectsManager effectsManager =
                WorldCharacterEffectsManager.Instance;
            if (wasBlocked)
            {
                TakeBlockedDamageEffect blockedTemplate =
                    effectsManager?.TakeBlockedDamageEffect;
                CharacterStatsManager statsManager = target.CharacterStatsManager;
                if (blockedTemplate == null || statsManager == null)
                {
                    return null;
                }

                return blockedTemplate.CreateRuntimeBlockedDamageEffect(
                    attacker,
                    payload.Physical,
                    payload.Magic,
                    payload.Fire,
                    payload.Lightning,
                    payload.Holy,
                    contactPoint,
                    payload.Poise,
                    statsManager.BlockingPhysicalAbsorption,
                    statsManager.BlockingMagicAbsorption,
                    statsManager.BlockingFireAbsorption,
                    statsManager.BlockingLightningAbsorption,
                    statsManager.BlockingHolyAbsorption,
                    statsManager.BlockingStability);
            }

            return effectsManager?.TakeDamageEffect?.CreateRuntimeDamageEffect(
                attacker,
                payload.Physical,
                payload.Magic,
                payload.Fire,
                payload.Lightning,
                payload.Holy,
                contactPoint,
                payload.Poise);
        }

    }
}
