using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZZ.Editor
{
    /// <summary>Adds encounter volumes at each authored streamed Boss spawn point.</summary>
    public static class BossArenaSetup
    {
        [MenuItem("Tools/ZZ/Repair Streamed Boss Arenas")]
        public static void Apply()
        {
            if (Application.isPlaying)
            {
                throw new System.InvalidOperationException("Stop Play Mode before repairing arenas.");
            }
            var report = new StringBuilder();
            Scene previous = SceneManager.GetActiveScene();
            foreach (string path in Directory.GetFiles("Assets/_Game/Scenes/Levels", "*_Spawners.unity", SearchOption.AllDirectories))
            {
                string assetPath = path.Replace('\\', '/');
                Scene scene = SceneManager.GetSceneByPath(assetPath);
                bool opened = !scene.isLoaded;
                if (opened)
                {
                    scene = EditorSceneManager.OpenScene(assetPath, OpenSceneMode.Additive);
                }
                if (scene.isDirty)
                {
                    throw new System.InvalidOperationException("Save existing scene edits before arena setup: " + assetPath);
                }
                bool changed = false;
                foreach (AICharacterSpawner spawner in scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<AICharacterSpawner>(true)).Where(item => item.IsBoss))
                {
                    Transform existing = spawner.transform.Find("Encounter Area");
                    GameObject area = existing != null ? existing.gameObject : new GameObject("Encounter Area");
                    area.transform.SetParent(spawner.transform, false);
                    area.transform.localPosition = Vector3.zero;
                    BoxCollider volume = area.GetComponent<BoxCollider>();
                    if (volume == null)
                    {
                        volume = area.AddComponent<BoxCollider>();
                    }
                    volume.isTrigger = true;
                    volume.center = Vector3.up * 2f;
                    volume.size = new Vector3(18f, 8f, 18f);
                    BossArenaController arena = area.GetComponent<BossArenaController>();
                    if (arena == null)
                    {
                        arena = area.AddComponent<BossArenaController>();
                    }
                    var data = new SerializedObject(arena);
                    data.FindProperty("m_bossID").intValue = spawner.BossID;
                    data.ApplyModifiedPropertiesWithoutUndo();
                    report.AppendLine($"{spawner.name} ID={spawner.BossID} center={area.transform.TransformPoint(volume.center)} scene={scene.name}");
                    changed = true;
                }
                if (changed)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
                if (opened)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
            SceneManager.SetActiveScene(previous);
            Directory.CreateDirectory(".utmp");
            File.WriteAllText(".utmp/boss-arena-repair.txt", report.ToString());
        }
    }
}
