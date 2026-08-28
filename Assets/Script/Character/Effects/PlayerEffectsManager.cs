using UnityEngine;

namespace ZZ
{
    [RequireComponent(typeof(PlayerManager))]
    public class PlayerEffectsManager : CharacterEffectsManager
    {
        [Header("Debug")]
        [SerializeField] private InstantCharacterEffect m_effectToTest;
        [SerializeField] private bool m_shouldProcessEffect;

        protected override void Update()
        {
            base.Update();
            if (!m_shouldProcessEffect)
            {
                return;
            }

            m_shouldProcessEffect = false;
            if (Character == null || !Character.IsSpawned || !Character.IsOwner)
            {
                Debug.LogWarning(
                    "Debug effects can only be processed by the locally owned spawned player.",
                    this);
                return;
            }

            ProcessInstantEffect(m_effectToTest);
        }
    }
}
