using UnityEngine;

namespace ZZ
{
    /// <summary>Hides the gameplay HUD for the lifetime of an enabled menu window.</summary>
    public class PlayerUIToggleHUD : MonoBehaviour
    {
        private void OnEnable()
        {
            PlayerUIManager.Instance?.PlayerUIHUDManager?.HideHUD();
        }

        private void OnDisable()
        {
            PlayerUIManager.Instance?.PlayerUIHUDManager?.ShowHUD();
        }
    }
}
