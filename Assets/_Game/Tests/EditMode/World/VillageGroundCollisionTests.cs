using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace ZZ.Tests
{
    public class VillageGroundCollisionTests
    {
        private const string k_Scene = "Assets/_Game/Scenes/Levels/LV02_VikingVillage/SCN_LV02_VikingVillage.unity";

        [Test]
        public void VillageGroundAndWalkwaysUsePlayerGroundLayers()
        {
            Scene scene = EditorSceneManager.OpenScene(k_Scene, OpenSceneMode.Single);
            GameObject content = scene.GetRootGameObjects().Single(root => root.name == "Content");
            GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Prefabs/Characters/Player/Player.prefab");
            Component locomotion = player.GetComponent("PlayerLocomotionManager");
            int mask = new SerializedObject(locomotion).FindProperty("m_groundLayers").intValue;
            MeshFilter[] floors = content.GetComponentsInChildren<MeshFilter>(true).Where(filter =>
                filter.gameObject.activeInHierarchy && (filter.name.StartsWith("prop_boardwalk") ||
                filter.name == "terrain_near_01")).ToArray();
            Assert.That(floors.Length, Is.GreaterThan(10));
            foreach (MeshFilter floor in floors)
            {
                Assert.That(mask & (1 << floor.gameObject.layer), Is.Not.Zero, floor.name + " ground layer");
                Collider collider = floor.GetComponent<Collider>();
                Assert.That(collider, Is.Not.Null, floor.name + " missing collision");
                Assert.That(collider.enabled && !collider.isTrigger, Is.True, floor.name);
                Assert.That(Physics.GetIgnoreLayerCollision(player.layer, floor.gameObject.layer), Is.False);
            }
        }

        [Test]
        public void PlayerCapsuleTraversesVillagePathsWithoutFallingThroughFloor()
        {
            Scene scene = EditorSceneManager.OpenScene(k_Scene, OpenSceneMode.Single);
            Physics.SyncTransforms();
            Transform gameplay = scene.GetRootGameObjects().Single(root => root.name == "_VillageGameplay").transform;
            Vector3 entry = gameplay.Find("Player Spawn Point").position;
            Assert.That(NavMesh.SamplePosition(entry, out NavMeshHit start, 3f, NavMesh.AllAreas), Is.True);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Prefabs/Characters/Player/Player.prefab");
            CharacterController template = prefab.GetComponent<CharacterController>();
            var probe = new GameObject("Village Capsule Traversal Probe");
            probe.layer = prefab.layer;
            var controller = probe.AddComponent<CharacterController>();
            controller.height = template.height;
            controller.radius = template.radius;
            controller.center = template.center;
            controller.stepOffset = template.stepOffset;
            controller.slopeLimit = template.slopeLimit;
            controller.skinWidth = template.skinWidth;
            try
            {
                int traversed = 0;
                foreach (Transform destination in gameplay.GetComponentsInChildren<Transform>().Where(item =>
                    item.GetComponent("AICharacterSpawner") != null).Take(5))
                {
                    Assert.That(NavMesh.SamplePosition(destination.position, out NavMeshHit target, 3f, NavMesh.AllAreas), Is.True);
                    var path = new NavMeshPath();
                    Assert.That(NavMesh.CalculatePath(start.position, target.position, NavMesh.AllAreas, path), Is.True);
                    Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete));
                    controller.enabled = false;
                    probe.transform.position = start.position + Vector3.up * 0.15f;
                    controller.enabled = true;
                    foreach (Vector3 corner in path.corners.Skip(1))
                    {
                        int steps = 0;
                        while (Vector2.Distance(new Vector2(probe.transform.position.x, probe.transform.position.z),
                            new Vector2(corner.x, corner.z)) > 0.15f && steps++ < 2000)
                        {
                            Vector3 direction = corner - probe.transform.position;
                            direction.y = 0f;
                            controller.Move(direction.normalized * 0.08f + Vector3.down * 0.12f);
                            Assert.That(probe.transform.position.y, Is.GreaterThan(-2f), destination.name + " fell through ground");
                        }
                        Assert.That(steps, Is.LessThan(2000), "Blocked route to " + destination.name);
                    }
                    traversed++;
                }
                Assert.That(traversed, Is.EqualTo(5));
            }
            finally { Object.DestroyImmediate(probe); }
        }
    }
}
