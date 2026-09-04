using UnityEngine;

namespace ZZ
{
    /// <summary>Displays one accumulation channel only while its value is positive.</summary>
    public class UIBuildupBar : UIStatBar
    {
        [SerializeField] private Buildup m_buildupType;

        /// <summary>Gets the accumulation channel represented by this reusable bar.</summary>
        public Buildup BuildupType => m_buildupType;

        /// <summary>
        /// Buildup tracks its value exactly: the bar hides the instant the value reaches zero, so
        /// an easing fill would lag behind the very state that drives its visibility.
        /// </summary>
        protected override bool ShouldTweenValue => false;

        /// <summary>Updates the current value and toggles the bar's visibility.</summary>
        public void SetBuildupAmount(float buildupAmount)
        {
            float sanitizedAmount = Mathf.Max(0f, buildupAmount);
            SetStat(sanitizedAmount);
            gameObject.SetActive(sanitizedAmount > 0f);
        }

        /// <summary>Updates the shared buildup range without forcing the bar visible.</summary>
        public void SetMaxBuildupValue(float maximumBuildup)
        {
            bool wasActive = gameObject.activeSelf;
            SetMaxStat(maximumBuildup);
            gameObject.SetActive(wasActive);
        }
    }
}
