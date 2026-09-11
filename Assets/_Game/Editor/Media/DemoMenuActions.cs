using UnityEngine;

namespace ZZ.Editor
{
    /// <summary>
    /// UI actions a demo clip can fire. Menus are driven through their own public API rather than
    /// synthesised clicks, so a clip survives button renames as long as the action does.
    /// </summary>
    internal static class DemoMenuActions
    {
        public static void StartNetworkAsHost()
        {
            Object.FindFirstObjectByType<TitleScreenManager>()?.StartNetworkAsHost();
        }

        public static void PressStart()
        {
            Object.FindFirstObjectByType<TitleScreenManager>()?.PressStart();
        }

        public static void OpenCharacterCreation()
        {
            Object.FindFirstObjectByType<TitleScreenManager>()?.StartNewGame();
        }

        /// <summary>
        /// Opens a creation submenu with a different option highlighted. The commit methods close the
        /// submenu again, so a preview is what actually stays on screen long enough to record.
        /// </summary>
        public static void PreviewClass(int classIndex)
        {
            TitleScreenCharacterCreationManager creation = FindCharacterCreation();
            creation?.OpenClassMenu();
            creation?.PreviewClass(classIndex);
        }

        /// <inheritdoc cref="PreviewClass"/>
        public static void PreviewHairstyle(int hairstyleID)
        {
            TitleScreenCharacterCreationManager creation = FindCharacterCreation();
            creation?.OpenHairMenu();
            creation?.PreviewHairstyle(hairstyleID);
        }

        public static void SelectClass(int classIndex)
        {
            FindCharacterCreation()?.SelectClass(classIndex);
        }

        public static void SelectHairstyle(int hairstyleID)
        {
            FindCharacterCreation()?.SelectHairstyle(hairstyleID);
        }

        public static void StartCreatedGame()
        {
            FindCharacterCreation()?.StartGame();
        }

        public static void OpenInGameCharacterMenu()
        {
            PlayerUIManager.Instance?.PlayerUICharacterMenuManager?.OpenCharacterMenu();
        }

        public static void OpenEquipmentMenu()
        {
            PlayerUIManager.Instance?.PlayerUIEquipmentManager?.OpenEquipmentManagerMenu();
        }

        public static void SelectEquipmentSlot(int equipmentSlot)
        {
            PlayerUIManager.Instance?.PlayerUIEquipmentManager?.SelectEquipmentSlot(equipmentSlot);
        }

        public static void CloseInGameMenus()
        {
            PlayerUIManager.Instance?.CloseAllMenuWindows();
        }

        private static TitleScreenCharacterCreationManager FindCharacterCreation()
        {
            return Object.FindFirstObjectByType<TitleScreenCharacterCreationManager>();
        }
    }
}
