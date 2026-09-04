using System;
using System.Reflection;
using NUnit.Framework;
using Unity.Netcode;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZZ.Tests
{
    public class RuntimeSpawnedNetworkObjectSceneProcessorTests
    {
        private static readonly FieldInfo s_spawnCountField = typeof(NetworkObject).GetField(
            "m_SpawnCount",
            BindingFlags.Instance | BindingFlags.NonPublic);

        [Test]
        public void OnProcessSceneRemovesRuntimeSpawnedNetworkObject()
        {
            Scene testScene = EditorSceneManager.NewPreviewScene();
            GameObject runtimeObject = new("Runtime Enemy");
            SceneManager.MoveGameObjectToScene(runtimeObject, testScene);
            NetworkObject networkObject = runtimeObject.AddComponent<NetworkObject>();

            try
            {
                Assert.That(s_spawnCountField, Is.Not.Null);
                s_spawnCountField.SetValue(networkObject, 1);

                ProcessScene(testScene);

                Assert.That(runtimeObject == null, Is.True);
            }
            finally
            {
                if (runtimeObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(runtimeObject);
                }

                EditorSceneManager.ClosePreviewScene(testScene);
            }
        }

        [Test]
        public void OnProcessScenePreservesUnspawnedNetworkObject()
        {
            Scene testScene = EditorSceneManager.NewPreviewScene();
            GameObject authoredObject = new("Authored Network Object");
            SceneManager.MoveGameObjectToScene(authoredObject, testScene);
            authoredObject.AddComponent<NetworkObject>();

            try
            {
                ProcessScene(testScene);

                Assert.That(authoredObject != null, Is.True);
            }
            finally
            {
                if (authoredObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(authoredObject);
                }

                EditorSceneManager.ClosePreviewScene(testScene);
            }
        }

        private static void ProcessScene(Scene scene)
        {
            Type processorType = Type.GetType(
                "ZZ.Editor.RuntimeSpawnedNetworkObjectSceneProcessor, Assembly-CSharp-Editor");
            Assert.That(processorType, Is.Not.Null);
            MethodInfo processScene = processorType.GetMethod("OnProcessScene");
            Assert.That(processScene, Is.Not.Null);
            processScene.Invoke(Activator.CreateInstance(processorType), new object[]
            {
                scene,
                null
            });
        }
    }
}
