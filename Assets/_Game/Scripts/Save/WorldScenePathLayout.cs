namespace ZZ
{
    /// <summary>
    /// Central registry for the on-disk layout of the streaming world Scenes.
    /// </summary>
    public static class WorldScenePathLayout
    {
        public const string ScenesRoot = "Assets/Scenes";
        public const string RegionsFolder = LevelFolder + "/Regions";
        public const string MasterSceneName = "SCN_LV01_AbandonedMonastery";
        public const string LevelFolderName = "LV01_AbandonedMonastery";
        public const string LevelFolder =
            ScenesRoot + "/Levels/" + LevelFolderName;
        public const string MasterScenePath =
            LevelFolder + "/" + MasterSceneName + ".unity";
        public const string MainMenuScenePath =
            ScenesRoot + "/Frontend/SCN_MainMenu.unity";
        public const string SharedOcclusionFolder =
            LevelFolder + "/Shared/Occlusion";
        public const string SharedLightingBakedFolder =
            LevelFolder + "/Shared/Lighting/Baked";

        private static readonly string[] s_regionFolderNames =
        {
            "R01_MonasteryOutskirts",
            "R02_MonasteryInterior",
            "R03_Catacombs",
            "R04_BellTower",
            "R05_BossSanctum"
        };

        private static readonly string[] s_sliceNames =
        {
            "Base",
            "Props",
            "Effects",
            "Spawners"
        };

        public static int RegionCount => s_regionFolderNames.Length;

        public static string GetRegionFolderName(int regionIndex) =>
            s_regionFolderNames[regionIndex];

        public static string GetRegionFolderPath(int regionIndex) =>
            $"{LevelFolder}/Regions/{s_regionFolderNames[regionIndex]}";

        public static string GetSceneID(int regionIndex, int sliceIndex) =>
            $"SCN_LV01_R{regionIndex + 1:00}_A01_{s_sliceNames[sliceIndex]}";

        public static string GetScenePath(int regionIndex, int sliceIndex) =>
            $"{GetRegionFolderPath(regionIndex)}/{GetSceneID(regionIndex, sliceIndex)}.unity";

        /// <summary>
        /// Resolves the folder path of a region Scene ID such as
        /// SCN_LV01_R02_A01_Props.
        /// </summary>
        public static string GetScenePath(string sceneID) =>
            $"{GetRegionFolderPath(GetRegionIndexFromSceneID(sceneID))}/{sceneID}.unity";

        public static int GetRegionIndexFromSceneID(string sceneID) =>
            int.Parse(sceneID.Substring(9, 3).Substring(1)) - 1;
    }
}
