using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ZZ.Editor
{
    public static class EnemyModelPreview
    {
        [MenuItem("Tools/ZZ/Preview Enemy Models")]
        public static void Capture()
        {
            string[] paths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Game/Prefabs/Characters/AI/Variants" })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path).ToArray();
            const int k_Size = 384;
            var atlas = new Texture2D(k_Size * 4, k_Size * 2, TextureFormat.RGB24, false);
            atlas.SetPixels(Enumerable.Repeat(new Color(0.09f, 0.1f, 0.12f), atlas.width * atlas.height).ToArray());
            for (int index = 0; index < paths.Length; index++)
            {
                var preview = new PreviewRenderUtility();
                GameObject root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(paths[index]));
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
                    Renderer[] renderers = root.GetComponentsInChildren<Renderer>().Where(renderer => renderer.enabled).ToArray();
                    Bounds bounds = renderers[0].bounds;
                    foreach (Renderer renderer in renderers.Skip(1))
                    {
                        bounds.Encapsulate(renderer.bounds);
                    }
                    preview.camera.fieldOfView = 35f;
                    preview.camera.transform.position = bounds.center + new Vector3(0.5f, 0.2f, 1f).normalized * bounds.size.magnitude * 1.8f;
                    preview.camera.transform.LookAt(bounds.center);
                    preview.camera.nearClipPlane = 0.01f;
                    preview.camera.farClipPlane = 100f;
                    preview.camera.backgroundColor = new Color(0.09f, 0.1f, 0.12f);
                    preview.camera.clearFlags = CameraClearFlags.SolidColor;
                    preview.lights[0].intensity = 1.5f;
                    preview.lights[0].transform.rotation = Quaternion.Euler(40f, 30f, 0f);
                    preview.lights[1].intensity = 0.8f;
                    preview.BeginStaticPreview(new Rect(0, 0, k_Size, k_Size));
                    preview.camera.Render();
                    Texture2D thumbnail = preview.EndStaticPreview();
                    atlas.SetPixels(index % 4 * k_Size, (1 - index / 4) * k_Size, k_Size, k_Size, thumbnail.GetPixels());
                    Object.DestroyImmediate(thumbnail);
                }
                finally
                {
                    preview.Cleanup();
                }
            }
            atlas.Apply();
            File.WriteAllBytes(".utmp/enemy-model-gallery.png", atlas.EncodeToPNG());
            File.WriteAllLines(".utmp/enemy-model-gallery.txt", paths);
            Object.DestroyImmediate(atlas);
        }
    }
}
