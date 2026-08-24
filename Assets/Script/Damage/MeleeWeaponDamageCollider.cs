namespace ZZ
{
    /// <summary>
    /// Marks a standard damage collider as belonging to a melee weapon model.
    /// </summary>
    public class MeleeWeaponDamageCollider : DamageCollider
    {
        /// <inheritdoc />
        protected override bool CheckForParry(CharacterManager damageTarget)
        {
            return base.CheckForParry(damageTarget);
        }
    }
}
