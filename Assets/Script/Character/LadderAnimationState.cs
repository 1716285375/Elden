namespace ZZ
{
    /// <summary>Identifies which hand owns the stable ladder idle pose.</summary>
    public enum LadderHandState : byte
    {
        Left = 0,
        Right = 1
    }

    /// <summary>Stable identifiers for owner-authored ladder presentation.</summary>
    public enum LadderAnimationState : byte
    {
        None = 0,
        EnterBottom = 1,
        EnterTop = 2,
        IdleLeft = 3,
        IdleRight = 4,
        ClimbUpLeft = 5,
        ClimbUpRight = 6,
        ClimbDownLeft = 7,
        ClimbDownRight = 8,
        ExitTopLeft = 9,
        ExitTopRight = 10,
        ExitBottomLeft = 11,
        ExitBottomRight = 12,
        SlideStart = 13,
        SlideMid = 14,
        SlideEnd = 15,
        JumpOffStart = 16,
        JumpOffMid = 17,
        JumpOffEnd = 18,
        FallStart = 19,
        FallLoop = 20
    }

    /// <summary>Centralizes deterministic ladder-segment and hand-pose rules.</summary>
    public static class LadderAnimationStateUtility
    {
        public static bool IsIdle(LadderAnimationState state)
        {
            return state == LadderAnimationState.IdleLeft ||
                state == LadderAnimationState.IdleRight;
        }

        public static bool IsSliding(LadderAnimationState state)
        {
            return state == LadderAnimationState.SlideStart ||
                state == LadderAnimationState.SlideMid ||
                state == LadderAnimationState.SlideEnd;
        }

        public static bool RequiresLadderLayerAfterClimb(
            LadderAnimationState state)
        {
            return state == LadderAnimationState.JumpOffStart ||
                state == LadderAnimationState.JumpOffMid ||
                state == LadderAnimationState.JumpOffEnd ||
                state == LadderAnimationState.FallStart ||
                state == LadderAnimationState.FallLoop;
        }

        public static LadderHandState GetIdleHand(LadderAnimationState state)
        {
            return state == LadderAnimationState.IdleRight
                ? LadderHandState.Right
                : LadderHandState.Left;
        }

        public static LadderAnimationState GetSegment(
            LadderHandState currentHand,
            float verticalInput)
        {
            if (verticalInput > 0f)
            {
                return currentHand == LadderHandState.Left
                    ? LadderAnimationState.ClimbUpRight
                    : LadderAnimationState.ClimbUpLeft;
            }

            if (verticalInput < 0f)
            {
                return currentHand == LadderHandState.Left
                    ? LadderAnimationState.ClimbDownRight
                    : LadderAnimationState.ClimbDownLeft;
            }

            return currentHand == LadderHandState.Left
                ? LadderAnimationState.IdleLeft
                : LadderAnimationState.IdleRight;
        }

        public static LadderAnimationState GetIdleAfterCompletedState(
            LadderAnimationState state)
        {
            switch (state)
            {
                case LadderAnimationState.EnterTop:
                case LadderAnimationState.ClimbUpRight:
                case LadderAnimationState.ClimbDownRight:
                    return LadderAnimationState.IdleRight;
                case LadderAnimationState.EnterBottom:
                case LadderAnimationState.ClimbUpLeft:
                case LadderAnimationState.ClimbDownLeft:
                default:
                    return LadderAnimationState.IdleLeft;
            }
        }
    }
}
