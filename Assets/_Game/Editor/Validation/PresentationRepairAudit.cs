using System.IO;
using System.Collections;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ZZ.Editor
{
    public static class PresentationRepairAudit
    {
        [MenuItem("Tools/ZZ/Validation/Verify Runtime Audio Output")]
        public static void VerifyRuntimeAudioOutput()
        {
            if (!Application.isPlaying || WorldSaveGameManager.Instance == null)
            {
                throw new System.InvalidOperationException("Run the title scene first.");
            }
            File.WriteAllText("Docs/PresentationRepair/runtime-audio.txt", "RUNNING\n");
            WorldSaveGameManager.Instance.StartCoroutine(ObserveAudio());
        }

        private static IEnumerator ObserveAudio()
        {
            ScheduledMusicPlayer music = Object.FindFirstObjectByType<ScheduledMusicPlayer>();
            if (music == null || !music.IsScheduled)
            {
                File.AppendAllText("Docs/PresentationRepair/runtime-audio.txt", "FAIL: title music not scheduled.\n");
                yield break;
            }
            var samples = new float[1024];
            music.enabled = false;
            try
            {
                yield return new WaitForSecondsRealtime(0.15f);
                foreach (string stage in new[] { "Hover", "Confirm", "Music" })
                {
                    if (stage == "Hover") { PlayerUIManager.Instance.PlayMenuHoverSound(); }
                    else if (stage == "Confirm") { PlayerUIManager.Instance.PlayMenuConfirmSound(); }
                    else { music.enabled = true; }
                    double end = Time.realtimeSinceStartupAsDouble + (stage == "Music" ? 3d : 0.5d);
                    float peak = 0f;
                    while (Time.realtimeSinceStartupAsDouble < end)
                    {
                        AudioListener.GetOutputData(samples, 0);
                        foreach (float sample in samples) { peak = Mathf.Max(peak, Mathf.Abs(sample)); }
                        yield return null;
                    }
                    File.AppendAllText("Docs/PresentationRepair/runtime-audio.txt",
                        $"{(peak > 0.00001f ? "PASS" : "FAIL")}: {stage} listener output peak={peak:F6}.\n");
                }
            }
            finally { music.enabled = true; }
        }

        [MenuItem("Tools/ZZ/Validation/Audit Audio And Weapon Presentation")]
        public static void Audit()
        {
            var report = new StringBuilder();
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/_Game" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                report.AppendLine($"CLIP {path}: seconds={clip.length:F3}; load={clip.loadState}");
            }
            foreach (string path in new[]
            {
                "Assets/_Game/Prefabs/Characters/Player/Player.prefab",
                "Assets/_Game/Prefabs/World/Managers/World Sound FX Manager.prefab",
                "Assets/_Game/Prefabs/Equipment/Weapons/Melee Weapons/Straight Sword.prefab",
                "Assets/_Game/Prefabs/Equipment/Weapons/Melee Weapons/Medium Shield.prefab"
            })
            {
                GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (root == null) { report.AppendLine("MISSING " + path); continue; }
                foreach (Component component in root.GetComponentsInChildren<Component>(true))
                {
                    if (component == null) { continue; }
                    var data = new SerializedObject(component);
                    SerializedProperty property = data.GetIterator();
                    while (property.NextVisible(true))
                    {
                        if (property.propertyType == SerializedPropertyType.ObjectReference &&
                            (property.type.Contains("AudioClip") || property.type.Contains("AudioSource")))
                        {
                            report.AppendLine($"REF {path}/{component.name}/{component.GetType().Name}/{property.propertyPath}={property.objectReferenceValue?.name ?? "NULL"}");
                        }
                    }
                }
                foreach (WeaponModelInstantiationSlot slot in root.GetComponentsInChildren<WeaponModelInstantiationSlot>(true))
                {
                    report.AppendLine($"SLOT {slot.WeaponModelSlot}: parent={slot.transform.parent.name}; local={slot.transform.localPosition}; rotation={slot.transform.localEulerAngles}");
                }
            }
            Directory.CreateDirectory("Docs/PresentationRepair");
            File.WriteAllText("Docs/PresentationRepair/audit.txt", report.ToString());
            Debug.Log("Presentation audit written.");
        }
    }
}
