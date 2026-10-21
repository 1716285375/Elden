using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ZZ.Tests
{
    public class OriginalPlayerActionTests
    {
        private const string k_Root = "Assets/_Game/Art/Characters/EldenRing/PlayerActions";

        [TestCase("a030_030000")]
        [TestCase("a030_030010")]
        [TestCase("a030_030020")]
        public void OriginalMotionAnimatesTheOriginalSkeletonWithoutInvalidTransforms(string name)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(k_Root + "/Clips/" + name + ".anim");
            Assert.That(clip, Is.Not.Null);
            Assert.That(clip.length, Is.GreaterThan(0.1f));
            Assert.That(clip.isHumanMotion, Is.False, "Source hierarchy must retain its original bone relationships.");
            Assert.That(AnimationUtility.GetCurveBindings(clip).Length, Is.GreaterThan(100));
            GameObject root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(k_Root + "/Source/c0000.fbx"));
            try
            {
                Transform[] bones = root.GetComponentsInChildren<Transform>(true);
                Assert.That(bones.Length, Is.GreaterThan(100));
                clip.SampleAnimation(root, clip.length * 0.1f);
                Quaternion[] before = bones.Select(bone => bone.localRotation).ToArray();
                clip.SampleAnimation(root, clip.length * 0.7f);
                Assert.That(bones.Select((bone, index) => Quaternion.Angle(before[index], bone.localRotation)).Max(),
                    Is.GreaterThan(0.1f));
                Assert.That(bones.All(bone => float.IsFinite(bone.position.sqrMagnitude) &&
                    bone.position.magnitude < 100f), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
