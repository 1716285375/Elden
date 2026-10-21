using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace ZZ.Tests
{
    public class VillageSaveMigrationTests
    {
        [Test]
        public void MonasterySaveMovesToVillageWithoutLosingCharacterOrRecoveryRunes()
        {
            string folder = Path.Combine(Path.GetTempPath(), "VillageSaveTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                var data = new CharacterSaveData
                {
                    CharacterName = "Knight", SceneIndex = 25, XPosition = 140f, YPosition = 2f,
                    ZPosition = 200f, Runes = 300, HasDeadSpot = true, DeadSpotRuneCount = 70,
                    Vitality = 20, LastSiteOfGraceRestedAt = 4
                };
                string json = System.Text.RegularExpressions.Regex.Replace(JsonUtility.ToJson(data), "\"m_dataVersion\":\\d+", "\"m_dataVersion\":16");
                File.WriteAllText(Path.Combine(folder, "CharacterSlot01.json"), json);
                var writer = new SaveFileDataWriter(folder, "CharacterSlot01");
                CharacterSaveData migrated = writer.LoadSaveFile();
                Assert.That(migrated.SceneIndex, Is.EqualTo(1));
                Assert.That(migrated.XPosition, Is.Zero);
                Assert.That(migrated.YPosition, Is.Zero);
                Assert.That(migrated.ZPosition, Is.Zero);
                Assert.That(migrated.Runes, Is.EqualTo(370));
                Assert.That(migrated.HasDeadSpot, Is.False);
                Assert.That(migrated.LastSiteOfGraceRestedAt, Is.Zero);
                Assert.That(migrated.Vitality, Is.EqualTo(20));
                Assert.That(migrated.CharacterName, Is.EqualTo("Knight"));
                writer.SaveFile(migrated);
                CharacterSaveData reloaded = writer.LoadSaveFile();
                Assert.That(reloaded.Runes, Is.EqualTo(370), "Migration must be applied only once.");
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }
    }
}
