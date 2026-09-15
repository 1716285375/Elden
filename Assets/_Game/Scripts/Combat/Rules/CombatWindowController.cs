namespace ZZ
{
    /// <summary>Owns transient permissions for one action; each replacement starts with closed windows.</summary>
    public sealed class CombatWindowController
    {
        public CombatWindowType OpenWindows { get; private set; }

        public bool IsOpen(CombatWindowType window)
        {
            return window != CombatWindowType.None && (OpenWindows & window) == window;
        }

        public void Set(CombatWindowType window, bool isOpen)
        {
            OpenWindows = isOpen ? OpenWindows | window : OpenWindows & ~window;
        }

        public void Clear()
        {
            OpenWindows = CombatWindowType.None;
        }
    }
}
