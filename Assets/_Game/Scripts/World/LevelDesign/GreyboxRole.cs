namespace ZZ
{
    /// <summary>
    /// Classifies one greybox volume so the generator can give it a material that
    /// makes its spatial function readable at a glance.
    /// </summary>
    public enum GreyboxRole
    {
        /// <summary>Neutral structure that is neither clearly walkable nor decorative.</summary>
        Base,

        /// <summary>A surface the player is expected to stand on.</summary>
        Walkable,

        /// <summary>Solid volume the player cannot pass.</summary>
        Blocking,

        /// <summary>Low cover roughly chest height, see-over but not walk-over.</summary>
        Cover,

        /// <summary>Scene dressing placeholder, visually distinct from structure.</summary>
        Prop,

        /// <summary>High-contrast gameplay marker such as a spawn or objective.</summary>
        Marker,

        /// <summary>Transparent volume that only reports overlap.</summary>
        Trigger
    }
}
