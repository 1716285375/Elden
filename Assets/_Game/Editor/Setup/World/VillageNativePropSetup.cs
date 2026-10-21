using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZZ.Editor
{
    /// <summary>Uses Village art for all imported world reward props while preserving save identities.</summary>
    public static class VillageNativePropSetup
    {
        [MenuItem("Tools/ZZ/Environment/Replace Legacy Props With Village Content")]
        public static void Apply()
        {
            if (Application.isPlaying) { throw new InvalidOperationException("Stop Play Mode first."); }
            Scene scene = EditorSceneManager.OpenScene(WorldScenePathLayout.GameplayScenePath, OpenSceneMode.Single);
            Transform gameplay = scene.GetRootGameObjects().Single(root => root.name == "_VillageGameplay").transform;
            var report = new StringBuilder();
            foreach (TreasureChestInteractable chest in gameplay.GetComponentsInChildren<TreasureChestInteractable>(true))
            {
                ClearOldVisuals(chest.gameObject);
                var data = new SerializedObject(chest);
                data.FindProperty("m_chestAnimator").objectReferenceValue = null;
                data.ApplyModifiedPropertiesWithoutUndo();
                foreach (Animator animator in chest.GetComponentsInChildren<Animator>(true))
                {
                    UnityEngine.Object.DestroyImmediate(animator);
                }
                AddVillageVisual(chest.transform, "pf_barrel_01", 1.1f);
                chest.name = "Village Supply Barrel " + chest.WorldItemID;
                report.AppendLine("BARREL " + chest.name + "; preserved reward and world id=" + chest.WorldItemID);
            }
            foreach (PickupItemInteractable pickup in gameplay.GetComponentsInChildren<PickupItemInteractable>(true))
            {
                ClearOldVisuals(pickup.gameObject);
                string prop = pickup.Item is UpgradeMaterial ? "pf_boulder_02_015" : "pf_bucket_01";
                AddVillageVisual(pickup.transform, prop, prop.Contains("boulder") ? 0.24f : 0.35f);
                pickup.name = "Village Pickup " + pickup.ItemID;
                report.AppendLine("PICKUP " + pickup.ItemID + " -> " + prop);
            }
            EditorSceneManager.SaveScene(scene);
            foreach (string name in new[] { "Empty Flask", "Health Flask", "Focus Flask" })
            {
                string path = "Assets/_Game/Prefabs/Items/" + name + ".prefab";
                GameObject vessel = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    ClearOldVisuals(vessel);
                    AddVillageVisual(vessel.transform, "pf_bucket_01", 0.18f);
                    PrefabUtility.SaveAsPrefabAsset(vessel, path);
                    report.AppendLine("CONSUMABLE " + name + " -> Village wooden vessel; gameplay and VFX preserved");
                }
                finally { PrefabUtility.UnloadPrefabContents(vessel); }
            }
            File.WriteAllText("Docs/CollisionAssetRefresh/village-native-props.txt", report.ToString());
        }

        private static void ClearOldVisuals(GameObject root)
        {
            if (PrefabUtility.IsPartOfPrefabInstance(root))
            {
                GameObject instance = PrefabUtility.GetOutermostPrefabInstanceRoot(root);
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            }
            Transform replacement = root.transform.Find("Village Visual");
            foreach (LODGroup group in root.GetComponentsInChildren<LODGroup>(true))
            {
                UnityEngine.Object.DestroyImmediate(group);
            }
            foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (replacement != null && renderer.transform.IsChildOf(replacement)) { continue; }
                UnityEngine.Object.DestroyImmediate(renderer);
            }
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (replacement != null && filter.transform.IsChildOf(replacement)) { continue; }
                UnityEngine.Object.DestroyImmediate(filter);
            }
        }

        private static void AddVillageVisual(Transform parent, string name, float height)
        {
            Transform existing = parent.Find("Village Visual");
            if (existing != null) { UnityEngine.Object.DestroyImmediate(existing.gameObject); }
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Viking Village/Prefabs/Props/" + name + ".prefab");
            GameObject visual = UnityEngine.Object.Instantiate(source, parent);
            visual.name = "Village Visual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true))
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) { bounds.Encapsulate(renderer.bounds); }
            visual.transform.localScale *= height / bounds.size.y;
            bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) { bounds.Encapsulate(renderer.bounds); }
            visual.transform.position += new Vector3(parent.position.x - bounds.center.x,
                parent.position.y - bounds.min.y, parent.position.z - bounds.center.z);
        }
    }
}
