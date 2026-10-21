using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ZZ.Tests
{
    public class SaveReliabilitySystemTests
    {
        private string m_testDirectory;
        private GameObject m_managerObject;
        private Component m_manager;

        [SetUp]
        public void SetUp()
        {
            m_testDirectory = Path.Combine(
                Path.GetTempPath(),
                "EldenSaveReliabilityTests",
                Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (m_managerObject != null)
            {
                UnityEngine.Object.DestroyImmediate(m_managerObject);
            }

            if (Directory.Exists(m_testDirectory))
            {
                Directory.Delete(m_testDirectory, true);
            }
        }

        [Test]
        public void ReplacingSavePublishesNewDataWithoutChangingAnOpenSnapshot()
        {
            SaveFileDataWriter writer = new(m_testDirectory, "CharacterSlot01");
            writer.SaveFile(new CharacterSaveData { CharacterName = "FirstCharacter" });
            string savePath = Path.Combine(m_testDirectory, "CharacterSlot01.json");
            string originalJson = File.ReadAllText(savePath);
            using FileStream snapshotStream = new(
                savePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using StreamReader snapshotReader = new(snapshotStream);

            writer.SaveFile(new CharacterSaveData { CharacterName = "SecondCharacter" });

            Assert.That(snapshotReader.ReadToEnd(), Is.EqualTo(originalJson));
            Assert.That(writer.LoadSaveFile().CharacterName, Is.EqualTo("SecondCharacter"));
            Assert.That(Directory.GetFiles(m_testDirectory), Has.Length.EqualTo(1));
        }

        [Test]
        [Platform("Win")]
        public void FailedReplacementPreservesPreviousSaveAndRemovesStagingFile()
        {
            SaveFileDataWriter writer = new(m_testDirectory, "CharacterSlot01");
            writer.SaveFile(new CharacterSaveData { CharacterName = "FirstCharacter" });
            string savePath = Path.Combine(m_testDirectory, "CharacterSlot01.json");
            string originalJson = File.ReadAllText(savePath);
            using (FileStream lockedSave = new(
                       savePath,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.Read))
            {
                Assert.Throws<IOException>(() => writer.SaveFile(
                    new CharacterSaveData { CharacterName = "SecondCharacter" }));
            }

            Assert.That(File.ReadAllText(savePath), Is.EqualTo(originalJson));
            Assert.That(Directory.GetFiles(m_testDirectory), Has.Length.EqualTo(1));
        }

        [Test]
        public void InvalidSceneDoesNotBeginTransitionOrReplaceCurrentCharacter()
        {
            CreateManager();
            CharacterSaveData originalData = new() { CharacterName = "Original" };
            SetManagerField("m_currentCharacterSlotBeingUsed", CharacterSlot.CharacterSlot01);
            SetManagerField("m_currentCharacterData", originalData);
            LogAssert.Expect(LogType.Error, "Scene build index -1 is not available in Build Settings.");

            bool didStart = (bool)m_manager.GetType().GetMethod(
                "TryBeginSceneLoad", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(m_manager, new object[] { -1 });

            Assert.That(didStart, Is.False);
            Assert.That(GetManagerProperty("CurrentCharacterData"), Is.SameAs(originalData));
            Assert.That(GetManagerProperty("IsSceneLoadInProgress"), Is.False);
        }

        [TestCase(true, false)]
        [TestCase(false, true)]
        public void PendingSceneRestorationKeepsTheSelectedCharacterStable(
            bool isLoadingScene,
            bool isApplyingCharacter)
        {
            CreateManager();
            SetManagerField("m_currentCharacterSlotBeingUsed", CharacterSlot.CharacterSlot01);
            SetManagerField("m_sceneLoadIsInProgress", isLoadingScene);
            SetManagerField("m_shouldApplyLoadedCharacterData", isApplyingCharacter);

            m_manager.GetType().GetMethod("SelectCharacterSlot")
                .Invoke(m_manager, new object[] { CharacterSlot.CharacterSlot02 });

            Assert.That(GetManagerProperty("CurrentCharacterSlot"),
                Is.EqualTo(CharacterSlot.CharacterSlot01));
        }

        private void CreateManager()
        {
            Type managerType = Type.GetType("ZZ.WorldSaveGameManager, Assembly-CSharp");
            Assert.That(managerType, Is.Not.Null);
            m_managerObject = new GameObject("SaveReliabilityTestManager");
            m_managerObject.SetActive(false);
            m_manager = m_managerObject.AddComponent(managerType);
        }

        private void SetManagerField(string name, object value)
        {
            FieldInfo field = m_manager.GetType().GetField(
                name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(m_manager, value);
        }

        private object GetManagerProperty(string name)
        {
            PropertyInfo property = m_manager.GetType().GetProperty(name);
            Assert.That(property, Is.Not.Null, name);
            return property.GetValue(m_manager);
        }
    }
}
