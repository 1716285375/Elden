using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ZZ.Tests
{
    public class CharacterProfileIconSystemTests
    {
        private const string k_ProfileIconPrefabPath =
            "Assets/_Game/Resources/UI/Profile Icon Maker.prefab";
        private const string k_RenderTexturePath =
            "Assets/_Game/Resources/UI/Icon Render Texture.renderTexture";
        private const string k_MakerSourcePath =
            "Assets/_Game/Scripts/UI/Frontend/ProfileIcons/" +
            "CharacterProfileIconMaker.cs";
        private const string k_DummySourcePath =
            "Assets/_Game/Scripts/UI/Frontend/ProfileIcons/" +
            "ProfileIconMakerManager.cs";

        [Test]
        public void PortraitPrefabContainsOnlyVisualAndProfileComponents()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                k_ProfileIconPrefabPath);
            Assert.That(prefab, Is.Not.Null);

            Component dummyManager = prefab.GetComponentInChildren(
                GetRuntimeType("ZZ.ProfileIconMakerManager"),
                true);
            Assert.That(dummyManager, Is.Not.Null);
            GameObject dummy = dummyManager.gameObject;
            string[] forbiddenTypes =
            {
                "ZZ.CharacterManager",
                "ZZ.PlayerManager",
                "ZZ.PlayerNetworkManager"
            };
            foreach (string typeName in forbiddenTypes)
            {
                Assert.That(dummy.GetComponent(GetRuntimeType(typeName)),
                    Is.Null,
                    $"Portrait dummy must not contain {typeName}.");
            }

            Type networkObjectType = Type.GetType(
                "Unity.Netcode.NetworkObject, Unity.Netcode.Runtime");
            Assert.That(networkObjectType, Is.Not.Null);
            Assert.That(dummy.GetComponent(networkObjectType), Is.Null);
            Assert.That(dummy.GetComponent<CharacterController>(), Is.Null);
            Assert.That(dummy.GetComponent<Rigidbody>(), Is.Null);
            Assert.That(
                dummy.GetComponentsInChildren<MonoBehaviour>(true)
                    .Select(component => component.GetType().Name),
                Is.EquivalentTo(new[]
                {
                    "ProfileIconMakerBodyManager",
                    "ProfileIconMakerEquipmentManager",
                    "ProfileIconMakerManager"
                }));
        }

        [Test]
        public void PortraitCameraUsesIsolatedSquareRenderTargetAndLight()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                k_ProfileIconPrefabPath);
            RenderTexture template =
                AssetDatabase.LoadAssetAtPath<RenderTexture>(
                    k_RenderTexturePath);
            Camera portraitCamera = prefab.GetComponentInChildren<Camera>(true);
            Light portraitLight = prefab.GetComponentInChildren<Light>(true);

            Assert.That(template, Is.Not.Null);
            Assert.That(template.width, Is.EqualTo(600));
            Assert.That(template.height, Is.EqualTo(600));
            Assert.That(template.antiAliasing, Is.EqualTo(4));
            Assert.That(template.useDynamicScale, Is.True);
            Assert.That(portraitCamera, Is.Not.Null);
            Assert.That(portraitCamera.targetTexture, Is.EqualTo(template));
            Assert.That(portraitCamera.enabled, Is.False);
            Assert.That(portraitCamera.cullingMask, Is.EqualTo(1 << 31));
            Assert.That(portraitLight, Is.Not.Null);
            Assert.That(portraitLight.type, Is.EqualTo(LightType.Point));
            Assert.That(portraitLight.cullingMask, Is.EqualTo(1 << 31));
        }

        [Test]
        public void EquipmentModelDependsOnPresentationManagerNotPlayer()
        {
            Type equipmentModelType = GetRuntimeType("ZZ.EquipmentModel");
            MethodInfo loadModel = equipmentModelType.GetMethod("LoadModel");

            Assert.That(loadModel, Is.Not.Null);
            Assert.That(
                loadModel.GetParameters().Select(parameter => parameter.ParameterType.Name),
                Is.EqualTo(new[] { "PlayerEquipmentManager", "Boolean" }));
            Assert.That(
                GetRuntimeType("ZZ.ProfileIconMakerEquipmentManager")
                    .IsSubclassOf(GetRuntimeType("ZZ.PlayerEquipmentManager")),
                Is.True);
        }

        [Test]
        public void EverySaveFullyOverwritesDummyAndNullEquipment()
        {
            string source = File.ReadAllText(k_DummySourcePath);

            Assert.That(source, Does.Contain("ChangeSex(characterData.IsMale)"));
            Assert.That(source,
                Does.Contain("SetHairstyle(characterData.HairstyleID)"));
            Assert.That(source,
                Does.Contain("characterData.HairColorRed"));
            Assert.That(source,
                Does.Contain("LoadHeadEquipment(headEquipment)"));
            Assert.That(source,
                Does.Contain("LoadBodyEquipment(bodyEquipment)"));
        }

        [Test]
        public void MakerPersistsPngAndBuildsRuntimeSpritePerSlot()
        {
            string source = File.ReadAllText(k_MakerSourcePath);
            Type makerType = GetRuntimeType("ZZ.CharacterProfileIconMaker");

            Assert.That(source, Does.Contain("m_profileIconCamera.Render()"));
            Assert.That(source, Does.Contain("ReadPixels("));
            Assert.That(source, Does.Contain("EncodeToPNG()"));
            Assert.That(source, Does.Contain("Directory.CreateDirectory("));
            Assert.That(source, Does.Contain("File.WriteAllBytes("));
            Assert.That(source, Does.Contain("Sprite.Create("));
            Assert.That(makerType.GetMethod("CreateAllProfileIcons"), Is.Not.Null);
            Assert.That(makerType.GetMethod("CreateCharacterProfileIcon"),
                Is.Not.Null);
            Assert.That(makerType.GetMethod("GetProfileIconPath"), Is.Not.Null);
        }

        private static Type GetRuntimeType(string fullName)
        {
            Type type = Type.GetType($"{fullName}, Assembly-CSharp");
            Assert.That(type, Is.Not.Null, $"Could not resolve {fullName}.");
            return type;
        }
    }
}
