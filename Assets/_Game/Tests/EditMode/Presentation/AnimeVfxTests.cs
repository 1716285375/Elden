using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ZZ.Tests
{
    public class AnimeVfxTests
    {
        [TestCase("Assets/_Game/Prefabs/Effects/BloodSplatterVFX.prefab", "cd8ae04f0906320429f9c16f38f0b5eb", 1749097799505083475L)]
        [TestCase("Assets/_Game/Prefabs/Effects/Health Flask VFX.prefab", "1058ca6f234a7a142950bc60bb1d9970", 7046630527272132478L)]
        [TestCase("Assets/_Game/Prefabs/Effects/Focus Flask VFX.prefab", "2515d77588552d040bf5354f9af240cd", 7501971539574221985L)]
        public void ReplacementPreservesExistingSceneAndPrefabReferences(string path, string expectedGuid, long expectedId)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(prefab, out string guid, out long id), Is.True);
            Assert.That(guid, Is.EqualTo(expectedGuid));
            Assert.That(id, Is.EqualTo(expectedId));
        }

        [TestCase("Assets/_Game/Prefabs/Effects/BloodSplatterVFX.prefab", false)]
        [TestCase("Assets/_Game/Prefabs/Effects/Health Flask VFX.prefab", false)]
        [TestCase("Assets/_Game/Prefabs/Effects/Focus Flask VFX.prefab", false)]
        [TestCase("Assets/_Game/Resources/Effects/Poisoned VFX.prefab", true)]
        [TestCase("Assets/_Game/Resources/Effects/Frostbite VFX.prefab", true)]
        [TestCase("Assets/_Game/Resources/Effects/Anime/Guard Spark.prefab", false)]
        [TestCase("Assets/_Game/Resources/Effects/Anime/Parry Flash.prefab", false)]
        [TestCase("Assets/_Game/Resources/Effects/Anime/Critical Impact.prefab", false)]
        public void GameplayVfxHasBoundedParticlesAndOnlyApprovedVisualDependencies(string path, bool loops)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null);
            ParticleSystem[] systems = prefab.GetComponentsInChildren<ParticleSystem>(true);
            Assert.That(systems.Length, Is.InRange(1, 3));
            Type cleanup = Type.GetType("ZZ.Utility_DestroyAfterTime, Assembly-CSharp");
            Assert.That(prefab.GetComponent(cleanup) != null, Is.EqualTo(!loops));
            foreach (ParticleSystem particles in systems)
            {
                Assert.That(particles.main.loop, Is.EqualTo(loops));
                Assert.That(particles.main.maxParticles, Is.LessThanOrEqualTo(64));
                Assert.That(particles.main.startLifetime.constantMax, Is.LessThan(3f));
                Material material = particles.GetComponent<ParticleSystemRenderer>().sharedMaterial;
                Assert.That(material, Is.Not.Null);
                Assert.That(ShaderUtil.ShaderHasError(material.shader), Is.False);
            }
            foreach (string dependency in AssetDatabase.GetDependencies(path, true))
            {
                string extension = Path.GetExtension(dependency);
                if (new[] { ".png", ".jpg", ".tga" }.Contains(extension))
                {
                    Assert.That(dependency, Does.StartWith("Assets/_Game/Art/VFX/Licensed/KenneyParticles/"));
                }
                if (new[] { ".shader", ".shadergraph", ".shadersubgraph" }.Contains(extension))
                {
                    Assert.That(dependency.StartsWith("Packages/", StringComparison.Ordinal) ||
                        dependency.StartsWith("Assets/_Game/Shaders/Anime/", StringComparison.Ordinal), Is.True,
                        "Unexpected visual source: " + dependency);
                }
            }
        }

        [TestCase("Assets/_Game/Shaders/Anime/AnimeVfx.shader")]
        [TestCase("Assets/_Game/Shaders/Anime/AnimeTelegraph.shader")]
        [TestCase("Assets/_Game/Shaders/Anime/AnimeParticleGraph.shadergraph")]
        public void AnimeShadersImportWithoutErrors(string path)
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            Assert.That(shader, Is.Not.Null);
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
        }
    }
}
