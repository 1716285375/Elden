using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Destroys a temporary object after a fixed lifetime so transient VFX and
    /// spawned objects clean themselves up.
    /// </summary>
    public class Utility_DestroyAfterTime : MonoBehaviour
    {
        [SerializeField, Min(0.001f)] private float m_destroyTime = 3f;

        private void Awake()
        {
            Destroy(gameObject, m_destroyTime);
        }
    }
}
