using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.ProBuilder;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.SceneManagement;

/// <summary>
/// Enables Contribute GI on Base/Props geometry and generates lightmap UV2
/// for every ProBuilder mesh in those slices, per the Area lighting bake plan.
/// Effects and Spawners slices stay GI-excluded.
/// </summary>
public sealed class LV01GIFixTests
{
    private const string k_Root = "Assets/_Game/Scenes/Levels/LV01_AbandonedMonastery";

    [Test]
    public void EnableGIAndUV2OnBaseAndProps()
    {
        int totalMeshes = 0;
        string[] areas = { "A01", "A02", "A03", "A04" };
        string[] giSlices = { "Base", "Props" };
        foreach (string area in areas)
        {
            foreach (string slice in giSlices)
            {
                string scenePath = $"{k_Root}/Regions/R01_MonasteryOutskirts/SCN_LV01_R01_{area}_{slice}.unity";
                if (!File.Exists(scenePath))
                {
                    continue;
                }
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

                int meshes = 0;
                foreach (MeshRenderer renderer in Object.FindObjectsOfType<MeshRenderer>())
                {
                    if (!renderer.gameObject.activeInHierarchy)
                    {
                        continue;
                    }
                    StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(renderer.gameObject);
                    GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, flags | StaticEditorFlags.ContributeGI);
                    renderer.receiveGI = ReceiveGI.Lightmaps;
                    meshes++;
                }
                foreach (ProBuilderMesh pb in Object.FindObjectsOfType<ProBuilderMesh>())
                {
                    if (!pb.gameObject.activeInHierarchy)
                    {
                        continue;
                    }
                    EditorMeshUtility.Optimize(pb, true);
                }
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
                totalMeshes += meshes;
                Debug.Log($"GI enabled on {meshes} renderers in {scenePath}");
            }
        }
        Assert.Greater(totalMeshes, 0, "No renderers processed");
    }
}
