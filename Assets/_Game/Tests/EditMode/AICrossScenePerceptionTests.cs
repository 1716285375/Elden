using System;
using System.Reflection;
using NUnit.Framework;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZZ.Tests
{
    public sealed class AICrossScenePerceptionTests
    {
        [TestCase(true, true)]
        [TestCase(false, false)]
        public void PerceptionAcceptsActivePlayersAcrossStreamingScenes(bool targetActive, bool expected)
        {
            Scene previous = SceneManager.GetActiveScene();
            Scene world = previous;
            string scenePath = "Assets/_Game/Tests/EditMode/PerceptionRegion_" + Guid.NewGuid().ToString("N") + ".unity";
            Assert.That(AssetDatabase.CopyAsset("Assets/_Game/Scenes/Frontend/SCN_MainMenu.unity", scenePath), Is.True);
            Scene region = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            GameObject player = null;
            GameObject enemy = null;
            try
            {
                player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/_Game/Prefabs/Characters/Player/Player.prefab"), world);
                enemy = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/_Game/Prefabs/Characters/AI/Undead AI.prefab"), region);
                Component playerManager = player.GetComponent("PlayerManager");
                player.SetActive(targetActive);
                Component ai = enemy.GetComponent("AICharacterManager");
                typeof(NetworkBehaviour).GetProperty("IsSpawned").SetValue(playerManager, true);
                MethodInfo validate = ai.GetType().GetMethod("IsValidTarget", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.That(validate.Invoke(ai, new object[] { playerManager }), Is.EqualTo(expected));
                player.SetActive(false);
                Assert.That(validate.Invoke(ai, new object[] { playerManager }), Is.False);
                typeof(NetworkBehaviour).GetProperty("IsSpawned").SetValue(playerManager, false);
            }
            finally
            {
                if (player != null)
                {
                    typeof(NetworkBehaviour).GetProperty("IsSpawned").SetValue(player.GetComponent("PlayerManager"), false);
                    UnityEngine.Object.DestroyImmediate(player);
                }
                if (enemy != null)
                {
                    UnityEngine.Object.DestroyImmediate(enemy);
                }
                EditorSceneManager.CloseScene(region, true);
                AssetDatabase.DeleteAsset(scenePath);
                SceneManager.SetActiveScene(previous);
            }
        }
    }
}
