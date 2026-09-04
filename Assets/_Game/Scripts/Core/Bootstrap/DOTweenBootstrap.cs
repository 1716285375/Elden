using DG.Tweening;
using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Reserves DOTween's tween pool before the first tween is created so the pool never has to
    /// grow mid-presentation. DOTween itself auto-initialises from <c>DOTweenSettings</c>, so this
    /// class owns nothing else.
    /// </summary>
    public static class DOTweenBootstrap
    {
        private const int k_TweenerCapacity = 500;
        private const int k_SequenceCapacity = 80;

        /// <summary>
        /// Runs before any scene object, so it always precedes the first tween regardless of
        /// <c>DefaultExecutionOrder</c> values used elsewhere in the project.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            DOTween.SetTweensCapacity(k_TweenerCapacity, k_SequenceCapacity);
        }
    }
}
