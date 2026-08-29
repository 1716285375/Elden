using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ZZ.Tests
{
    public class PlayerBodyManagerTests
    {
        private const string k_PlayerPrefabPath =
            "Assets/Data/Prefabs/Player.prefab";

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(6)]
        public void SetHairstyleRefreshesPresentationWithoutNullReference(int hairstyleID)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(k_PlayerPrefabPath);
            try
            {
                Component bodyManager = root.GetComponents<Component>()
                    .Single(component =>
                        component.GetType().Name == "PlayerBodyManager");
                Type bodyManagerType = bodyManager.GetType();

                bodyManagerType.GetMethod("InitializeBodyModels")
                    ?.Invoke(bodyManager, null);
                MethodInfo setHairstyle = bodyManagerType.GetMethod("SetHairstyle");

                Assert.That(
                    () => setHairstyle.Invoke(
                        bodyManager,
                        new object[] { hairstyleID }),
                    Throws.Nothing);
                Assert.That(
                    bodyManagerType.GetProperty("HairstyleID")
                        ?.GetValue(bodyManager),
                    Is.EqualTo(hairstyleID));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}

