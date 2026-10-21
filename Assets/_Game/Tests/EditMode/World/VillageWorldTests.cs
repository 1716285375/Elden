using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace ZZ.Tests
{
    public class VillageWorldTests
    {
        [Test]
        public void RenderWarmupEnablesAtMost32RenderersPerFrameAndCompletes()
        {
            var root = new GameObject("Warmup Test");
            root.SetActive(false);
            try
            {
                Type type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("ZZ.VillageSceneWarmup"))
                    .First(t => t != null);
                Component warmup = root.AddComponent(type);
                var renderers = new Renderer[65];
                for (int index = 0; index < renderers.Length; index++)
                {
                    var child = new GameObject("Renderer " + index);
                    child.transform.SetParent(root.transform);
                    renderers[index] = child.AddComponent<MeshRenderer>();
                    renderers[index].enabled = false;
                }
                var serialized = new SerializedObject(warmup);
                SerializedProperty list = serialized.FindProperty("m_renderers");
                list.arraySize = renderers.Length;
                for (int index = 0; index < renderers.Length; index++)
                {
                    list.GetArrayElementAtIndex(index).objectReferenceValue = renderers[index];
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var routine = (System.Collections.IEnumerator)type.GetMethod("Start",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(warmup, null);
                int previouslyEnabled = 0;
                while (routine.MoveNext())
                {
                    int enabled = renderers.Count(r => r.enabled);
                    Assert.That(enabled - previouslyEnabled, Is.InRange(0, 32));
                    previouslyEnabled = enabled;
                }
                Assert.That(renderers.All(r => r.enabled), Is.True);
                Assert.That(type.GetProperty("IsReady").GetValue(warmup), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void BuildLoadsVillageDirectlyWithoutLegacySlices()
        {
            Assert.That(EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path), Is.EqualTo(new[]
            {
                WorldScenePathLayout.MainMenuScenePath, WorldScenePathLayout.GameplayScenePath
            }));
            Assert.That(AssetDatabase.FindAssets("t:Scene", new[] { "Assets/_Game/Scenes/Levels" })
                .Select(AssetDatabase.GUIDToAssetPath).Any(p => p.Contains("LV01_AbandonedMonastery")), Is.False);
        }

        [Test]
        public void VillageHasConnectedSpawnNavigationAndValidGameplayReferences()
        {
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = SceneManager.GetSceneByPath(WorldScenePathLayout.GameplayScenePath);
            bool opened = !scene.isLoaded;
            if (opened) { scene = EditorSceneManager.OpenScene(WorldScenePathLayout.GameplayScenePath, OpenSceneMode.Additive); }
            try
            {
                Transform[] objects = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
                Component[] components = objects.SelectMany(t => t.GetComponents<Component>()).Where(c => c != null).ToArray();
                Assert.That(objects.Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)), Is.Zero);
                Assert.That(components.Count(c => c.GetType().Name == "WorldAIManager"), Is.EqualTo(1));
                Assert.That(components.Count(c => c.GetType().Name == "WorldObjectManager"), Is.EqualTo(1));
                Assert.That(components.Any(c => c.GetType().Name == "EventTriggerLoadScene" || c.GetType().Name == "WorldSceneSubSceneManager"), Is.False);
                Transform spawn = objects.Single(t => t.name == "Player Spawn Point");
                Assert.That(NavMesh.SamplePosition(spawn.position, out NavMeshHit spawnHit, 1f, NavMesh.AllAreas), Is.True);
                Component[] spawners = components.Where(c => c.GetType().Name == "AICharacterSpawner").ToArray();
                Assert.That(spawners.Length, Is.InRange(3, 15));
                foreach (Component spawner in spawners)
                {
                    Assert.That(new SerializedObject(spawner).FindProperty("m_characterGameObject").objectReferenceValue, Is.Not.Null, spawner.name);
                    Assert.That(NavMesh.SamplePosition(spawner.transform.position, out NavMeshHit hit, 1f, NavMesh.AllAreas), Is.True, spawner.name);
                    var path = new NavMeshPath();
                    Assert.That(NavMesh.CalculatePath(spawnHit.position, hit.position, NavMesh.AllAreas, path), Is.True);
                    bool originalBoss = scene.name == "SCN_m31_03_00_00" &&
                        new SerializedObject(spawner).FindProperty("m_bossID").intValue == 31030800;
                    if (originalBoss)
                    {
                        Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathPartial),
                            "The original NPC-only fog boundary must keep the Boss inside its arena.");
                        MeshCollider barrier = components.OfType<MeshCollider>().Single(collider => collider.name == "h900100");
                        Assert.That(barrier.excludeLayers.value & (1 << LayerMask.NameToLayer("Player")), Is.Not.Zero,
                            "The source NPC-only boundary must allow the player through.");
                    }
                    else
                    {
                        Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete), "Unreachable AI: " + spawner.name);
                    }
                }
                foreach (Component component in components.Where(c => c.GetType().Namespace == "ZZ"))
                {
                    var serialized = new SerializedObject(component);
                    SerializedProperty property = serialized.GetIterator();
                    while (property.NextVisible(true))
                    {
                        if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == null)
                        {
                            Assert.That(property.objectReferenceInstanceIDValue, Is.Zero, component.name + "/" + property.propertyPath);
                        }
                    }
                }
            }
            finally
            {
                if (opened) { EditorSceneManager.CloseScene(scene, true); }
                if (previous.IsValid() && previous.isLoaded) { SceneManager.SetActiveScene(previous); }
            }
        }
    }
}
