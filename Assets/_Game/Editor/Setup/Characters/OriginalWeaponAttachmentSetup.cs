using System;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace ZZ.Editor
{
    /// <summary>Installs regulation-selected dummy placements without altering gameplay asset identities.</summary>
    public static class OriginalWeaponAttachmentSetup
    {
        [MenuItem("Tools/ZZ/Elden Ring/Install Original Equipment Dummy Placements")]
        public static void Apply()
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException("Stop Play Mode before installing original equipment placements.");
            }

            JObject source = JObject.Parse(File.ReadAllText(
                "F:/MyProject/RE/EldenRing/Converted/WeaponAttachment/source.json"));
            const string referencePath = "Assets/_Game/Art/Equipment/EldenRing/OriginalAttachments.json";
            File.WriteAllText(referencePath, new JObject
            {
                ["Attachments"] = source["Attachments"].DeepClone(),
                ["Parameters"] = source["Parameters"].DeepClone()
            }.ToString());
            AssetDatabase.ImportAsset(referencePath, ImportAssetOptions.ForceSynchronousImport);
            EldenRingArtImport.RestoreOriginalWeaponMotionCurves();
            EquipmentVisualRefresh.RestoreOriginalRigidEquipmentAxes();
            var report = new StringBuilder();
            foreach (var entry in new[]
            {
                (200, "Assets/_Game/Prefabs/Equipment/Weapons/Melee Weapons/Straight Sword.prefab"),
                (201, "Assets/_Game/Prefabs/Equipment/Weapons/Melee Weapons/Broadsword.prefab"),
                (2020, "Assets/_Game/Prefabs/Equipment/Weapons/Melee Weapons/Medium Shield.prefab"),
                (1410, "Assets/_Game/Prefabs/Equipment/Weapons/Longbow.prefab"),
                (1330, "Assets/_Game/Prefabs/Abilities/Incantation Catalyst.prefab")
            })
            {
                int absorption = source["Weapons"].Where(row => (int)row["Model"] == entry.Item1)
                    .Select(row => (int)row["Absorp"]).Distinct().Single();
                JToken fields = source["Parameters"].Single(row => (int)row["Id"] == absorption)["Fields"];
                GameObject root = PrefabUtility.LoadPrefabContents(entry.Item2);
                try
                {
                    OriginalWeaponAttachment attachment = root.GetComponent<OriginalWeaponAttachment>() ??
                        root.AddComponent<OriginalWeaponAttachment>();
                    var serialized = new SerializedObject(attachment);
                    foreach (var pair in new[] { ("m_rightHand", "right_0"), ("m_leftHand", "left_0"),
                        ("m_twoHandRight", "both_0"), ("m_twoHandLeft", "leftBoth_0"),
                        ("m_storedRight", "rightHang_0"), ("m_storedLeft", "leftHang_0") })
                    {
                        int id = (int)fields[pair.Item2] % 1000;
                        JToken dummy = source["Attachments"].Single(row => (int)row["Id"] == id);
                        SerializedProperty placement = serialized.FindProperty(pair.Item1);
                        placement.FindPropertyRelative("Bone").stringValue = (string)dummy["Bone"];
                        placement.FindPropertyRelative("Position").vector3Value = ReadVector(dummy["Position"]);
                        float[] values = dummy["Rotation"].ToObject<float[]>();
                        placement.FindPropertyRelative("Rotation").quaternionValue =
                            new Quaternion(values[0], values[1], values[2], values[3]).normalized;
                        report.AppendLine(entry.Item1 + " " + pair.Item2 + " -> dummy " + id + " / " + dummy["Bone"]);
                    }
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    BoxCollider collider = root.GetComponentInChildren<MeleeWeaponDamageCollider>(true)?.GetComponent<BoxCollider>();
                    MeshFilter mesh = root.GetComponentsInChildren<MeshFilter>(true)
                        .Single(item => item.GetComponent<MeshRenderer>()?.enabled == true);
                    if (collider != null)
                    {
                        collider.transform.localPosition = Vector3.zero;
                        collider.transform.localRotation = Quaternion.identity;
                        collider.transform.localScale = Vector3.one;
                        collider.center = mesh.sharedMesh.bounds.center;
                        collider.size = Vector3.Max(mesh.sharedMesh.bounds.size, Vector3.one * 0.04f);
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, entry.Item2);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Docs/WeaponCoordinateRepair");
            File.WriteAllText("Docs/WeaponCoordinateRepair/installed-placements.txt", report.ToString());
        }

        private static Vector3 ReadVector(JToken token)
        {
            float[] values = token.ToObject<float[]>();
            return new Vector3(values[0], values[1], values[2]);
        }
    }
}
