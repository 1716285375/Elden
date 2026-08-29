using UnityEngine;

/// <summary>
/// Spawns impact VFX only after weapon hit detection reports a real hit.
/// Swing-trail generation is deliberately kept separate.
/// </summary>
public class SwordSlashHitVFXSpawner : MonoBehaviour
{
    [SerializeField] private GameObject hitVfxPrefab;
    [SerializeField, Min(0.1f)] private float cleanupDelaySeconds = 2.0f;

    public void Spawn(RaycastHit hit)
    {
        Spawn(hit.point, hit.normal);
    }

    public void Spawn(Vector3 hitPoint, Vector3 hitNormal)
    {
        if (hitVfxPrefab == null)
        {
            Debug.LogWarning("Hit VFX prefab is not assigned.", this);
            return;
        }

        Vector3 normal =
            hitNormal.sqrMagnitude > 0.0001f
                ? hitNormal.normalized
                : Vector3.up;

        Vector3 up =
            Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > 0.98f
                ? Vector3.forward
                : Vector3.up;

        Quaternion rotation = Quaternion.LookRotation(normal, up);

        GameObject instance =
            Instantiate(hitVfxPrefab, hitPoint, rotation);

        foreach (ParticleSystem ps in
                 instance.GetComponentsInChildren<ParticleSystem>(true))
        {
            ps.Play(true);
        }

        Destroy(instance, cleanupDelaySeconds);
    }
}
