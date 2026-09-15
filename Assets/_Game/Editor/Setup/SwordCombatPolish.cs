using System;
using UnityEditor;
using UnityEngine;

namespace ZZ.Editor
{
    /// <summary>Authors mixed sword chains and distinct stance pressure using existing attack animations.</summary>
    public static class SwordCombatPolish
    {
        private static readonly string[] s_weaponPaths =
        {
            "Assets/_Game/Data/Items/Weapons/Melee Weapons/Straight Sword.asset",
            "Assets/_Game/Data/Items/Weapons/Melee Weapons/Broadsword.asset"
        };

        [MenuItem("Tools/ZZ/Combat/Apply Sword Combat Polish")]
        public static void Apply()
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException("Stop Play Mode before authoring sword movesets.");
            }

            foreach (string path in s_weaponPaths)
            {
                WeaponItem weapon = AssetDatabase.LoadAssetAtPath<WeaponItem>(path);
                if (weapon == null || weapon.Moveset == null)
                {
                    throw new InvalidOperationException($"Missing migrated sword: {path}");
                }

                Link(weapon, AttackType.LightAttack01, AttackType.HeavyAttack01, "m_nextHeavyAttack");
                Link(weapon, AttackType.LightAttack02, AttackType.HeavyAttack02, "m_nextHeavyAttack");
                Link(weapon, AttackType.HeavyAttack01, AttackType.LightAttack03, "m_nextLightAttack");
                SetStance(weapon, AttackType.HeavyAttack01, 1.5f);
                SetStance(weapon, AttackType.HeavyAttack02, 1.75f);
                SetStance(weapon, AttackType.ChargedAttack01, 2.5f);
                SetStance(weapon, AttackType.HeavyJumpingAttack01, 2f);
                var moveset = new SerializedObject(weapon.Moveset);
                moveset.FindProperty("m_guardCounter").objectReferenceValue =
                    weapon.GetAttackDefinition(AttackType.HeavyAttack02);
                moveset.ApplyModifiedProperties();
                AssetDatabase.SaveAssetIfDirty(weapon.Moveset);
            }
        }

        private static void Link(WeaponItem weapon, AttackType source, AttackType destination, string field)
        {
            AttackDefinition attack = weapon.GetAttackDefinition(source);
            var data = new SerializedObject(attack);
            data.FindProperty(field).objectReferenceValue = weapon.GetAttackDefinition(destination);
            data.ApplyModifiedProperties();
            AssetDatabase.SaveAssetIfDirty(attack);
        }

        private static void SetStance(WeaponItem weapon, AttackType type, float multiplier)
        {
            AttackDefinition attack = weapon.GetAttackDefinition(type);
            var data = new SerializedObject(attack);
            data.FindProperty("m_stanceDamage").floatValue = weapon.BasePoiseDamage * multiplier;
            data.ApplyModifiedProperties();
            AssetDatabase.SaveAssetIfDirty(attack);
        }
    }
}
