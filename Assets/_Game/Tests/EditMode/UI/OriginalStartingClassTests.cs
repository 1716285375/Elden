using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ZZ.Tests
{
    public sealed class OriginalStartingClassTests
    {
        [Test]
        public void TenOriginsHaveOriginalStatsAndIndependentArmorInsteadOfDrakeKnight()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/_Game/Scenes/Frontend/SCN_MainMenu.unity");
            try
            {
                Component title = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Component>(true))
                    .Single(component => component != null && component.GetType().Name == "TitleScreenManager");
                SerializedProperty classes = new SerializedObject(title).FindProperty("m_startingClasses");
                Assert.That(classes.arraySize, Is.EqualTo(10));
                int[] expectedLevels = { 9, 8, 7, 5, 6, 7, 10, 9, 9, 1 };
                string[] attributes = { "m_vitality", "m_mind", "m_endurance", "m_strength", "m_dexterity",
                    "m_intelligence", "m_faith", "m_arcane" };
                for (int index = 0; index < classes.arraySize; index++)
                {
                    SerializedProperty origin = classes.GetArrayElementAtIndex(index);
                    Assert.That(origin.FindPropertyRelative("m_originId").intValue, Is.EqualTo(3000 + index));
                    Assert.That(origin.FindPropertyRelative("m_level").intValue, Is.EqualTo(expectedLevels[index]));
                    Assert.That(attributes.Sum(name => origin.FindPropertyRelative(name).intValue) - 79,
                        Is.EqualTo(expectedLevels[index]));
                    foreach (string slot in new[] { "m_headEquipment", "m_bodyEquipment", "m_handEquipment", "m_legEquipment" })
                    {
                        Object item = origin.FindPropertyRelative(slot).objectReferenceValue;
                        if (index == 9)
                        {
                            Assert.That(item, Is.Null);
                        }
                        else if (item != null)
                        {
                            Assert.That(AssetDatabase.GetAssetPath(item), Does.Contain("/StartingClasses/Items/ER_"));
                        }
                    }
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
