using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Sword-slash preview driver.
///
/// Coordinate intent for this test scene:
/// - Camera looks toward the gray wall.
/// - Sword is held roughly vertical in the idle pose.
/// - Left click performs a fixed upper-left -> lower-right diagonal cut.
/// - The slash VFX records the blade's real swept path.
/// </summary>
public class SwordSlashTestDriver : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform swordRig;
    [SerializeField] private Transform bladeBase;
    [SerializeField] private Transform bladeTip;
    [SerializeField] private Transform slashAnchor;
    [SerializeField] private SwordSlashVFXPlayer swingVfxPlayer;
    [SerializeField] private SwordSlashHitVFXSpawner hitVfxSpawner;
    [SerializeField] private Camera previewCamera;

    [Header("Preview Camera")]
    [SerializeField] private bool configurePreviewCameraOnStart = true;
    [SerializeField] private Vector3 previewFocusOffset = new Vector3(0f, 0.08f, 0f);
    [SerializeField, Min(0.5f)] private float cameraDistance = 3.2f;
    [SerializeField, Range(15f, 60f)] private float cameraFieldOfView = 30f;

    [Header("Auto Preview")]
    [SerializeField] private bool autoPlayOnStart = false;
    [SerializeField, Min(0.5f)] private float autoPlayIntervalSeconds = 2.2f;

    [Header("Light Slash - LMB")]
    [Tooltip("Positive Z angle puts the sword tip toward the upper-left in this scene.")]
    [SerializeField] private float lightStartAngle = 55f;

    [Tooltip("Negative Z angle finishes the sword toward the lower-right.")]
    [SerializeField] private float lightEndAngle = -115f;

    [SerializeField, Min(0.01f)] private float lightWindupDuration = 0.10f;
    [SerializeField, Min(0.01f)] private float lightSwingDuration = 0.30f;
    [SerializeField, Min(0.01f)] private float lightRecoveryDuration = 0.16f;

    [Header("Heavy Slash - RMB")]
    [SerializeField] private float heavyStartAngle = 72f;
    [SerializeField] private float heavyEndAngle = -132f;
    [SerializeField, Min(0.01f)] private float heavyWindupDuration = 0.18f;
    [SerializeField, Min(0.01f)] private float heavySwingDuration = 0.42f;
    [SerializeField, Min(0.01f)] private float heavyRecoveryDuration = 0.22f;

    [Header("Hit VFX")]
    [Tooltip("Preview-only. Leave off unless you specifically want to test an impact effect.")]
    [SerializeField] private bool spawnTestHitVfx = false;

    private enum SwingPhase
    {
        Idle,
        Windup,
        Swing,
        Recovery
    }

    private struct AttackProfile
    {
        public float StartAngle;
        public float EndAngle;
        public float WindupDuration;
        public float SwingDuration;
        public float RecoveryDuration;
    }

    private SwingPhase m_phase = SwingPhase.Idle;
    private AttackProfile m_attack;
    private Quaternion m_idleRotation;
    private float m_phaseTime;
    private float m_recoveryStartAngle;
    private float m_nextAutoTime;

    private void Awake()
    {
        ResolveReferences();

        if (swordRig != null)
        {
            m_idleRotation = swordRig.localRotation;
        }

        if (swingVfxPlayer != null)
        {
            swingVfxPlayer.ConfigureBlade(bladeBase, bladeTip);
        }
    }

    private void Start()
    {
        if (configurePreviewCameraOnStart)
        {
            ConfigurePreviewCamera();
        }

        if (autoPlayOnStart)
        {
            m_nextAutoTime = Time.unscaledTime + 0.75f;
        }
    }

    private void Update()
    {
        HandleInput();
        HandleAutoPreview();
        AdvanceAttack();
    }

    private void OnDisable()
    {
        ResetAttack();
    }

    private void HandleInput()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            return;
        }

        if (mouse.leftButton.wasPressedThisFrame)
        {
            BeginLightSlash();
        }

        if (mouse.rightButton.wasPressedThisFrame)
        {
            BeginHeavySlash();
        }
    }

    private void HandleAutoPreview()
    {
        if (!autoPlayOnStart || m_phase != SwingPhase.Idle)
        {
            return;
        }

        if (Time.unscaledTime < m_nextAutoTime)
        {
            return;
        }

        BeginLightSlash();
        m_nextAutoTime = Time.unscaledTime + autoPlayIntervalSeconds;
    }

    private void BeginLightSlash()
    {
        BeginAttack(new AttackProfile
        {
            StartAngle = lightStartAngle,
            EndAngle = lightEndAngle,
            WindupDuration = lightWindupDuration,
            SwingDuration = lightSwingDuration,
            RecoveryDuration = lightRecoveryDuration
        });
    }

    private void BeginHeavySlash()
    {
        BeginAttack(new AttackProfile
        {
            StartAngle = heavyStartAngle,
            EndAngle = heavyEndAngle,
            WindupDuration = heavyWindupDuration,
            SwingDuration = heavySwingDuration,
            RecoveryDuration = heavyRecoveryDuration
        });
    }

    private void BeginAttack(AttackProfile profile)
    {
        if (m_phase != SwingPhase.Idle || swordRig == null)
        {
            return;
        }

        m_attack = profile;
        m_phase = SwingPhase.Windup;
        m_phaseTime = 0f;

        if (swingVfxPlayer != null)
        {
            // Windup is preparation only. No slash trail yet.
            swingVfxPlayer.Stop(true);
        }
    }

    private void AdvanceAttack()
    {
        if (m_phase == SwingPhase.Idle || swordRig == null)
        {
            return;
        }

        m_phaseTime += Time.deltaTime;

        switch (m_phase)
        {
            case SwingPhase.Windup:
                AdvanceWindup();
                break;

            case SwingPhase.Swing:
                AdvanceSwing();
                break;

            case SwingPhase.Recovery:
                AdvanceRecovery();
                break;
        }
    }

    private void AdvanceWindup()
    {
        float t = Mathf.Clamp01(m_phaseTime / m_attack.WindupDuration);

        // Move from vertical idle to the upper-left starting pose.
        float angle = Mathf.Lerp(0f, m_attack.StartAngle, Smooth01(t));
        ApplySwordAngle(angle);

        if (t < 1f)
        {
            return;
        }

        m_phase = SwingPhase.Swing;
        m_phaseTime = 0f;

        // Start recording exactly when the real cut begins.
        if (swingVfxPlayer != null)
        {
            swingVfxPlayer.Play();
        }
    }

    private void AdvanceSwing()
    {
        float t = Mathf.Clamp01(m_phaseTime / m_attack.SwingDuration);

        // A slightly aggressive curve keeps the blade fast through the middle
        // of the cut while still easing into/out of the endpoints.
        float motionT = FastMiddleEase(t);
        float angle = Mathf.Lerp(m_attack.StartAngle, m_attack.EndAngle, motionT);

        ApplySwordAngle(angle);

        if (t < 1f)
        {
            return;
        }

        m_recoveryStartAngle = m_attack.EndAngle;

        // Stop sampling new blade positions but let the trail fade naturally.
        if (swingVfxPlayer != null)
        {
            swingVfxPlayer.Stop(false);
        }

        if (spawnTestHitVfx)
        {
            SpawnTestHit();
        }

        m_phase = SwingPhase.Recovery;
        m_phaseTime = 0f;
    }

    private void AdvanceRecovery()
    {
        float t = Mathf.Clamp01(m_phaseTime / m_attack.RecoveryDuration);
        float angle = Mathf.Lerp(m_recoveryStartAngle, 0f, Smooth01(t));

        ApplySwordAngle(angle);

        if (t < 1f)
        {
            return;
        }

        swordRig.localRotation = m_idleRotation;
        m_phase = SwingPhase.Idle;
        m_phaseTime = 0f;
    }

    private void ApplySwordAngle(float zAngle)
    {
        // The gray wall and the camera define the XY presentation plane.
        // Rotating around local Z makes the vertical sword perform a visible
        // upper-left -> lower-right diagonal cut in that plane.
        swordRig.localRotation =
            m_idleRotation * Quaternion.AngleAxis(zAngle, Vector3.forward);
    }

    private void SpawnTestHit()
    {
        if (hitVfxSpawner == null || bladeTip == null)
        {
            return;
        }

        Vector3 hitPoint = bladeTip.position;

        Vector3 bladeDirection =
            bladeBase != null
                ? (bladeTip.position - bladeBase.position).normalized
                : swordRig.up;

        // For this front-facing test scene, derive a reasonable impact normal.
        Vector3 hitNormal = Vector3.Cross(Vector3.forward, bladeDirection).normalized;

        if (hitNormal.sqrMagnitude < 0.001f)
        {
            hitNormal = -Vector3.forward;
        }

        hitVfxSpawner.Spawn(hitPoint, hitNormal);
    }

    private void ConfigurePreviewCamera()
    {
        if (previewCamera == null || swordRig == null)
        {
            return;
        }

        // Focus on the center of the slash arc, not on the sword tip.
        Vector3 focusPoint =
            swordRig.position +
            swordRig.TransformVector(previewFocusOffset);

        Transform cameraTransform = previewCamera.transform;
        cameraTransform.position = focusPoint + Vector3.back * cameraDistance;
        cameraTransform.rotation =
            Quaternion.LookRotation(focusPoint - cameraTransform.position, Vector3.up);

        previewCamera.fieldOfView = cameraFieldOfView;
    }

    private void ResetAttack()
    {
        m_phase = SwingPhase.Idle;
        m_phaseTime = 0f;

        if (swordRig != null)
        {
            swordRig.localRotation = m_idleRotation;
        }

        if (swingVfxPlayer != null)
        {
            swingVfxPlayer.Stop(true);
        }
    }

    private void ResolveReferences()
    {
        if (swordRig == null)
        {
            swordRig = transform.Find("SwordRig");
        }

        if (swordRig != null)
        {
            if (bladeBase == null)
            {
                bladeBase = swordRig.Find("Sword/BladeBase");
            }

            if (bladeTip == null)
            {
                bladeTip = swordRig.Find("Sword/BladeTip");
            }

            if (slashAnchor == null)
            {
                slashAnchor = swordRig.Find("Sword/VFX_SlashAnchor");
            }
        }

        if (swingVfxPlayer == null && slashAnchor != null)
        {
            swingVfxPlayer =
                slashAnchor.GetComponentInChildren<SwordSlashVFXPlayer>(true);
        }

        if (swingVfxPlayer == null)
        {
            swingVfxPlayer =
                GetComponentInChildren<SwordSlashVFXPlayer>(true);
        }

        if (hitVfxSpawner == null)
        {
            hitVfxSpawner = GetComponent<SwordSlashHitVFXSpawner>();
        }

        if (previewCamera == null)
        {
            previewCamera = Camera.main;
        }
    }

    private static float Smooth01(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    private static float FastMiddleEase(float t)
    {
        t = Mathf.Clamp01(t);

        // SmoothStep with a slightly faster center.
        float smooth = t * t * (3f - 2f * t);
        return Mathf.Lerp(t, smooth, 0.65f);
    }
}
