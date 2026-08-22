using UnityEngine;

namespace ZZ
{
    /// <summary>Marks a server-owned enemy spawn location.</summary>
    public class AISpawnPoint : MonoBehaviour
    {
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.85f, 0.15f, 0.1f, 0.8f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up, 0.5f);
            Gizmos.DrawLine(
                transform.position,
                transform.position + transform.forward * 1.5f);
        }
    }
}
