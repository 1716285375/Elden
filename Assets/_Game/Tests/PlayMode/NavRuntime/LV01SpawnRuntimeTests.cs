using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>Verifies that LV01 starts on solid ground inside the initial streaming trigger.</summary>
public sealed class LV01SpawnRuntimeTests
{
    private static readonly Vector3 k_ExpectedPosition =
        new(26f, 6.19999981f, 42.7999992f);
    private static readonly Quaternion k_ExpectedRotation =
        new(0f, 0.70710683f, 0f, 0.70710683f);

    [UnityTest]
    public IEnumerator SpawnIsSupportedAndInsideInitialAreaTrigger()
    {
        if (!SceneManager.GetSceneByName("SCN_LV01_AbandonedMonastery").isLoaded)
        {
            SceneManager.LoadScene("SCN_LV01_AbandonedMonastery", LoadSceneMode.Additive);
            yield return null;
        }

        if (!SceneManager.GetSceneByName("SCN_LV01_R01_A01_Base").isLoaded)
        {
            SceneManager.LoadScene("SCN_LV01_R01_A01_Base", LoadSceneMode.Additive);
            yield return null;
        }

        yield return null;
        Scene masterScene = SceneManager.GetSceneByName("SCN_LV01_AbandonedMonastery");
        Transform spawn = FindTransform(masterScene, "Player Spawn Point");
        Transform initialTrigger = FindTransform(masterScene, "A01_CliffPath");
        Terrain landTerrain = Object.FindObjectsByType<Terrain>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None)
            .FirstOrDefault(terrain => terrain.name == "LandTerrain");

        Assert.IsNotNull(spawn, "Player Spawn Point is missing");
        Assert.IsNotNull(initialTrigger, "Spawn Area Load Trigger is missing");
        Assert.IsNotNull(landTerrain, "LandTerrain is missing");
        Assert.That(Vector3.Distance(spawn.position, k_ExpectedPosition), Is.LessThan(0.001f));
        Assert.That(Quaternion.Angle(spawn.rotation, k_ExpectedRotation), Is.LessThan(0.01f));
        Assert.That(Vector3.Distance(spawn.localScale, Vector3.one), Is.LessThan(0.001f));

        TerrainCollider terrainCollider = landTerrain.GetComponent<TerrainCollider>();
        Assert.IsNotNull(terrainCollider, "LandTerrain has no TerrainCollider");
        Assert.IsTrue(terrainCollider.enabled, "LandTerrain TerrainCollider is disabled");
        Assert.IsFalse(terrainCollider.isTrigger, "LandTerrain TerrainCollider cannot be a trigger");
        Assert.AreSame(
            landTerrain.terrainData,
            terrainCollider.terrainData,
            "Terrain and TerrainCollider use different TerrainData");

        int playerLayer = LayerMask.NameToLayer("Player");
        Assert.That(playerLayer, Is.GreaterThanOrEqualTo(0), "Player layer is missing");
        Assert.IsFalse(
            Physics.GetIgnoreLayerCollision(0, playerLayer),
            "Default terrain layer does not collide with Player layer");

        Physics.SyncTransforms();
        RaycastHit support = Physics.RaycastAll(
                spawn.position + Vector3.up * 1000f,
                Vector3.down,
                2000f,
                ~0,
                QueryTriggerInteraction.Ignore)
            .Where(hit => hit.point.y <= spawn.position.y + 0.25f)
            .OrderByDescending(hit => hit.point.y)
            .FirstOrDefault();
        Assert.IsNotNull(support.collider, "No solid collider exists below Player Spawn Point");
        Assert.That(
            spawn.position.y - support.point.y,
            Is.InRange(-0.25f, 3f),
            "Player Spawn Point is too far from its supporting collider");

        Collider triggerCollider = initialTrigger.GetComponent<Collider>();
        Assert.IsNotNull(triggerCollider, "Spawn Area Load Trigger has no Collider");
        Assert.IsTrue(triggerCollider.enabled, "Spawn Area Load Trigger Collider is disabled");
        Assert.IsTrue(triggerCollider.isTrigger, "Spawn Area Load Trigger Collider is not a trigger");
        Assert.IsTrue(
            triggerCollider.bounds.Contains(spawn.position),
            "Spawn Area Load Trigger does not contain Player Spawn Point");
    }

    private static Transform FindTransform(Scene scene, string objectName)
    {
        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .FirstOrDefault(candidate => candidate.name == objectName);
    }
}
