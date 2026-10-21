namespace ZZ
{
    /// <summary>Prevents an outgoing neutral-state callback from completing a newly requested action.</summary>
    public struct AnimationActionGate
    {
        private int m_pendingState;
        public bool CanComplete => m_pendingState == 0;
        public void Request(int stateHash) => m_pendingState = stateHash;
        public void Cancel() => m_pendingState = 0;

        public void Observe(int currentState, int nextState)
        {
            if (m_pendingState == currentState || m_pendingState == nextState)
            {
                m_pendingState = 0;
            }
        }
    }
}
