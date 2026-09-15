namespace ZZ
{
    public static class ParryRules
    {
        public static bool CanParry(int attackLevel, int parryStrength, float facingDot)
        {
            return attackLevel > 0 && attackLevel <= parryStrength && facingDot > 0.3f;
        }
    }
}
