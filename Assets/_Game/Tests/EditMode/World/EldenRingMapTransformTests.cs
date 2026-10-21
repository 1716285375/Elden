using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZZ.Tests.EditMode
{
    public class EldenRingMapTransformTests
    {
        [TestCase("../outside")]
        [TestCase("m10_00_00_00/other")]
        public void BatchImporterRejectsInvalidMapIdsBeforeChangingScenes(string mapId)
        {
            Type importer = Type.GetType("ZZ.Editor.EldenRingMapSceneImport, Assembly-CSharp-Editor", true);
            var error = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
                importer.GetMethod("ImportMap").Invoke(null, new object[] { mapId }));
            Assert.That(error.InnerException, Is.TypeOf<ArgumentException>());
        }

        [Test]
        public void EmptyPreparedMapUsesOneWorkspaceAndPreservesActiveScene()
        {
            const string source = "F:/MyProject/RE/EldenRing/Converted/MapWorld/Maps/m10_01_00_99/world.json";
            if (!File.Exists(source))
            {
                Assert.Ignore("The original empty map fixture has not been prepared.");
            }
            const string fixture = "Assets/_Game/Tests/EditMode/World/MapImportWorkspaceTest.unity";
            Assert.That(File.Exists(fixture), Is.False, "Do not overwrite an existing test fixture.");
            Scene previous = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Assert.That(EditorSceneManager.SaveScene(previous, fixture), Is.True);
            try
            {
                Type importer = Type.GetType("ZZ.Editor.EldenRingMapSceneImport, Assembly-CSharp-Editor", true);
                importer.GetMethod("ImportMap").Invoke(null, new object[] { "m10_01_00_99" });
                Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(previous));
                Assert.That(previous.isDirty, Is.False);
                Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    "Assets/_Game/Scenes/Maps/m10/SCN_m10_01_00_99.unity"), Is.Not.Null);
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(fixture);
            }
        }

        [TestCase(0f, 90f, 0f, -1f, 0f, 0f)]
        [TestCase(90f, 0f, 0f, 0f, -1f, 0f)]
        [TestCase(0f, 0f, 0f, 0f, 0f, 1f)]
        public void OriginalRotationMatchesMirroredBasis(float x, float y, float z,
            float expectedX, float expectedY, float expectedZ)
        {
            Type importer = Type.GetType("ZZ.Editor.EldenRingMapSceneImport, Assembly-CSharp-Editor", true);
            Quaternion rotation = (Quaternion)importer.GetMethod("ConvertRotation")
                .Invoke(null, new object[] { new Vector3(x, y, z) });
            Assert.That(Vector3.Distance(rotation * Vector3.forward,
                new Vector3(expectedX, expectedY, expectedZ)), Is.LessThan(0.0001f));
        }

        [Test]
        public void MixedRotationUsesOriginalXzyOrder()
        {
            Type importer = Type.GetType("ZZ.Editor.EldenRingMapSceneImport, Assembly-CSharp-Editor", true);
            Quaternion rotation = (Quaternion)importer.GetMethod("ConvertRotation")
                .Invoke(null, new object[] { new Vector3(90f, 90f, 90f) });
            // Source forward: Rx -> -Y, Rz -> +X, Ry -> -Z. Mirroring X leaves -Z unchanged.
            Assert.That(Vector3.Distance(rotation * Vector3.forward, Vector3.back), Is.LessThan(0.0001f));
        }

        [Test]
        public void SourceDummyMaterialDoesNotOccludeVisibleArchitecture()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/_Game/Art/Environment/EldenRing/StormveilWorld/Materials/SYSTEX_Dummy.mat");
            Assert.That(material, Is.Not.Null);
            Assert.That(material.GetColor("_BaseColor").a, Is.Zero);
            Assert.That(material.GetFloat("_AlphaClip"), Is.EqualTo(1f));
            Assert.That(material.IsKeywordEnabled("_ALPHATEST_ON"), Is.True);
        }

        [Test]
        public void SavedRampartFacesPointAlongSurfaceNormals()
        {
            string[] meshes = AssetDatabase.FindAssets("t:Mesh", new[]
            {
                "Assets/_Game/Art/Environment/EldenRing/StormveilWorld/Models/m10_00_00_00_000004"
            });
            Assert.That(meshes.Length, Is.GreaterThan(0));
            int frontFaces = 0;
            int backFaces = 0;
            foreach (string guid in meshes)
            {
                Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(AssetDatabase.GUIDToAssetPath(guid));
                Vector3[] vertices = mesh.vertices;
                Vector3[] normals = mesh.normals;
                int[] triangles = mesh.triangles;
                int step = Math.Max(3, triangles.Length / 600 / 3 * 3);
                for (int index = 0; index + 2 < triangles.Length; index += step)
                {
                    int first = triangles[index];
                    Vector3 face = Vector3.Cross(vertices[triangles[index + 1]] - vertices[first],
                        vertices[triangles[index + 2]] - vertices[first]);
                    float orientation = Vector3.Dot(face, normals[first]);
                    if (orientation > 0.000001f)
                    {
                        frontFaces++;
                    }
                    else if (orientation < -0.000001f)
                    {
                        backFaces++;
                    }
                }
            }
            Assert.That(frontFaces, Is.GreaterThan(backFaces * 10), "Exterior faces must survive backface culling.");
        }
    }
}
