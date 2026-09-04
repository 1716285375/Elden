using UnityEngine;

namespace ZZ
{
    /// <summary>Exits the running build, or stops Play Mode when running inside the Editor.</summary>
    public static class GameExit
    {
        /// <summary>Quits the application immediately, discarding unsaved progress.</summary>
        public static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
