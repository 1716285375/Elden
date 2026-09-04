using System.Reflection;
using Unity.Netcode;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZZ.Editor
{
    /// <summary>
    /// Removes runtime-spawned network objects before NGO processes an Editor scene copy.
    /// </summary>
    [BuildCallbackVersion(1)]
    public sealed class RuntimeSpawnedNetworkObjectSceneProcessor : IProcessSceneWithReport
    {
        private const int k_CallbackOrder = -1000;

        private static readonly FieldInfo s_spawnCountField = typeof(NetworkObject).GetField(
            "m_SpawnCount",
            BindingFlags.Instance | BindingFlags.NonPublic);

        public int callbackOrder => k_CallbackOrder;

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                NetworkObject[] networkObjects =
                    root.GetComponentsInChildren<NetworkObject>(true);
                foreach (NetworkObject networkObject in networkObjects)
                {
                    if (networkObject == null ||
                        networkObject.InScenePlaced ||
                        !HasBeenSpawned(networkObject))
                    {
                        continue;
                    }

                    Object.DestroyImmediate(networkObject.gameObject);
                }
            }
        }

        private static bool HasBeenSpawned(NetworkObject networkObject)
        {
            return s_spawnCountField?.GetValue(networkObject) is int spawnCount
                ? spawnCount > 0
                : networkObject.IsSpawned;
        }
    }
}
