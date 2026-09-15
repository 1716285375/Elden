using System;
using UnityEngine;

namespace ZZ
{
    /// <summary>Authored displacement of an attack: root motion stepping plus target correction.</summary>
    [Serializable]
    public struct MotionProfile
    {
        [Tooltip("World units travelled along the attacker's forward during the active phase.")]
        [SerializeField, Min(0f)] private float m_forwardStepDistance;

        [Tooltip("Seconds over which the step is applied. 0 applies the step instantly.")]
        [SerializeField, Min(0f)] private float m_stepDuration;

        [Tooltip("Closes the remaining gap to a locked target up to this distance. 0 disables correction.")]
        [SerializeField, Min(0f)] private float m_targetCorrectionDistance;

        [Tooltip("Whether character controller motion is allowed to continue under root motion.")]
        [SerializeField] private bool m_allowsControllerMotion;

        public float ForwardStepDistance => Mathf.Max(0f, m_forwardStepDistance);
        public float StepDuration => Mathf.Max(0f, m_stepDuration);
        public float TargetCorrectionDistance => Mathf.Max(0f, m_targetCorrectionDistance);
        public bool AllowsControllerMotion => m_allowsControllerMotion;

        public static MotionProfile None => new MotionProfile();
    }
}
