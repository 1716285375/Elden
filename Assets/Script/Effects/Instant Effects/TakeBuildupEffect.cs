using UnityEngine;

namespace ZZ
{
    [CreateAssetMenu(
        fileName = "Take Buildup Effect",
        menuName = "ZZ/Character Effects/Instant/Take Buildup")]
    public class TakeBuildupEffect : InstantCharacterEffect
    {
        [SerializeField] private Buildup m_buildupType;
        [SerializeField, Min(0f)] private float m_buildupAmount = 25f;
        [SerializeField] private BuildupEffect m_degradeBuildupEffect;

        /// <summary>Gets the accumulation channel increased by this effect.</summary>
        public Buildup BuildupType => m_buildupType;

        /// <summary>Gets the sanitized amount added by this runtime effect.</summary>
        public float BuildupAmount => Mathf.Max(0f, m_buildupAmount);

        /// <summary>Gets the decay template refreshed after buildup is applied.</summary>
        public BuildupEffect DegradeBuildupEffect => m_degradeBuildupEffect;

        /// <summary>Creates a transient payload with one caller-supplied buildup amount.</summary>
        public TakeBuildupEffect CreateRuntimeBuildupEffect(float buildupAmount)
        {
            TakeBuildupEffect runtimeEffect =
                (TakeBuildupEffect)CreateRuntimeInstance();
            runtimeEffect.m_buildupAmount = Mathf.Max(0f, buildupAmount);
            return runtimeEffect;
        }

        /// <inheritdoc />
        public override void ProcessEffect(CharacterManager character)
        {
            if (character == null || character.IsDead || BuildupAmount <= 0f)
            {
                return;
            }

            CharacterEffectsManager effectsManager =
                character.CharacterEffectsManager;
            if (effectsManager == null ||
                !effectsManager.AddBuildup(m_buildupType, BuildupAmount))
            {
                return;
            }

            BuildupEffect decayEffect = ResolveDegradeEffect();
            if (decayEffect != null)
            {
                effectsManager.AddTimedEffect(decayEffect);
                CharacterNetworkManager networkManager =
                    character.CharacterNetworkManager;
                if (networkManager != null &&
                    BuildupEffect.ShouldStopDegrading(
                        networkManager.GetBuildup(m_buildupType),
                        networkManager.BuildupCapacity.Value))
                {
                    effectsManager.RemoveTimedEffect(decayEffect.TimedEffectID);
                }
            }
        }

        private BuildupEffect ResolveDegradeEffect()
        {
            if (m_degradeBuildupEffect != null)
            {
                return m_degradeBuildupEffect;
            }

            WorldCharacterEffectsManager worldEffects =
                WorldCharacterEffectsManager.Instance;
            return m_buildupType == Buildup.Poison
                ? worldEffects?.DegradePoisonBuildupEffect
                : worldEffects?.DegradeBleedBuildupEffect;
        }
    }
}
