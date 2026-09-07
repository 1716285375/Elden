using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace ZZ.Tests
{
    public class WorldContentValidationTests
    {
        [Test]
        public void AuthoredWorldHasUsableNavigationInteractionsAndAreaVolumes()
        {
            var openedScenes = new List<Scene>();
            var navigationInstances = new List<NavMeshDataInstance>();
            var failures = new List<string>();
            var report = new StringBuilder();
            Scene previousActiveScene = SceneManager.GetActiveScene();
            try
            {
                string[] paths = EditorBuildSettings.scenes.Where(entry => entry.enabled &&
                    entry.path.Contains("LV01_AbandonedMonastery") &&
                    !entry.path.EndsWith("_Effects.unity")).Select(entry => entry.path).ToArray();
                var roots = new List<GameObject>();
                foreach (string path in paths)
                {
                    Scene scene = SceneManager.GetSceneByPath(path);
                    if (!scene.isLoaded)
                    {
                        scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                        openedScenes.Add(scene);
                    }
                    roots.AddRange(scene.GetRootGameObjects());
                }

                Component[] components = roots.SelectMany(root => root.GetComponentsInChildren<Component>(true))
                    .Where(component => component != null).ToArray();
                foreach (Component surface in components.Where(component => component.GetType().Name == "NavMeshSurface"))
                {
                    var data = surface.GetType().GetProperty("navMeshData").GetValue(surface) as NavMeshData;
                    if (data == null)
                    {
                        failures.Add($"Missing NavMesh data: {surface.gameObject.scene.name}/{surface.name}");
                        continue;
                    }
                    navigationInstances.Add(NavMesh.AddNavMeshData(data, surface.transform.position, surface.transform.rotation));
                }

                foreach (Component spawner in components.Where(component => component.GetType().Name == "AICharacterSpawner"))
                {
                    var serialized = new SerializedObject(spawner);
                    var prefab = serialized.FindProperty("m_characterGameObject").objectReferenceValue as GameObject;
                    bool onNavigation = NavMesh.SamplePosition(spawner.transform.position, out NavMeshHit hit, 4f, NavMesh.AllAreas);
                    report.AppendLine($"SPAWNER {spawner.name} position={spawner.transform.position} nav={onNavigation}");
                    if (prefab == null || prefab.GetComponent("AICharacterManager") == null || !onNavigation)
                    {
                        failures.Add($"Invalid spawn: {spawner.name} at {spawner.transform.position}, nav={onNavigation}");
                    }
                }

                Type interactableType = Type.GetType("ZZ.Interactable, Assembly-CSharp", true);
                var worldItemIDs = new HashSet<int>();
                foreach (Component interactable in components.Where(component => interactableType.IsInstanceOfType(component)))
                {
                    var serialized = new SerializedObject(interactable);
                    var collider = serialized.FindProperty("m_interactableCollider").objectReferenceValue as Collider;
                    var itemProperty = serialized.FindProperty("m_item");
                    bool hasItem = itemProperty == null || itemProperty.objectReferenceValue != null;
                    bool canCollide = collider != null && !Physics.GetIgnoreLayerCollision(LayerMask.NameToLayer("Player"), collider.gameObject.layer);
                    report.AppendLine($"INTERACTION {interactable.name} position={interactable.transform.position} collider={collider?.name} layer={collider?.gameObject.layer} item={hasItem} collision={canCollide}");
                    if (collider == null || !collider.enabled || !collider.isTrigger || !hasItem || !canCollide)
                    {
                        failures.Add($"Invalid interaction: {interactable.gameObject.scene.name}/{interactable.name}, item={hasItem}, collision={canCollide}");
                    }
                    SerializedProperty pickupType = serialized.FindProperty("m_pickupType");
                    if (pickupType != null && (pickupType.intValue != 0 ||
                        !worldItemIDs.Add(serialized.FindProperty("m_itemID").intValue)))
                    {
                        failures.Add($"Scene pickup must use WorldSpawn and a unique save key: {interactable.name}");
                    }
                    SerializedProperty chestID = serialized.FindProperty("m_worldItemID");
                    if (chestID != null && (!worldItemIDs.Add(chestID.intValue) ||
                        serialized.FindProperty("m_reward").objectReferenceValue == null))
                    {
                        failures.Add($"Chest reward or save key is invalid: {interactable.name}");
                    }
                }

                Component[] triggers = components.Where(component => component.GetType().Name == "EventTriggerLoadScene" &&
                    component.gameObject.activeInHierarchy).ToArray();
                if (triggers.Length != 8)
                {
                    failures.Add($"Expected eight persistent area volumes, found {triggers.Length}.");
                }
                foreach (Component trigger in triggers)
                {
                    var collider = trigger.GetComponent<BoxCollider>();
                    var location = new SerializedObject(trigger).FindProperty("m_worldLocation").objectReferenceValue;
                    report.AppendLine($"AREA {trigger.name} position={trigger.transform.position} size={collider?.size} location={location?.name}");
                    if (collider == null || collider.size.x < 30f || collider.size.z < 30f || location == null ||
                        !trigger.gameObject.scene.path.EndsWith("/SCN_LV01_AbandonedMonastery.unity"))
                    {
                        failures.Add($"Area volume cannot reliably own an open-world region: {trigger.name}");
                    }
                }
            }
            finally
            {
                foreach (NavMeshDataInstance instance in navigationInstances)
                {
                    instance.Remove();
                }
                for (int sceneIndex = openedScenes.Count - 1; sceneIndex >= 0; sceneIndex--)
                {
                    EditorSceneManager.CloseScene(openedScenes[sceneIndex], true);
                }
                if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                {
                    SceneManager.SetActiveScene(previousActiveScene);
                }
                Directory.CreateDirectory(".utmp");
                File.WriteAllText(".utmp/world-content-audit.txt", report + "\nFAILURES\n" + string.Join("\n", failures));
            }
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }
    }
}
