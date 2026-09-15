namespace ZZ
{
    /// <summary>
    /// Marks a standard damage collider as belonging to a melee weapon model.
    /// </summary>
    public class MeleeWeaponDamageCollider : DamageCollider
    {
        protected override bool UsesWeaponTrace => true;

        /// <inheritdoc />
        public override bool UsesActionSnapshot => true;
    }
}
