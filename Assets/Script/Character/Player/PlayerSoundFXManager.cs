namespace ZZ
{
    public class PlayerSoundFXManager : CharacterSoundFXManager
    {
        private PlayerManager m_player;

        protected override void Awake()
        {
            base.Awake();
            m_player = GetComponentInParent<PlayerManager>();
        }

        /// <inheritdoc />
        public override void PlayBlockingSoundEffect()
        {
            WeaponItem blockingWeapon =
                m_player?.InventoryManager?.CurrentLeftHandWeapon;
            WorldSoundFXManager.Instance?.PlaySoundEffect(
                blockingWeapon?.BlockingSoundEffects,
                CharacterAudioSource);
        }
    }
}
