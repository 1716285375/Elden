using System.Collections;
using System.Linq;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Verifies per-Area NavMeshData streaming behavior in play mode:
/// single-area sampling, cross-area paths over NavMeshLinks, removal on unload,
/// and re-registration on reload. Frame-based waits avoid timeScale dependence.
/// </summary>
public sealed class LV01AreaNavRuntimeTests
{
    private static readonly Vector2 k_A01Road = new(130f, 76f);
    private static readonly Vector2 k_A02Floor = new(130f, 84f);
    private const float k_SampleRadius = 8f;

    [UnityTest]
    public IEnumerator NavMeshFollowsAreaLifecycle()
    {
        if (!SceneManager.GetSceneByName("SCN_LV01_AbandonedMonastery").isLoaded)
        {
            SceneManager.LoadScene("SCN_LV01_AbandonedMonastery", LoadSceneMode.Additive);
            yield return null;
        }

        SceneManager.LoadScene("SCN_LV01_R01_A01_Base", LoadSceneMode.Additive);
        yield return null;
        SceneManager.LoadScene("SCN_LV01_R01_A02_Base", LoadSceneMode.Additive);
        yield return WaitFrames(90);

        Terrain terrain = Object.FindFirstObjectByType<Terrain>();
        Assert.IsNotNull(terrain, "Master terrain missing after load");
        Vector3 a01Road = OnTerrain(terrain, k_A01Road);
        Vector3 a02Floor = OnTerrain(terrain, k_A02Floor);

        Assert.IsTrue(
            NavMesh.SamplePosition(a01Road, out _, k_SampleRadius, NavMesh.AllAreas),
            "A01 navmesh missing after load");
        Assert.AreEqual(
            2,
            CountTargetAreaSurfaces(),
            "Expected 2 active surfaces after loading A01+A02");

        NavMeshLink areaLink = Object.FindObjectsByType<NavMeshLink>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None)
            .FirstOrDefault(link => link.name ==
                "Nature Link A01_CliffPath <-> A02_Graveyard");
        Assert.IsNotNull(areaLink, "A01-A02 NavMeshLink missing after load");
        Vector3 linkStart = areaLink.transform.TransformPoint(areaLink.startPoint);
        Vector3 linkEnd = areaLink.transform.TransformPoint(areaLink.endPoint);
        Assert.IsTrue(
            NavMesh.SamplePosition(
                linkStart,
                out NavMeshHit startHit,
                k_SampleRadius,
                NavMesh.AllAreas),
            $"A01 link endpoint is off NavMesh: {linkStart}");
        Assert.IsTrue(
            NavMesh.SamplePosition(
                linkEnd,
                out NavMeshHit endHit,
                k_SampleRadius,
                NavMesh.AllAreas),
            $"A02 link endpoint is off NavMesh: {linkEnd}");

        NavMeshPath path = new NavMeshPath();
        bool hasPath = NavMesh.CalculatePath(
            startHit.position,
            endHit.position,
            NavMesh.AllAreas,
            path);
        Assert.IsTrue(hasPath, "No path computed between A01 and A02");
        Assert.AreEqual(NavMeshPathStatus.PathComplete, path.status, "Cross-area path not complete");
        Debug.Log($"Cross-area path corners: {path.corners.Length}");

        AsyncOperation unload = SceneManager.UnloadSceneAsync("SCN_LV01_R01_A01_Base");
        yield return new WaitUntil(() => unload == null || unload.isDone);
        yield return WaitFrames(30);

        Assert.AreEqual(1, CountTargetAreaSurfaces(), "A01 surface not removed after unload");

        SceneManager.LoadScene("SCN_LV01_R01_A01_Base", LoadSceneMode.Additive);
        yield return WaitFrames(90);

        Assert.AreEqual(
            2,
            CountTargetAreaSurfaces(),
            "A01 surface not re-registered after reload");
        Assert.IsTrue(
            NavMesh.SamplePosition(a01Road, out _, k_SampleRadius, NavMesh.AllAreas),
            "A01 navmesh missing after reload");
    }

    private static Vector3 OnTerrain(Terrain terrain, Vector2 position)
    {
        Vector3 worldPosition = new(position.x, terrain.transform.position.y, position.y);
        worldPosition.y = terrain.SampleHeight(worldPosition);
        return worldPosition;
    }

    private static int CountTargetAreaSurfaces()
    {
        return NavMeshSurface.activeSurfaces.Count(surface =>
            surface.gameObject.scene.name == "SCN_LV01_R01_A01_Base" ||
            surface.gameObject.scene.name == "SCN_LV01_R01_A02_Base");
    }

    private static IEnumerator WaitFrames(int frames)
    {
        for (int i = 0; i < frames; i++)
        {
            yield return null;
        }
    }
}
