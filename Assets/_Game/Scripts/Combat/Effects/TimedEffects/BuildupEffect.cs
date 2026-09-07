using UnityEngine;

namespace ZZ
{
    [GameAsset(
        FileName = "Degrade Buildup Effect",
        MenuName = "ZZ/Character Effects/Timed/Degrade Buildup")]
    public class BuildupEffect : TimedCharacterEffect
    {
        [SerializeField] private Buildup m_buildupType;
        [SerializeField] private float m_buildupAmountDegradation = -1f;

        /// <summary>Gets the accumulation channel reduced by this effect.</summary>
        public Buildup BuildupType => m_buildupType;

        /// <summary>Gets the signed amount applied during each shared effect tick.</summary>
        public float BuildupAmountDegradation =>
            Mathf.Min(0f, m_buildupAmountDegradation);

        /// <summary>Gets the value observed after this runtime effect last processed.</summary>
        public float BuildupRemaining { get; private set; }

        /// <inheritdoc />
        public override void ProcessEffect(CharacterManager character)
        {
            if (character == null || character.IsDead)
            {
                character?.CharacterEffectsManager?.RemoveTimedEffect(TimedEffectID);
                return;
            }

            CharacterNetworkManager networkManager =
                character.CharacterNetworkManager;
            if (networkManager == null || !character.IsSpawned || !character.IsOwner)
            {
                return;
            }

            float currentBuildup = networkManager.GetBuildup(m_buildupType);
            float capacity = Mathf.Max(0f, networkManager.BuildupCapacity.Value);
            if (ShouldStopDegrading(currentBuildup, capacity))
            {
                BuildupRemaining = currentBuildup;
                character.CharacterEffectsManager?.RemoveTimedEffect(TimedEffectID);
                return;
            }

            CharacterStatsManager statsManager = character.CharacterStatsManager;
            if (statsManager == null)
            {
                return;
            }

            BuildupRemaining = statsManager.DegradeBuildup(this);
            if (ShouldStopDegrading(BuildupRemaining, capacity))
            {
                character.CharacterEffectsManager?.RemoveTimedEffect(TimedEffectID);
            }
        }

        /// <summary>Returns whether accumulation has fully cleared or reached its trigger point.</summary>
        public static bool ShouldStopDegrading(float buildupAmount, float capacity)
        {
            return buildupAmount <= 0f ||
                capacity > 0f && buildupAmount >= capacity;
        }
    }
}
