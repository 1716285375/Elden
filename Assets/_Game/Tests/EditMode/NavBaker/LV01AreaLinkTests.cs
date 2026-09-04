using System.Collections.Generic;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Verifies the nature-map NavMesh links that join the eight streaming Areas.
/// The integration tool owns link creation; this test is intentionally read-only.
/// </summary>
public sealed class LV01AreaLinkTests
{
    private const string k_Root = "Assets/_Game/Scenes/Levels/LV01_AbandonedMonastery";

    private static readonly string[] s_baseScenes =
    {
        "/Regions/R01_MonasteryOutskirts/SCN_LV01_R01_A01_Base.unity",
        "/Regions/R01_MonasteryOutskirts/SCN_LV01_R01_A02_Base.unity",
        "/Regions/R01_MonasteryOutskirts/SCN_LV01_R01_A03_Base.unity",
        "/Regions/R01_MonasteryOutskirts/SCN_LV01_R01_A04_Base.unity",
        "/Regions/R02_MonasteryInterior/SCN_LV01_R02_A01_Base.unity",
        "/Regions/R03_Catacombs/SCN_LV01_R03_A01_Base.unity",
        "/Regions/R04_BellTower/SCN_LV01_R04_A01_Base.unity",
        "/Regions/R05_BossSanctum/SCN_LV01_R05_A01_Base.unity"
    };

    [Test]
    public void NatureLinksCoverEveryAreaJunction()
    {
        var linkNames = new HashSet<string>();
        foreach (string relativePath in s_baseScenes)
        {
            Scene scene = EditorSceneManager.OpenScene(k_Root + relativePath, OpenSceneMode.Single);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (NavMeshLink link in root.GetComponentsInChildren<NavMeshLink>(true))
                {
                    Assert.That(link.name, Does.StartWith("Nature Link "));
                    Assert.That(link.bidirectional, Is.True);
                    Assert.That(link.width, Is.GreaterThanOrEqualTo(2f));
                    Assert.That(linkNames.Add(link.name), Is.True, $"Duplicate link: {link.name}");
                }
            }
        }

        Assert.That(linkNames, Has.Count.EqualTo(8));
    }
}
