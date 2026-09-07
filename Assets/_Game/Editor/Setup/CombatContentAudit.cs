using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ZZ.Editor
{
    public static class CombatContentAudit
    {
        [MenuItem("Tools/ZZ/Audit Combat Content")]
        public static void Audit()
        {
            var report = new StringBuilder();
            foreach (string path in Directory.GetFiles("Assets/_Game/Art/Shared/Models/Rigged/Characters", "*.prefab", SearchOption.AllDirectories))
            {
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path.Replace('\\', '/'));
                Animator animator = model.GetComponentInChildren<Animator>(true);
                report.AppendLine($"MODEL {model.name} animator={animator?.name} avatar={AssetDatabase.GetAssetPath(animator?.avatar)} human={animator?.avatar?.isHuman} renderers={model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length} controller={AssetDatabase.GetAssetPath(animator?.runtimeAnimatorController)}");
            }
            GameObject ai = PrefabUtility.LoadPrefabContents("Assets/_Game/Prefabs/Characters/AI/Undead AI.prefab");
            try
            {
                foreach (Component component in ai.GetComponentsInChildren<Component>(true).Where(c => c != null && (c is MonoBehaviour || c is Animator)))
                {
                    var data = new SerializedObject(component);
                    SerializedProperty property = data.GetIterator();
                    while (property.Next(true))
                    {
                        if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue != null)
                        {
                            report.AppendLine($"REF {component.gameObject.name}/{component.GetType().Name}.{property.propertyPath} -> {property.objectReferenceValue.name} [{property.objectReferenceValue.GetType().Name}]");
                        }
                    }
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(ai);
            }
            File.WriteAllText(".utmp/combat-content-audit.txt", report.ToString());
        }
    }
}
