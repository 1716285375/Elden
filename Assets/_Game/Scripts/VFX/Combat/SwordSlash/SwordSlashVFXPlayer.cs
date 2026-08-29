using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Procedural sword-sweep trail.
///
/// Unlike a billboard particle/card, this trail is built from the actual
/// BladeBase and BladeTip world positions every frame. The generated ribbon
/// therefore lies on the real plane swept by the sword.
/// </summary>
public class SwordSlashVFXPlayer : MonoBehaviour
{
    [Header("Blade Tracking")]
    [SerializeField] private Transform bladeBase;
    [SerializeField] private Transform bladeTip;

    [Tooltip("Ignore the lowest part of the blade so the trail does not start inside the hilt.")]
    [SerializeField, Range(0f, 0.5f)] private float baseInset = 0.12f;

    [Tooltip("Small extension beyond the physical tip for a stylized slash silhouette.")]
    [SerializeField, Range(0f, 0.25f)] private float tipExtension = 0.06f;

    [Header("Trail Shape")]
    [SerializeField, Min(0.03f)] private float trailLifetime = 0.18f;
    [SerializeField, Range(6, 64)] private int maxSamples = 28;
    [SerializeField, Min(0.001f)] private float minSampleDistance = 0.012f;

    [Header("Rendering")]
    [Tooltip("Optional. If empty, the script reuses a material from the existing child Particle System.")]
    [SerializeField] private Material trailMaterial;

    [SerializeField] private int sortingOrder = 20;

    [Tooltip("Disable the old particle-card renderers so they cannot appear as a horizontal 2D sheet.")]
    [SerializeField] private bool hideLegacyParticleRenderers = true;

    [Tooltip("Optional accent particles such as sparks/noise. Keep off while validating the main slash trail.")]
    [SerializeField] private bool playAccentParticles = false;

    [SerializeField] private ParticleSystem[] particleSystems;

    private struct TrailSample
    {
        public Vector3 Base;
        public Vector3 Tip;
        public float Time;
    }

    private readonly List<TrailSample> m_samples = new List<TrailSample>(32);

    private GameObject m_runtimeTrailObject;
    private Mesh m_mesh;
    private MeshRenderer m_meshRenderer;
    private ParticleSystemRenderer[] m_particleRenderers;

    private bool m_sampling;
    private bool m_initialized;

    private void Awake()
    {
        Initialize();
    }

    private void LateUpdate()
    {
        Initialize();

        if (m_sampling)
        {
            TryAddSample();
        }

        RemoveExpiredSamples();
        RebuildMesh();

        if (!m_sampling && m_samples.Count == 0)
        {
            SetTrailVisible(false);
        }
    }

    private void OnDestroy()
    {
        if (m_mesh != null)
        {
            Destroy(m_mesh);
        }

        if (m_runtimeTrailObject != null)
        {
            Destroy(m_runtimeTrailObject);
        }
    }

    public void ConfigureBlade(Transform newBladeBase, Transform newBladeTip)
    {
        bladeBase = newBladeBase;
        bladeTip = newBladeTip;
        Initialize();
    }

    public void Play()
    {
        Initialize();

        if (bladeBase == null || bladeTip == null)
        {
            Debug.LogWarning(
                "Sword slash trail needs BladeBase and BladeTip references.",
                this);
            return;
        }

        m_samples.Clear();
        m_sampling = true;
        SetTrailVisible(true);

        // Record the exact starting pose before the sword begins crossing the arc.
        AddCurrentSample(force: true);

        if (playAccentParticles)
        {
            PlayAccentParticles();
        }
        else
        {
            StopAccentParticles(clear: true);
        }
    }

    /// <summary>
    /// clearTrail = false:
    /// stop recording new sword positions, but keep the existing ribbon
    /// alive until trailLifetime fades it out.
    ///
    /// clearTrail = true:
    /// immediately clear everything.
    /// </summary>
    public void Stop(bool clearTrail = true)
    {
        Initialize();
        m_sampling = false;

        if (clearTrail)
        {
            m_samples.Clear();
            RebuildMesh();
            SetTrailVisible(false);
        }

        StopAccentParticles(clearTrail);
    }

    // Compatibility overload for older code.
    public void Play(Transform anchor, float scale)
    {
        Play();
    }

