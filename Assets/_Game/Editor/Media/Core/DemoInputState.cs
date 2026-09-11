using UnityEngine;

namespace ZZ.Demo
{
    /// <summary>
    /// One frame of input intent. Used by scripted demo clips today and by a future in-game
    /// replay or input-recording module, so it deliberately holds intents rather than device state.
    /// </summary>
    public struct DemoInputState
    {
        /// <summary>Move stick deflection. Y is forward, X is strafe.</summary>
        public Vector2 Move;

        /// <summary>Look stick deflection.</summary>
        public Vector2 Look;

        public bool Sprint;

        public bool Dodge;

        public bool Jump;

        public bool LightAttack;

        public bool HeavyAttack;

        public bool Block;

        public bool AshOfWar;

        public bool Sneak;

        public bool LockOn;

        public bool Interact;
    }
}
