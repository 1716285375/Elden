using UnityEngine;

namespace ZZ
{
    /// <summary>Loads the small shared combat particle family and presents local, short-lived effects.</summary>
    public static class CombatVfx
    {
        private static GameObject s_guard;
        private static GameObject s_parry;
        private static GameObject s_critical;

        /// <summary>Gets the authored critical burst used when a character has no explicit override.</summary>
        public static GameObject CriticalImpact => s_critical != null
            ? s_critical : s_critical = Resources.Load<GameObject>("Effects/Anime/Critical Impact");

        /// <summary>Shows a guard impact at the resolved contact point; does not alter combat state.</summary>
        public static void PlayGuard(Vector3 position)
        {
            if (s_guard == null)
            {
                s_guard = Resources.Load<GameObject>("Effects/Anime/Guard Spark");
            }
            Spawn(s_guard, position);
        }

        /// <summary>Shows a parry flash after the replicated parry has been accepted.</summary>
        public static void PlayParry(Vector3 position)
        {
            if (s_parry == null)
            {
                s_parry = Resources.Load<GameObject>("Effects/Anime/Parry Flash");
            }
            Spawn(s_parry, position);
        }

        private static void Spawn(GameObject prefab, Vector3 position)
        {
            if (Application.isPlaying && prefab != null)
            {
                Object.Instantiate(prefab, position, Quaternion.identity);
            }
        }
    }
}