    // Compatibility method for older code.
    public void SetScale(float scale)
    {
        // Intentionally unused.
        // The procedural trail width is derived from the real blade endpoints,
        // so changing transform scale would make the trail detach from the sword.
    }

    // Compatibility method for older code.
    public void BindToAnchor(Transform anchor)
    {
        // Intentionally unused.
        // The trail is generated in world space from BladeBase/BladeTip.
    }

    private void Initialize()
    {
        if (m_initialized)
        {
            return;
        }

        m_initialized = true;

        if (particleSystems == null || particleSystems.Length == 0)
        {
            particleSystems = GetComponentsInChildren<ParticleSystem>(true);
        }

        m_particleRenderers =
            GetComponentsInChildren<ParticleSystemRenderer>(true);

        Material resolvedMaterial = trailMaterial;

        if (resolvedMaterial == null && m_particleRenderers != null)
        {
            foreach (ParticleSystemRenderer renderer in m_particleRenderers)
            {
                if (renderer != null && renderer.sharedMaterial != null)
                {
                    resolvedMaterial = renderer.sharedMaterial;
                    break;
                }
            }
        }

        CreateRuntimeTrailObject(resolvedMaterial);
        ConfigureLegacyParticles();
    }

    private void CreateRuntimeTrailObject(Material resolvedMaterial)
    {
        m_runtimeTrailObject =
            new GameObject($"{name}_RuntimeBladeTrail");

        // Keep this at world identity so old trail vertices remain in world
        // space even while the sword keeps rotating/recovering.
        Transform trailTransform = m_runtimeTrailObject.transform;
        trailTransform.SetParent(null, false);
        trailTransform.position = Vector3.zero;
        trailTransform.rotation = Quaternion.identity;
        trailTransform.localScale = Vector3.one;

        MeshFilter meshFilter =
            m_runtimeTrailObject.AddComponent<MeshFilter>();

        m_meshRenderer =
            m_runtimeTrailObject.AddComponent<MeshRenderer>();

        m_mesh = new Mesh
        {
            name = $"{name}_ProceduralSlashMesh"
        };
        m_mesh.MarkDynamic();

        meshFilter.sharedMesh = m_mesh;

        if (resolvedMaterial != null)
        {
            m_meshRenderer.sharedMaterial = resolvedMaterial;
        }
        else
        {
            Debug.LogWarning(
                "No slash material found. Assign Trail Material on SwordSlashVFXPlayer.",
                this);
        }

        m_meshRenderer.sortingOrder = sortingOrder;
        SetTrailVisible(false);
    }

