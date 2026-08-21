using System;
using System.IO;
using NUnit.Framework;

namespace ZZ.Tests
{
    public class SaveFileDataWriterTests
    {
        private string m_testDirectory;

        [SetUp]
        public void SetUp()
        {
            m_testDirectory = Path.Combine(
                Path.GetTempPath(),
                "EldenSaveGameTests",
                Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(m_testDirectory))
            {
                Directory.Delete(m_testDirectory, true);
            }
        }

        [Test]
        public void SavedCharacterCanBeLoadedAndDeleted()
        {
            SaveFileDataWriter writer = new SaveFileDataWriter(
                m_testDirectory,
                "CharacterSlot01");
            CharacterSaveData expectedData = new CharacterSaveData
            {
                CharacterName = "TestKnight",
                SecondsPlayed = 123.5f,
                XPosition = 10f,
                YPosition = 2f,
                ZPosition = 30f,
                SceneIndex = 1,
                Vitality = 14,
                Endurance = 12,
                CurrentHealth = 142f,
                CurrentStamina = 67f
            };

            Assert.That(writer.CheckToSeeIfFileExists(), Is.False);

            writer.SaveFile(expectedData);

            Assert.That(writer.CheckToSeeIfFileExists(), Is.True);
            CharacterSaveData loadedData = writer.LoadSaveFile();
            Assert.That(loadedData, Is.Not.Null);
            Assert.That(loadedData.CharacterName, Is.EqualTo("TestKnight"));
            Assert.That(loadedData.SecondsPlayed, Is.EqualTo(123.5f));
            Assert.That(loadedData.XPosition, Is.EqualTo(10f));
            Assert.That(loadedData.YPosition, Is.EqualTo(2f));
            Assert.That(loadedData.ZPosition, Is.EqualTo(30f));
            Assert.That(loadedData.SceneIndex, Is.EqualTo(1));
            Assert.That(loadedData.Vitality, Is.EqualTo(14));
            Assert.That(loadedData.Endurance, Is.EqualTo(12));
            Assert.That(loadedData.CurrentHealth, Is.EqualTo(142f));
            Assert.That(loadedData.CurrentStamina, Is.EqualTo(67f));

            writer.DeleteSaveFile();

            Assert.That(writer.CheckToSeeIfFileExists(), Is.False);
        }

        [Test]
        public void NewCharacterUsesStartingAttributeAndResourceDefaults()
        {
            CharacterSaveData characterData = new CharacterSaveData();

            Assert.That(characterData.Vitality, Is.EqualTo(10));
            Assert.That(characterData.Endurance, Is.EqualTo(10));
            Assert.That(characterData.CurrentHealth, Is.EqualTo(150f));
            Assert.That(characterData.CurrentStamina, Is.EqualTo(100f));
        }

        [Test]
        public void LegacyCharacterDataUsesStartingAttributeAndResourceDefaults()
        {
            Directory.CreateDirectory(m_testDirectory);
            File.WriteAllText(
                Path.Combine(m_testDirectory, "CharacterSlot01.json"),
                "{\"m_characterName\":\"LegacyKnight\",\"m_sceneIndex\":1}");
            SaveFileDataWriter writer = new SaveFileDataWriter(
                m_testDirectory,
                "CharacterSlot01");

            CharacterSaveData loadedData = writer.LoadSaveFile();

            Assert.That(loadedData, Is.Not.Null);
            Assert.That(loadedData.Vitality, Is.EqualTo(10));
            Assert.That(loadedData.Endurance, Is.EqualTo(10));
            Assert.That(loadedData.CurrentHealth, Is.EqualTo(150f));
            Assert.That(loadedData.CurrentStamina, Is.EqualTo(100f));
        }

        [Test]
        public void SavingAnExistingSlotOverwritesItsPreviousData()
        {
            SaveFileDataWriter writer = new SaveFileDataWriter(
                m_testDirectory,
                "CharacterSlot01");
            writer.SaveFile(new CharacterSaveData
            {
                CharacterName = "FirstCharacter",
                SceneIndex = 1
            });

            writer.SaveFile(new CharacterSaveData
            {
                CharacterName = "SecondCharacter",
                XPosition = 42f,
                SceneIndex = 2
            });

            CharacterSaveData loadedData = writer.LoadSaveFile();
            Assert.That(loadedData, Is.Not.Null);
            Assert.That(loadedData.CharacterName, Is.EqualTo("SecondCharacter"));
            Assert.That(loadedData.XPosition, Is.EqualTo(42f));
            Assert.That(loadedData.SceneIndex, Is.EqualTo(2));
        }

        [Test]
        public void MissingSaveFileReturnsNullAndCanBeDeletedSafely()
        {
            SaveFileDataWriter writer = new SaveFileDataWriter(
                m_testDirectory,
                "CharacterSlot01");

            Assert.That(writer.LoadSaveFile(), Is.Null);
            Assert.DoesNotThrow(writer.DeleteSaveFile);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void ConstructorRejectsMissingSaveDirectory(string saveDirectory)
        {
            Assert.Throws<ArgumentException>(
                () => new SaveFileDataWriter(saveDirectory, "CharacterSlot01"));
        }

        [Test]
        public void ConstructorRejectsInvalidSaveDirectory()
        {
            Assert.Throws<ArgumentException>(
                () => new SaveFileDataWriter("\0", "CharacterSlot01"));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("Nested/CharacterSlot01")]
        [TestCase("../CharacterSlot01")]
        public void ConstructorRejectsInvalidSaveFileName(string saveFileName)
        {
            Assert.Throws<ArgumentException>(
                () => new SaveFileDataWriter(m_testDirectory, saveFileName));
        }

        [Test]
        public void SavingNullCharacterDataThrows()
        {
            SaveFileDataWriter writer = new SaveFileDataWriter(
                m_testDirectory,
                "CharacterSlot01");

            Assert.Throws<ArgumentNullException>(() => writer.SaveFile(null));
        }

        [Test]
        public void LoadingCorruptedJsonThrows()
        {
            Directory.CreateDirectory(m_testDirectory);
            File.WriteAllText(
                Path.Combine(m_testDirectory, "CharacterSlot01.json"),
                "{ this is not valid JSON");
            SaveFileDataWriter writer = new SaveFileDataWriter(
                m_testDirectory,
                "CharacterSlot01");

            Assert.Throws<ArgumentException>(() => writer.LoadSaveFile());
        }
    }
}
