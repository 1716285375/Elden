using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ZZ.Tests
{
    public class AIAnimatorAvatarTests
    {
        private const string k_AIAvatarPath =
            "Assets/Data/Animations/AI/Undead AI Avatar.asset";

        [TestCase("Assets/Data/Prefabs/Characters/AI/Undead AI.prefab")]
        [TestCase("Assets/Data/Prefabs/Characters/AI/Fallen Watcher Boss.prefab")]
        public void AIAnimatorUsesValidDedicatedAvatarAndCanEvaluate(
            string prefabPath)
        {
            Avatar expectedAvatar = AssetDatabase.LoadAssetAtPath<Avatar>(
                k_AIAvatarPath);
            Assert.That(expectedAvatar, Is.Not.Null);
            Assert.That(expectedAvatar.isHuman, Is.True);
            Assert.That(expectedAvatar.isValid, Is.True);

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                Animator animator = root.GetComponentInChildren<Animator>(true);
                Assert.That(animator, Is.Not.Null);
                Assert.That(animator.avatar, Is.SameAs(expectedAvatar));

                animator.enabled = true;
                animator.Rebind();
                animator.Play("Locomotion", 0, 0f);
                for (int sampleIndex = 0; sampleIndex < 10; sampleIndex++)
                {
                    animator.Update(1f / 60f);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
