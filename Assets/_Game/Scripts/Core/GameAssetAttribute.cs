using System;

namespace ZZ
{
    /// <summary>Registers a ScriptableObject in the resource creation center without adding a Create menu.</summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class GameAssetAttribute : Attribute
    {
        /// <summary>Gets or sets the category and display name separated by slashes.</summary>
        public string MenuName { get; set; }

        /// <summary>Gets or sets the suggested filename for a new asset.</summary>
        public string FileName { get; set; }
    }
}
