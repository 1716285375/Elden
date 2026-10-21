using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ZZ.Tests
{
    public class AIAnimatorAvatarTests
    {
        private const string k_AIAvatarPath =
            "Assets/_Game/Art/Characters/Creatures/Undead/Animations/Undead AI Avatar.asset";

        [TestCase("Assets/_Game/Prefabs/Characters/AI/Undead AI.prefab",
            "Assets/_Game/Art/Characters/EldenRing/c3000/c3000Avatar.asset")]
        [TestCase("Assets/_Game/Prefabs/Characters/AI/Fallen Watcher Boss.prefab", k_AIAvatarPath)]
        [TestCase("Assets/_Game/Prefabs/Characters/AI/Ranger/Ranger AI.prefab",
            "Assets/_Game/Art/Characters/EldenRing/c3010/c3010Avatar.asset")]
        public void AIAnimatorUsesValidDedicatedAvatarAndCanEvaluate(
            string prefabPath, string avatarPath)
        {
            Avatar expectedAvatar = AssetDatabase.LoadAssetAtPath<Avatar>(
                avatarPath);
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
