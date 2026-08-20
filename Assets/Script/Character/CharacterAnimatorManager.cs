using UnityEngine;

namespace ZZ
{
    public class CharacterAnimatorManager : MonoBehaviour
    {
        private const float k_MovementParameterDampTime = 0.1f;

        private static readonly int s_horizontalParameter = Animator.StringToHash("Horizontal");
        private static readonly int s_verticalParameter = Animator.StringToHash("Vertical");

        [SerializeField] private Animator m_animator;

        protected virtual void Awake()
        {
            if (m_animator == null)
            {
                m_animator = GetComponent<Animator>();
            }

            if (m_animator == null)
            {
                m_animator = GetComponentInChildren<Animator>(true);
            }
        }

        public void Initialize(Animator characterAnimator)
        {
            m_animator = characterAnimator;
        }

        public void UpdateAnimatorMovementParameters(float horizontalValue, float verticalValue)
        {
            if (m_animator == null)
            {
                return;
            }

            m_animator.SetFloat(
                s_horizontalParameter,
                horizontalValue,
                k_MovementParameterDampTime,
                Time.deltaTime);
            m_animator.SetFloat(
                s_verticalParameter,
                verticalValue,
                k_MovementParameterDampTime,
                Time.deltaTime);
        }
    }
}
