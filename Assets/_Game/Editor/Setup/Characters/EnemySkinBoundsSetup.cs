using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ZZ.Editor
{
    /// <summary>Samples enemy skins before disabling offscreen mesh updates; gameplay bones remain animated.</summary>
    public static class EnemySkinBoundsSetup
    {
        [MenuItem("Tools/ZZ/Elden Ring/Calibrate Enemy Rendered Heights")]
        public static void CalibrateHeights()
        {
            var report = new StringBuilder();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab",
                new[] { "Assets/_Game/Prefabs/Characters/AI/Variants" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    FitCapsuleHeight(root);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    report.AppendLine(root.name + ": rendered height matches capsule");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            File.WriteAllText("Docs/ArtRefresh/enemy-height-calibration.txt", report.ToString());
        }

        internal static void FitCapsuleHeight(GameObject root)
        {
            Animator animator = root.GetComponentInChildren<Animator>(true);
            animator.Rebind();
            SkinnedMeshRenderer[] skins = root.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(renderer => renderer.enabled).ToArray();
            Vector3[] vertices = skins.SelectMany(GetWorldVertices).ToArray();
            float height = vertices.Max(vertex => vertex.y) - vertices.Min(vertex => vertex.y);
            float target = root.GetComponent<CharacterController>().height * root.transform.lossyScale.y;
            if (height < 0.01f || target < 0.01f)
            {
                throw new InvalidOperationException("Cannot fit a degenerate character skin.");
            }
            float factor = target / height;
            animator.transform.localScale *= factor;
            foreach (BoxCollider box in animator.GetComponentsInChildren<BoxCollider>(true))
            {
                box.transform.localScale /= factor;
            }
            vertices = skins.SelectMany(GetWorldVertices).ToArray();
            animator.transform.position += Vector3.up *
                (root.transform.position.y - vertices.Min(vertex => vertex.y));
            animator.Rebind();
            vertices = skins.SelectMany(GetWorldVertices).ToArray();
            float actual = vertices.Max(vertex => vertex.y) - vertices.Min(vertex => vertex.y);
            if (Mathf.Abs(actual - target) > 0.01f)
            {
                throw new InvalidOperationException("Character height changed after Animator.Rebind.");
            }
        }

        [MenuItem("Tools/ZZ/Elden Ring/Diagnose Enemy Skin Scale")]
        public static void DiagnoseScale()
        {
            var report = new StringBuilder();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab",
                new[] { "Assets/_Game/Prefabs/Characters/AI/Variants" }))
            {
                GameObject root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)));
                try
                {
                    Animator animator = root.GetComponentInChildren<Animator>(true);
                    animator.fireEvents = false;
                    animator.Rebind();
                    Vector3[] vertices = root.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                        .Where(renderer => renderer.enabled).SelectMany(GetWorldVertices).ToArray();
                    report.AppendLine(root.name + ": matrix skin height=" +
                        (vertices.Max(vertex => vertex.y) - vertices.Min(vertex => vertex.y)) +
                        "; capsule=" + root.GetComponent<CharacterController>().height);
                    float factor = root.GetComponent<CharacterController>().height /
                        (vertices.Max(vertex => vertex.y) - vertices.Min(vertex => vertex.y));
                    animator.transform.localScale *= factor;
                    animator.Rebind();
                    vertices = root.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                        .Where(renderer => renderer.enabled).SelectMany(GetWorldVertices).ToArray();
                    report.AppendLine("  after root scale and Rebind=" +
                        (vertices.Max(vertex => vertex.y) - vertices.Min(vertex => vertex.y)));
                }
                finally
                {
                    Object.DestroyImmediate(root);
                }
            }
            File.WriteAllText("Docs/ArtRefresh/enemy-skin-scale.txt", report.ToString());
        }

        internal static Vector3[] GetWorldVertices(SkinnedMeshRenderer renderer)
        {
            Mesh mesh = renderer.sharedMesh;
            Matrix4x4[] bindposes = mesh.bindposes;
            Matrix4x4[] matrices = renderer.bones.Select((bone, index) =>
                bone.localToWorldMatrix * bindposes[index]).ToArray();
            BoneWeight[] weights = mesh.boneWeights;
            Vector3[] vertices = mesh.vertices;
            for (int index = 0; index < vertices.Length; index++)
            {
                Vector3 vertex = vertices[index];
                BoneWeight weight = weights[index];
                vertices[index] = matrices[weight.boneIndex0].MultiplyPoint3x4(vertex) * weight.weight0 +
                    matrices[weight.boneIndex1].MultiplyPoint3x4(vertex) * weight.weight1 +
                    matrices[weight.boneIndex2].MultiplyPoint3x4(vertex) * weight.weight2 +
                    matrices[weight.boneIndex3].MultiplyPoint3x4(vertex) * weight.weight3;
            }
            return vertices;
        }

        [MenuItem("Tools/ZZ/Elden Ring/Optimize Enemy Skin Bounds")]
        public static void Apply()
        {
            if (Application.isPlaying || AnimationMode.InAnimationMode())
            {
                throw new InvalidOperationException("Stop Play Mode and Animation preview before sampling enemy skins.");
            }
            var report = new StringBuilder();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab",
                new[] { "Assets/_Game/Prefabs/Characters/AI/Variants" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject sample = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                var bounds = new Dictionary<string, Bounds>();
                try
                {
                    Animator animator = sample.GetComponentInChildren<Animator>(true);
                    animator.fireEvents = false;
                    Accumulate(sample, bounds);
                    AnimationClip[] clips = animator.runtimeAnimatorController.animationClips
                        .Where(clip => clip.isHumanMotion && clip.length > 0f).Distinct().ToArray();
                    foreach (AnimationClip clip in clips)
                    {
                        AnimationMode.StopAnimationMode();
                        animator.Rebind();
                        AnimationMode.StartAnimationMode();
                        foreach (float phase in new[] { 0f, 0.33f, 0.66f, 1f })
                        {
                            AnimationMode.BeginSampling();
                            AnimationMode.SampleAnimationClip(animator.gameObject, clip, clip.length * phase);
                            AnimationMode.EndSampling();
                            Accumulate(sample, bounds);
                        }
                    }
                    AnimationMode.StopAnimationMode();
                    GameObject root = PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        foreach (SkinnedMeshRenderer renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                        {
                            string key = AnimationUtility.CalculateTransformPath(renderer.transform, root.transform);
                            Bounds sampled = bounds[key];
                            sampled.Expand(sampled.size * 0.2f + Vector3.one * 0.1f);
                            renderer.localBounds = sampled;
                            renderer.updateWhenOffscreen = false;
                        }
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        report.AppendLine(root.name + ": skins=" + bounds.Count + ", clips=" + clips.Length +
                            ", offscreen mesh updates=disabled, bone animation=preserved");
                    }
                    finally
                    {
                        PrefabUtility.UnloadPrefabContents(root);
                    }
                }
                finally
                {
                    AnimationMode.StopAnimationMode();
                    Object.DestroyImmediate(sample);
                }
            }
            File.WriteAllText("Docs/ArtRefresh/enemy-skin-bounds.txt", report.ToString());
        }

        private static void Accumulate(GameObject root, Dictionary<string, Bounds> bounds)
        {
            foreach (SkinnedMeshRenderer renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(item => item.enabled))
            {
                Vector3[] vertices = GetWorldVertices(renderer).Select(renderer.transform.InverseTransformPoint).ToArray();
                string key = AnimationUtility.CalculateTransformPath(renderer.transform, root.transform);
                if (!bounds.TryGetValue(key, out Bounds sampled))
                {
                    sampled = new Bounds(vertices[0], Vector3.zero);
                }
                foreach (Vector3 vertex in vertices)
                {
                    if (!float.IsFinite(vertex.x) || !float.IsFinite(vertex.y) || !float.IsFinite(vertex.z))
                    {
                        throw new InvalidOperationException("Invalid sampled skin vertex: " + key);
                    }
                    sampled.Encapsulate(vertex);
                }
                bounds[key] = sampled;
            }
        }
    }
}