    private void ConfigureLegacyParticles()
    {
        if (particleSystems != null)
        {
            foreach (ParticleSystem ps in particleSystems)
            {
                if (ps == null)
                {
                    continue;
                }

                ParticleSystem.MainModule main = ps.main;
                main.playOnAwake = false;

                ps.Stop(
                    true,
                    ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        if (!hideLegacyParticleRenderers || m_particleRenderers == null)
        {
            return;
        }

        // The old slash prefab is built from camera-facing particle cards.
        // Those cards are the source of the "horizontal 2D sheet" look.
        foreach (ParticleSystemRenderer renderer in m_particleRenderers)
        {
            if (renderer != null)
            {
                renderer.enabled = false;
            }
        }
    }

    private void TryAddSample()
    {
        if (bladeBase == null || bladeTip == null)
        {
            return;
        }

        if (m_samples.Count == 0)
        {
            AddCurrentSample(force: true);
            return;
        }

        GetCurrentBladeSegment(out Vector3 currentBase, out Vector3 currentTip);

        TrailSample previous = m_samples[m_samples.Count - 1];

        float baseDelta = Vector3.Distance(previous.Base, currentBase);
        float tipDelta = Vector3.Distance(previous.Tip, currentTip);

        if (Mathf.Max(baseDelta, tipDelta) >= minSampleDistance)
        {
            AddSample(currentBase, currentTip);
        }
    }

    private void AddCurrentSample(bool force)
    {
        if (bladeBase == null || bladeTip == null)
        {
            return;
        }

        GetCurrentBladeSegment(out Vector3 currentBase, out Vector3 currentTip);

        if (!force && m_samples.Count > 0)
        {
            TrailSample previous = m_samples[m_samples.Count - 1];

            if (Vector3.Distance(previous.Base, currentBase) < minSampleDistance &&
                Vector3.Distance(previous.Tip, currentTip) < minSampleDistance)
            {
                return;
            }
        }

        AddSample(currentBase, currentTip);
    }

    private void AddSample(Vector3 currentBase, Vector3 currentTip)
    {
        if (m_samples.Count >= maxSamples)
        {
            m_samples.RemoveAt(0);
        }

        m_samples.Add(new TrailSample
        {
            Base = currentBase,
            Tip = currentTip,
            Time = Time.time
        });
    }

    private void GetCurrentBladeSegment(
        out Vector3 trailBase,
        out Vector3 trailTip)
    {
        Vector3 physicalBase = bladeBase.position;
        Vector3 physicalTip = bladeTip.position;

        Vector3 bladeVector = physicalTip - physicalBase;

        trailBase =
            physicalBase + bladeVector * Mathf.Clamp01(baseInset);

        trailTip =
            physicalTip + bladeVector * Mathf.Max(0f, tipExtension);
    }

    private void RemoveExpiredSamples()
    {
        float now = Time.time;

        while (m_samples.Count > 0 &&
               now - m_samples[0].Time > trailLifetime)
        {
            m_samples.RemoveAt(0);
        }
    }

    private void RebuildMesh()
    {
        if (m_mesh == null)
        {
            return;
        }

        m_mesh.Clear();

        int sampleCount = m_samples.Count;
        if (sampleCount < 2)
        {
            return;
        }

        int vertexCount = sampleCount * 2;

        Vector3[] vertices = new Vector3[vertexCount];
        Vector2[] uvs = new Vector2[vertexCount];
        Color[] colors = new Color[vertexCount];

        int[] triangles = new int[(sampleCount - 1) * 6];

        float now = Time.time;

        for (int i = 0; i < sampleCount; i++)
        {
            TrailSample sample = m_samples[i];

            int baseIndex = i * 2;
            int tipIndex = baseIndex + 1;

            // Runtime trail object has identity transform at world origin,
            // therefore world positions can be used directly as mesh vertices.
            vertices[baseIndex] = sample.Base;
            vertices[tipIndex] = sample.Tip;

            float alongTrail =
                sampleCount <= 1
                    ? 1f
                    : (float)i / (sampleCount - 1);

            uvs[baseIndex] = new Vector2(alongTrail, 0f);
            uvs[tipIndex] = new Vector2(alongTrail, 1f);

            float age01 =
                Mathf.Clamp01((now - sample.Time) / trailLifetime);

            float alpha = 1f - age01;

            // Fade the very oldest edge a little more softly.
            alpha *= Mathf.SmoothStep(0f, 1f, alongTrail);

            Color color = new Color(1f, 1f, 1f, alpha);
            colors[baseIndex] = color;
            colors[tipIndex] = color;
        }

        int triangleIndex = 0;

        for (int i = 0; i < sampleCount - 1; i++)
        {
            int currentBase = i * 2;
            int currentTip = currentBase + 1;
            int nextBase = currentBase + 2;
            int nextTip = currentBase + 3;

            // Front face.
            triangles[triangleIndex++] = currentBase;
            triangles[triangleIndex++] = currentTip;
            triangles[triangleIndex++] = nextTip;

            triangles[triangleIndex++] = currentBase;
            triangles[triangleIndex++] = nextTip;
            triangles[triangleIndex++] = nextBase;
        }

        m_mesh.vertices = vertices;
        m_mesh.uv = uvs;
        m_mesh.colors = colors;
        m_mesh.triangles = triangles;
        m_mesh.RecalculateBounds();
        m_mesh.RecalculateNormals();
    }

    private void SetTrailVisible(bool visible)
    {
        if (m_meshRenderer != null)
        {
            m_meshRenderer.enabled = visible;
        }
    }

    private void PlayAccentParticles()
    {
        if (particleSystems == null)
        {
            return;
        }

        foreach (ParticleSystem ps in particleSystems)
        {
            if (ps == null)
            {
                continue;
            }

            ps.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear);

            ps.Play(true);
        }
    }

    private void StopAccentParticles(bool clear)
    {
        if (particleSystems == null)
        {
            return;
        }

        ParticleSystemStopBehavior behavior =
            clear
                ? ParticleSystemStopBehavior.StopEmittingAndClear
                : ParticleSystemStopBehavior.StopEmitting;

        foreach (ParticleSystem ps in particleSystems)
        {
            if (ps != null)
            {
                ps.Stop(true, behavior);
            }
        }
    }
}
