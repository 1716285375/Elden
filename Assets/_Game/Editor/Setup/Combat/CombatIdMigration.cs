using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Assigns and validates the persistent <c>AttackId</c> used by networking and hit identity.
    /// Ids are authored once and never derived from asset names, so renaming or moving an asset
    /// can never change what the network sees.
    /// </summary>
    public static class CombatIdMigration
    {
        private const string k_SearchRoot = "Assets/_Game";
        private const uint k_FirstAttackId = 100001;

        [ZZTool("战斗", "分配并校验 AttackId", 10,
            "为所有 AttackDefinition 分配持久化 AttackId 并输出校验报告？")]
        public static void AssignAndValidateAttackIds()
        {
            AttackDefinition[] attacks = LoadAllAttackDefinitions();
            int assigned = AssignMissingIds(attacks);
            AssetDatabase.SaveAssets();
            Debug.Log(Validate(attacks, out bool isValid));
            Debug.Log($"Combat Id migration: {attacks.Length} definitions, {assigned} ids assigned.");
            if (!isValid)
            {
                Debug.LogError("Combat Id validation failed. See the report above.");
            }
        }

        [ZZTool("战斗", "仅校验 AttackId（不写入）", 20)]
        public static void ValidateAttackIds()
        {
            AttackDefinition[] attacks = LoadAllAttackDefinitions();
            Debug.Log(Validate(attacks, out bool isValid));
            if (!isValid)
            {
                Debug.LogError("Combat Id validation failed. See the report above.");
            }
        }

        /// <summary>Builds the validation report. Used by the pre-build validator.</summary>
        public static string Validate(AttackDefinition[] attacks, out bool isValid)
        {
            var report = new StringBuilder();
            var ids = new Dictionary<uint, AttackDefinition>();
            int missingIds = 0;
            int duplicates = 0;
            int duplicateTransitions = 0;

            foreach (AttackDefinition attack in attacks)
            {
                if (attack == null)
                {
                    continue;
                }

                string path = AssetDatabase.GetAssetPath(attack);
                if (!attack.HasStableAttackId)
                {
                    missingIds++;
                    report.AppendLine($"[MissingId] {path}");
                    continue;
                }

                if (ids.TryGetValue(attack.AttackId, out AttackDefinition existing))
                {
                    duplicates++;
                    report.AppendLine(
                        $"[DuplicateId] {attack.AttackId} used by {path} and {AssetDatabase.GetAssetPath(existing)}");
                    continue;
                }

                ids.Add(attack.AttackId, attack);
                if (HasDuplicateTransitionCommands(attack))
                {
                    duplicateTransitions++;
                    report.AppendLine($"[DuplicateTransition] {path} maps one command twice");
                }
            }

            int invalidMovesets = ReportInvalidMovesets(report);
            isValid = missingIds == 0 && duplicates == 0 && duplicateTransitions == 0 &&
                invalidMovesets == 0;
            report.Insert(0, $"Combat Id validation: {(isValid ? "OK" : "FAILED")}\n");
            report.AppendLine(
                $"Definitions {attacks.Length}; missing {missingIds}; duplicates {duplicates}; " +
                $"duplicate transitions {duplicateTransitions}; invalid movesets {invalidMovesets}.");
            return report.ToString();
        }

        private static int AssignMissingIds(AttackDefinition[] attacks)
        {
            var used = new HashSet<uint>();
            foreach (AttackDefinition attack in attacks)
            {
                if (attack != null && attack.HasStableAttackId)
                {
                    used.Add(attack.AttackId);
                }
            }

            uint next = k_FirstAttackId;
            int assigned = 0;
            foreach (AttackDefinition attack in attacks)
            {
                if (attack == null || attack.HasStableAttackId)
                {
                    continue;
                }

                while (used.Contains(next))
                {
                    next++;
                }

                var data = new SerializedObject(attack);
                data.FindProperty("m_attackId").longValue = next;
                data.ApplyModifiedPropertiesWithoutUndo();
                used.Add(next);
                next++;
                assigned++;
            }

            return assigned;
        }

        private static bool HasDuplicateTransitionCommands(AttackDefinition attack)
        {
            SerializedProperty transitions =
                new SerializedObject(attack).FindProperty("m_transitions");
            if (transitions == null || !transitions.isArray)
            {
                return false;
            }

            var commands = new HashSet<int>();
            for (int index = 0; index < transitions.arraySize; index++)
            {
                SerializedProperty command =
                    transitions.GetArrayElementAtIndex(index).FindPropertyRelative("m_command");
                if (command == null)
                {
                    continue;
                }

                if (!commands.Add(command.intValue))
                {
                    return true;
                }
            }

            return false;
        }

        private static int ReportInvalidMovesets(StringBuilder report)
        {
            int invalid = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:WeaponMoveset", new[] { k_SearchRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var moveset = AssetDatabase.LoadAssetAtPath<WeaponMoveset>(path);
                if (moveset == null)
                {
                    continue;
                }

                SerializedProperty attacks =
                    new SerializedObject(moveset).FindProperty("m_attacks");
                if (attacks == null || attacks.arraySize == 0)
                {
                    invalid++;
                    report.AppendLine($"[EmptyMoveset] {path}");
                    continue;
                }

                for (int index = 0; index < attacks.arraySize; index++)
                {
                    if (attacks.GetArrayElementAtIndex(index).objectReferenceValue == null)
                    {
                        invalid++;
                        report.AppendLine($"[NullAttack] {path} slot {index}");
                    }
                }
            }

            return invalid;
        }

        private static AttackDefinition[] LoadAllAttackDefinitions()
        {
            var attacks = new List<AttackDefinition>();
            foreach (string guid in AssetDatabase.FindAssets("t:AttackDefinition", new[] { k_SearchRoot }))
            {
                var attack = AssetDatabase.LoadAssetAtPath<AttackDefinition>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (attack != null)
                {
                    attacks.Add(attack);
                }
            }

            return attacks.ToArray();
        }

        /// <summary>Stops a build when combat identities are not stable.</summary>
        internal sealed class BuildValidator : IPreprocessBuildWithReport
        {
            public int callbackOrder => 0;

            public void OnPreprocessBuild(BuildReport report)
            {
                string validation = Validate(LoadAllAttackDefinitions(), out bool isValid);
                if (isValid)
                {
                    return;
                }

                throw new BuildFailedException(
                    $"Combat identities are not stable. Run Tools/ZZ/Combat migration first.\n{validation}");
            }
        }
    }
}
