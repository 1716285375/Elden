using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine.ProBuilder;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Audits Contribute GI participation and ProBuilder lightmap UV2 coverage
/// across the R01 area slices. Writes a report file for offline review.
/// </summary>
public sealed class LV01GIAuditTests
{
    private const string k_Root = "Assets/_Game/Scenes/Levels/LV01_AbandonedMonastery";
    private static readonly string k_ReportPath = Path.GetFullPath("Logs/LV01GIAudit.txt");

    [Test]
    public void AuditAreaGIAndUV2()
    {
        StringBuilder report = new StringBuilder();
        int totalNoUV2 = 0;
        int totalSpawnerGI = 0;

        string[] areas = { "A01", "A02", "A03", "A04" };
        string[] slices = { "Base", "Props", "Effects", "Spawners" };
        foreach (string area in areas)
        {
            foreach (string slice in slices)
            {
                string scenePath = $"{k_Root}/Regions/R01_MonasteryOutskirts/SCN_LV01_R01_{area}_{slice}.unity";
                if (!File.Exists(scenePath))
                {
                    continue;
                }
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

                int renderers = 0;
                int giTrue = 0;
                int pbMissingUV2 = 0;
                foreach (MeshRenderer renderer in Object.FindObjectsOfType<MeshRenderer>())
                {
                    if (!renderer.gameObject.activeInHierarchy)
                    {
                        continue;
                    }
                    renderers++;
                    bool contributeGI =
                        (GameObjectUtility.GetStaticEditorFlags(renderer.gameObject) & StaticEditorFlags.ContributeGI) != 0;
                    if (contributeGI)
                    {
                        giTrue++;
                        if (slice == "Spawners")
                        {
                            totalSpawnerGI++;
                            report.AppendLine($"VIOLATION: Spawner contributes GI: {renderer.gameObject.name} in {scenePath}");
                        }
                    }
                }

                foreach (ProBuilderMesh pb in Object.FindObjectsOfType<ProBuilderMesh>())
                {
                    if (!pb.gameObject.activeInHierarchy)
                    {
                        continue;
                    }
                    MeshFilter filter = pb.GetComponent<MeshFilter>();
                    bool hasUV2 = filter != null && filter.sharedMesh != null
                        && filter.sharedMesh.uv2 != null && filter.sharedMesh.uv2.Length > 0;
                    if (!hasUV2)
                    {
                        pbMissingUV2++;
                        totalNoUV2++;
                        report.AppendLine($"NO UV2: {pb.name} in {scenePath}");
                    }
                }

                report.AppendLine($"{area}/{slice}: renderers={renderers}, ContributeGI={giTrue}, PB missing UV2={pbMissingUV2}");
            }
        }

        report.AppendLine($"TOTAL: PB objects missing UV2 = {totalNoUV2}, Spawner objects with GI = {totalSpawnerGI}");
        File.WriteAllText(k_ReportPath, report.ToString());
        Debug.Log($"GI audit written to {k_ReportPath}");
    }
}
