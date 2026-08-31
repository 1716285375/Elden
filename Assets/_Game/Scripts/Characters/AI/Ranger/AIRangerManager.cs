namespace ZZ
{
    /// <summary>Coordinates ranger-specific combat while reusing the shared AI state machine.</summary>
    [UnityEngine.RequireComponent(typeof(AIRangerCombatManager))]
    [UnityEngine.RequireComponent(typeof(AIRangerEquipmentManager))]
    public sealed class AIRangerManager : AICharacterManager
    {
        [UnityEngine.SerializeField] private PursuitMode m_rangerPursuitMode =
            PursuitMode.Run;
        [UnityEngine.SerializeField] private PursuitMode m_rangerCombatMode =
            PursuitMode.None;

        public AIRangerCombatManager RangerCombatManager { get; private set; }
        public AIRangerEquipmentManager RangerEquipmentManager { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            RangerCombatManager = GetComponent<AIRangerCombatManager>();
            RangerEquipmentManager = GetComponent<AIRangerEquipmentManager>();
        }

        internal override PursuitMode GetPursuitMode(AICharacterStateId stateId)
        {
            return stateId switch
            {
                AICharacterStateId.PursueTarget => m_rangerPursuitMode,
                AICharacterStateId.CombatStance => m_rangerCombatMode,
                _ => PursuitMode.None
            };
        }
    }
}
