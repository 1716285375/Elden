using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ZZ
{
    /// <summary>Build-included references to the original action bindings and generated icon atlas.</summary>
    public sealed class InputHintCatalog : ScriptableObject
    {
        [SerializeField] private InputActionAsset m_actions;
        [SerializeField] private TMP_SpriteAsset m_icons;

        public InputActionAsset Actions => m_actions;
        public TMP_SpriteAsset Icons => m_icons;
    }
}
