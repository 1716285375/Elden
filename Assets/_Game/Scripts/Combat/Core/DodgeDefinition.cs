using System;
using UnityEngine;

namespace ZZ
{
    /// <summary>Identifies which authored dodge variant a definition describes.</summary>
    public enum DodgeKind
    {
        Light = 0,
        Medium = 1,
        Heavy = 2,
        Backstep = 3,
        Skill = 4
    }

    /// <summary>Authored dodge windows. Replaces the normalized-time constants hard-coded in clips.</summary>
    [GameAsset(FileName = "Dodge Definition", MenuName = "ZZ/Combat/Dodge Definition")]
    public sealed class DodgeDefinition : ScriptableObject
    {
        [SerializeField] private DodgeKind m_kind = DodgeKind.Medium;

        [Header("Windows (normalized)")]
        [SerializeField] private ActionWindow m_invulnerability = new ActionWindow(0.08f, 0.55f);
        [SerializeField] private ActionWindow m_attackCancel = new ActionWindow(0.62f, 1f);
        [SerializeField] private ActionWindow m_moveCancel = new ActionWindow(0.72f, 1f);

        [Header("Cost")]
        [SerializeField, Min(0f)] private float m_staminaCost;

        public DodgeKind Kind => m_kind;
        public ActionWindow Invulnerability => m_invulnerability;
        public ActionWindow AttackCancel => m_attackCancel;
        public ActionWindow MoveCancel => m_moveCancel;
        public float StaminaCost => Mathf.Max(0f, m_staminaCost);
    }
}
