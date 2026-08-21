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
                SceneIndex = 1
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

            writer.DeleteSaveFile();

            Assert.That(writer.CheckToSeeIfFileExists(), Is.False);
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
