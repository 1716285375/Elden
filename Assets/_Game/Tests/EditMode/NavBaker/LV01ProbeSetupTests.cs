using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Adds one baked ReflectionProbe per Area (Effects slice) named per the
/// Area ownership convention RP_LV01_R01_Axx_xxx. Idempotent.
/// </summary>
public sealed class LV01ProbeSetupTests
{
    private const string k_Root = "Assets/_Game/Scenes/Levels/LV01_AbandonedMonastery";
    private const string k_Region = k_Root + "/Regions/R01_MonasteryOutskirts";

    private static readonly (string Area, string Name, string Parent, Vector3 Pos, Vector3 Size)[] k_Probes =
    {
        ("A01", "RP_LV01_R01_A01_CliffPath", "A01_CliffPath", new Vector3(3.4f, 3.5f, 28.8f), new Vector3(35f, 15f, 80f)),
        ("A02", "RP_LV01_R01_A02_Graveyard", "A02_Graveyard", new Vector3(16f, 4.5f, 74f), new Vector3(48f, 15f, 60f)),
        ("A03", "RP_LV01_R01_A03_MainGate", "A03_MainGate", new Vector3(0f, 6.5f, 110f), new Vector3(30f, 12f, 18f)),
        ("A04", "RP_LV01_R01_A04_GateTower", "A04_GateTower", new Vector3(18f, 8f, 99f), new Vector3(20f, 20f, 30f)),
    };

    [Test]
    public void AddAreaReflectionProbes()
    {
        foreach ((string area, string name, string parent, Vector3 pos, Vector3 size) in k_Probes)
        {
            string scenePath = $"{k_Region}/SCN_LV01_R01_{area}_Effects.unity";
            if (!File.Exists(scenePath))
            {
                continue;
            }
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            GameObject parentGo = GameObject.Find(parent);
            Assert.IsNotNull(parentGo, $"Parent {parent} not found in {scenePath}");
            GameObject probeGo = GameObject.Find(name);
            if (probeGo == null)
            {
                probeGo = new GameObject(name);
                probeGo.transform.SetParent(parentGo.transform, false);
                probeGo.transform.position = pos;
                ReflectionProbe probe = probeGo.AddComponent<ReflectionProbe>();
                probe.mode = ReflectionProbeMode.Baked;
                probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
                probe.size = size;
            }
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Debug.Log($"Probe ready: {name} in {scenePath}");
        }
    }
}
