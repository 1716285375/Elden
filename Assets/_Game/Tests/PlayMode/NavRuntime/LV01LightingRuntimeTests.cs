using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Verifies that dynamically lit nature renderers remain available in the
/// still-loaded Area after a neighboring Area unloads.
/// </summary>
public sealed class LV01LightingRuntimeTests
{
    private const string k_Region = "SCN_LV01_R01_";

    [UnityTest]
    public IEnumerator NatureRenderersFollowAreaLifecycle()
    {
        LoadSceneIfNeeded("SCN_LV01_AbandonedMonastery");
        LoadArea("A01");
        LoadArea("A02");
        yield return WaitFrames(120);

        MeshRenderer natureRenderer = FindAreaRenderer("SCN_LV01_R01_A02_Base");
        Assert.IsNotNull(natureRenderer, "A02 nature renderer not found");
        Assert.IsTrue(natureRenderer.enabled, "A02 nature renderer is disabled after load");
        Assert.IsNotNull(
            Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .FirstOrDefault(light => light.type == LightType.Directional),
            "Master nature directional light is missing");

        UnloadArea("A01");
        yield return WaitFrames(60);

        MeshRenderer rendererAfter = FindAreaRenderer("SCN_LV01_R01_A02_Base");
        Assert.IsNotNull(rendererAfter, "A02 nature renderer missing after A01 unload");
        Assert.IsTrue(rendererAfter.enabled, "A02 nature renderer disabled after A01 unload");

        yield return WaitFrames(60);
    }

    private static MeshRenderer FindAreaRenderer(string sceneName)
    {
        return Object.FindObjectsByType<MeshRenderer>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None)
            .FirstOrDefault(renderer =>
                renderer.gameObject.scene.name == sceneName && renderer.enabled);
    }

    private static void LoadArea(string area)
    {
        LoadSceneIfNeeded(k_Region + area + "_Base");
        LoadSceneIfNeeded(k_Region + area + "_Props");
        LoadSceneIfNeeded(k_Region + area + "_Effects");
    }

    private static void LoadSceneIfNeeded(string sceneName)
    {
        if (!SceneManager.GetSceneByName(sceneName).isLoaded)
        {
            SceneManager.LoadScene(sceneName, LoadSceneMode.Additive);
        }
    }

    private static void UnloadArea(string area)
    {
        SceneManager.UnloadSceneAsync(k_Region + area + "_Base");
        SceneManager.UnloadSceneAsync(k_Region + area + "_Props");
        SceneManager.UnloadSceneAsync(k_Region + area + "_Effects");
    }

    private static IEnumerator WaitFrames(int frames)
    {
        for (int i = 0; i < frames; i++)
        {
            yield return null;
        }
    }
}
