using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace ZZ.Tests
{
    public sealed class OriginalCollisionWindingTests
    {
        [Test]
        public void MirroringPreservesTheFloorFacingAndDoesNotMutateSourceTopology()
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("ZZ.Editor.OriginalAssetCollisionSetup")).First(value => value != null);
            int[] original = { 0, 1, 2 };
            int[] mirrored = (int[])type.GetMethod("MirrorWinding").Invoke(null, new object[] { original });
            Vector3[] vertices = { Vector3.zero, Vector3.forward, Vector3.right };
            Vector3 normal = Vector3.Cross(vertices[1], vertices[2]);
            vertices = vertices.Select(vertex => Vector3.Scale(vertex, new Vector3(-1f, 1f, 1f))).ToArray();
            Vector3 mirroredNormal = Vector3.Cross(vertices[mirrored[1]], vertices[mirrored[2]]);
            Assert.That(Vector3.Dot(normal, mirroredNormal), Is.GreaterThan(0f));
            Assert.That(original, Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(mirrored, Is.EqualTo(new[] { 0, 2, 1 }));
            var error = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
                type.GetMethod("MirrorWinding").Invoke(null, new object[] { new[] { 0, 1 } }));
            Assert.That(error.InnerException, Is.TypeOf<System.IO.InvalidDataException>());
        }
    }
}
