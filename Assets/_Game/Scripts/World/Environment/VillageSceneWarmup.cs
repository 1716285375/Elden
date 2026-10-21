using System.Collections;
using UnityEngine;

namespace ZZ
{
    /// <summary>Spreads the village's first visible renderer submissions over loading frames.</summary>
    public sealed class VillageSceneWarmup : MonoBehaviour
    {
        private static VillageSceneWarmup s_instance;
        [SerializeField] private Renderer[] m_renderers = System.Array.Empty<Renderer>();

        public static bool IsLoading => s_instance != null && !s_instance.IsReady;
        public bool IsReady { get; private set; }

        private void Awake()
        {
            s_instance = this;
            foreach (Renderer renderer in m_renderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = false;
                }
            }
        }

        private IEnumerator Start()
        {
            for (int index = 0; index < m_renderers.Length; index++)
            {
                if (m_renderers[index] != null)
                {
                    m_renderers[index].enabled = true;
                }
                if ((index + 1) % 32 == 0)
                {
                    yield return null;
                }
            }
            yield return null;
            IsReady = true;
        }

        private void OnDestroy()
        {
            if (s_instance == this)
            {
                s_instance = null;
            }
        }
    }
}
