using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ZZ.Editor
{
    /// <summary>Samples the existing humanoid clips against old and subdivided meshes on the same rig.</summary>
    public static class PlayerSubdivisionVerification
    {
        [MenuItem("Tools/ZZ/Preview Subdivided Player")]
        public static void Preview()
        {
            var preview = new PreviewRenderUtility();
            GameObject root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Prefabs/Characters/Player/Player.prefab"));
            try
            {
                foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true))
                {
                    canvas.gameObject.SetActive(false);
                }
                preview.AddSingleGO(root);
                Animator animator = root.GetComponentInChildren<Animator>();
                animator.fireEvents = false;
                animator.Rebind();
                animator.Update(0f);
                animator.Update(0.5f);
                Renderer[] renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>()
                    .Where(renderer => renderer.enabled).ToArray();
                Bounds bounds = renderers[0].bounds;
                foreach (Renderer renderer in renderers.Skip(1))
                {
                    bounds.Encapsulate(renderer.bounds);
                }
                File.WriteAllText(".utmp/player-preview-bounds.txt", $"renderers={renderers.Length} bounds={bounds} root={root.activeInHierarchy}");
                preview.camera.fieldOfView = 35f;
                preview.camera.transform.position = bounds.center + new Vector3(0.3f, 0.05f, 1f).normalized * 4f;
                preview.camera.transform.LookAt(bounds.center);
                preview.camera.nearClipPlane = 0.01f;
                preview.camera.farClipPlane = 100f;
                preview.camera.backgroundColor = new Color(0.09f, 0.1f, 0.12f);
                preview.camera.clearFlags = CameraClearFlags.SolidColor;
                preview.lights[0].intensity = 1.5f;
                preview.lights[0].transform.rotation = Quaternion.Euler(40f, 30f, 0f);
                preview.lights[1].intensity = 0.8f;
                preview.BeginStaticPreview(new Rect(0, 0, 700, 900));
                preview.camera.Render();
                Texture2D texture = preview.EndStaticPreview();
                File.WriteAllBytes(".utmp/player-unity-preview.png", texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
            }
            finally
            {
                preview.Cleanup();
            }
        }

        [MenuItem("Tools/ZZ/Verify Player Subdivision Animations")]
        public static void Verify()
        {
            GameObject root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Prefabs/Characters/Player/Player.prefab"));
            var report = new StringBuilder();
            var baked = new Mesh();
            var reference = new Mesh();
            try
            {
                Animator animator = root.GetComponentInChildren<Animator>(true);
                animator.fireEvents = false;
                animator.Rebind();
                string[] names = { "core_main_idle_01", "straight_sword_main_light_attack_01",
                    "straight_sword_th_light_attack_01", "straight_sword_main_jump_light_attack_01",
                    "core_main_run_FR_01", "core_main_roll_med_to_idle_F_01" };
                SkinnedMeshRenderer[] renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .Where(renderer => renderer.sharedMesh != null && AssetDatabase.GetAssetPath(renderer.sharedMesh)
                        .StartsWith("Assets/_Game/Art/Characters/Player/Optimized/", StringComparison.Ordinal)).ToArray();
                AnimationMode.StartAnimationMode();
                float maximumDeviation = 0f;
                int samples = 0;
                foreach (string name in names)
                {
                    string path = Directory.GetFiles("Assets/_Game/Art/Characters/Shared/Humanoid/Animations",
                        name + ".anim", SearchOption.AllDirectories).Single().Replace('\\', '/');
                    AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                    foreach (float phase in new[] { 0.1f, 0.45f, 0.8f })
                    {
                        AnimationMode.BeginSampling();
                        AnimationMode.SampleAnimationClip(animator.gameObject, clip, clip.length * phase);
                        AnimationMode.EndSampling();
                        foreach (SkinnedMeshRenderer renderer in renderers)
                        {
                            Mesh current = renderer.sharedMesh;
                            string originalPath = "Assets/_Game/Art/Shared/Models/Rigged/Shared/Meshes/" +
                                Path.GetFileName(AssetDatabase.GetAssetPath(current));
                            renderer.BakeMesh(baked);
                            try
                            {
                                renderer.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(originalPath);
                                renderer.BakeMesh(reference);
                            }
                            finally
                            {
                                renderer.sharedMesh = current;
                            }
                            baked.RecalculateBounds();
                            reference.RecalculateBounds();
                            float deviation = Mathf.Max(Vector3.Distance(baked.bounds.min, reference.bounds.min),
                                Vector3.Distance(baked.bounds.max, reference.bounds.max));
                            if (!float.IsFinite(deviation) || deviation > 0.06f || baked.vertices.Any(vertex =>
                                !float.IsFinite(vertex.x) || !float.IsFinite(vertex.y) || !float.IsFinite(vertex.z)))
                            {
                                throw new InvalidOperationException($"Deformation envelope failed: {renderer.name} {name} {phase}: {deviation}");
                            }
                            maximumDeviation = Mathf.Max(maximumDeviation, deviation);
                            samples++;
                        }
                        report.AppendLine($"PASS {name} phase={phase}");
                    }
                }
                report.AppendLine($"PASS {samples} mesh/pose samples; maximum bounds deviation={maximumDeviation:F6}m");
            }
            finally
            {
                AnimationMode.StopAnimationMode();
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(baked);
                Object.DestroyImmediate(reference);
                File.WriteAllText(".utmp/player-subdivision-animation-verification.txt", report.ToString());
            }
        }
    }
}
