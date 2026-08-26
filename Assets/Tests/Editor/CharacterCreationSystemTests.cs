using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace ZZ.Tests
{
    public class CharacterCreationSystemTests
    {
        [TestCase(null, "Unnamed")]
        [TestCase("   ", "Unnamed")]
        [TestCase("  Melina  ", "Melina")]
        [TestCase("12345678901234567890", "123456789012345")]
        public void CharacterNameIsTrimmedAndClamped(string source, string expected)
        {
            Assert.That(
                SanitizeCharacterName(source),
                Is.EqualTo(expected));
        }

        [Test]
        public void AppearanceAndAllSevenAttributesSurviveJsonRoundTrip()
        {
            CharacterSaveData source = new()
            {
                Vitality = 11,
                Endurance = 12,
                Mind = 13,
                Strength = 14,
                Dexterity = 15,
                Intelligence = 16,
                Faith = 17,
                IsMale = false,
                HairstyleID = 6,
                HairColorRed = 241,
                HairColorGreen = 122,
                HairColorBlue = 33
            };

            CharacterSaveData restored = JsonUtility.FromJson<CharacterSaveData>(
                JsonUtility.ToJson(source));

            Assert.That(restored.Vitality, Is.EqualTo(11));
            Assert.That(restored.Endurance, Is.EqualTo(12));
            Assert.That(restored.Mind, Is.EqualTo(13));
            Assert.That(restored.Strength, Is.EqualTo(14));
            Assert.That(restored.Dexterity, Is.EqualTo(15));
            Assert.That(restored.Intelligence, Is.EqualTo(16));
            Assert.That(restored.Faith, Is.EqualTo(17));
            Assert.That(restored.IsMale, Is.False);
            Assert.That(restored.HairstyleID, Is.EqualTo(6));
            Assert.That(restored.HairColorRed, Is.EqualTo(241));
            Assert.That(restored.HairColorGreen, Is.EqualTo(122));
            Assert.That(restored.HairColorBlue, Is.EqualTo(33));
        }

        [Test]
        public void VersionEightSaveMigratesCharacterCreationDefaults()
        {
            string testDirectory = Path.Combine(
                Path.GetTempPath(),
                "EldenCharacterCreationTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testDirectory);
            try
            {
                File.WriteAllText(
                    Path.Combine(testDirectory, "CharacterSlot01.json"),
                    "{\"m_dataVersion\":8,\"m_characterName\":\"Legacy\"}");
                SaveFileDataWriter writer = new(testDirectory, "CharacterSlot01");

                CharacterSaveData restored = writer.LoadSaveFile();

                Assert.That(restored.Strength, Is.EqualTo(10));
                Assert.That(restored.Dexterity, Is.EqualTo(10));
                Assert.That(restored.Intelligence, Is.EqualTo(10));
                Assert.That(restored.Faith, Is.EqualTo(10));
                Assert.That(restored.HairstyleID, Is.Zero);
                Assert.That(restored.HairColorRed, Is.EqualTo(79));
                Assert.That(restored.HairColorGreen, Is.EqualTo(53));
                Assert.That(restored.HairColorBlue, Is.EqualTo(35));
            }
            finally
            {
                Directory.Delete(testDirectory, true);
            }
        }

        [Test]
        public void CharacterCreationValuesClampToPersistentRanges()
        {
            CharacterSaveData data = new()
            {
                Strength = -1,
                Dexterity = -2,
                Intelligence = -3,
                Faith = -4,
                HairstyleID = -5,
                HairColorRed = -1,
                HairColorGreen = 300,
                HairColorBlue = 256
            };

            Assert.That(data.Strength, Is.Zero);
            Assert.That(data.Dexterity, Is.Zero);
            Assert.That(data.Intelligence, Is.Zero);
            Assert.That(data.Faith, Is.Zero);
            Assert.That(data.HairstyleID, Is.Zero);
            Assert.That(data.HairColorRed, Is.Zero);
            Assert.That(data.HairColorGreen, Is.EqualTo(255));
            Assert.That(data.HairColorBlue, Is.EqualTo(255));
        }

        private static string SanitizeCharacterName(string source)
        {
            Type creationManagerType = Type.GetType(
                "ZZ.TitleScreenCharacterCreationManager, Assembly-CSharp");
            Assert.That(creationManagerType, Is.Not.Null);
            MethodInfo sanitizeMethod = creationManagerType.GetMethod(
                "SanitizeCharacterName",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(sanitizeMethod, Is.Not.Null);
            return (string)sanitizeMethod.Invoke(null, new object[] { source });
        }
    }
}
