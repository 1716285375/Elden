using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ZZ.Editor
{
    public static class CombatExperienceAudit
    {
        [MenuItem("Tools/ZZ/Combat/Audit Combat Experience")]
        public static void Audit()
        {
            Directory.CreateDirectory("Docs/CombatRefresh");
            var report = new StringBuilder();
            string[] paths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Game/Prefabs/Characters" })
                .Select(AssetDatabase.GUIDToAssetPath).ToArray();
            foreach (string path in paths)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab.GetComponent<CharacterManager>() == null) { continue; }
                report.AppendLine("PREFAB " + path);
                foreach (Animator animator in prefab.GetComponentsInChildren<Animator>(true))
                {
                    report.AppendLine($"ANIMATOR {animator.name} controller={AssetDatabase.GetAssetPath(animator.runtimeAnimatorController)} avatar={animator.avatar?.name}");
                    if (animator.runtimeAnimatorController == null) { continue; }
                    foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips.Distinct().Where(c =>
                        c.name.ToLowerInvariant().Contains("attack") || c.name.ToLowerInvariant().Contains("swipe")))
                    {
                        report.AppendLine($"CLIP {clip.name} seconds={clip.length:F3} path={AssetDatabase.GetAssetPath(clip)} events=" +
                            string.Join(",", AnimationUtility.GetAnimationEvents(clip).Select(e => $"{e.functionName}@{e.time:F3}")));
                    }
                }
                foreach (Component component in prefab.GetComponents<Component>().Where(c => c is AICharacterManager || c is AICharacterCombatManager))
                {
                    var serialized = new SerializedObject(component);
                    SerializedProperty property = serialized.GetIterator();
                    while (property.NextVisible(true))
                    {
                        if (property.propertyType == SerializedPropertyType.ObjectReference &&
                            property.objectReferenceValue is AICharacterAttackAction action)
                        {
                            report.AppendLine($"ACTION {action.name} range={action.MinimumRange}-{action.MaximumRange} recovery={action.RecoveryTime} type={action.AttackType} definition={AssetDatabase.GetAssetPath(action.AttackDefinition)} combo={action.ComboAction?.name}");
                        }
                    }
                }
            }
            File.WriteAllText("Docs/CombatRefresh/combat-baseline.txt", report.ToString());
        }
    }
}
