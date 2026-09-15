namespace ZZ
{
    /// <summary>Immutable offensive snapshot shared by detection and damage delivery.</summary>
    public readonly struct DamagePayload
    {
        public readonly float Physical;
        public readonly float Magic;
        public readonly float Fire;
        public readonly float Lightning;
        public readonly float Holy;
        public readonly float Poise;
        public readonly float Stance;
        public readonly float Poison;
        public readonly float Bleed;
        public readonly float Frost;

        public DamagePayload(float physical, float magic, float fire, float lightning, float holy,
            float poise, float stance, float poison, float bleed, float frost)
        {
            Physical = physical;
            Magic = magic;
            Fire = fire;
            Lightning = lightning;
            Holy = holy;
            Poise = poise;
            Stance = stance;
            Poison = poison;
            Bleed = bleed;
            Frost = frost;
        }
    }
}
