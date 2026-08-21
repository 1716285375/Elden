using System;
using System.IO;
using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Persists character data as JSON without depending on gameplay or UI objects.
    /// </summary>
    public class SaveFileDataWriter
    {
        private const string k_SaveFileExtension = ".json";

        private readonly string m_dataSavePath;
        private readonly string m_dataSaveFileName;

        /// <summary>
        /// Creates a writer for one save filename inside the supplied directory.
        /// </summary>
        public SaveFileDataWriter(string dataSavePath, string dataSaveFileName)
        {
            if (string.IsNullOrWhiteSpace(dataSavePath))
            {
                throw new ArgumentException("A save directory is required.", nameof(dataSavePath));
            }

            if (string.IsNullOrWhiteSpace(dataSaveFileName))
            {
                throw new ArgumentException("A save filename is required.", nameof(dataSaveFileName));
            }

            if (dataSaveFileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                dataSaveFileName != Path.GetFileName(dataSaveFileName))
            {
                throw new ArgumentException(
                    "The save filename cannot contain directory or invalid filename characters.",
                    nameof(dataSaveFileName));
            }

            m_dataSavePath = Path.GetFullPath(dataSavePath);
            m_dataSaveFileName = dataSaveFileName;
        }

        /// <summary>
        /// Returns whether this writer's save file currently exists.
        /// </summary>
        public bool CheckToSeeIfFileExists()
        {
            return File.Exists(GetFullSavePath());
        }

        /// <summary>
        /// Serializes and overwrites this writer's save file.
        /// </summary>
        public void SaveFile(CharacterSaveData characterData)
        {
            if (characterData == null)
            {
                throw new ArgumentNullException(nameof(characterData));
            }

            Directory.CreateDirectory(m_dataSavePath);
            string json = JsonUtility.ToJson(characterData, true);

            using FileStream stream = new FileStream(
                GetFullSavePath(),
                FileMode.Create,
                FileAccess.Write,
                FileShare.None);
            using StreamWriter writer = new StreamWriter(stream);
            writer.Write(json);
        }

        /// <summary>
        /// Loads this writer's save file, or returns null when it does not exist.
        /// </summary>
        public CharacterSaveData LoadSaveFile()
        {
            if (!CheckToSeeIfFileExists())
            {
                return null;
            }

            using FileStream stream = new FileStream(
                GetFullSavePath(),
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);
            using StreamReader reader = new StreamReader(stream);
            string json = reader.ReadToEnd();
            return JsonUtility.FromJson<CharacterSaveData>(json);
        }

        /// <summary>
        /// Deletes this writer's save file when it exists.
        /// </summary>
        public void DeleteSaveFile()
        {
            string fullSavePath = GetFullSavePath();
            if (File.Exists(fullSavePath))
            {
                File.Delete(fullSavePath);
            }
        }

        private string GetFullSavePath()
        {
            return Path.Combine(m_dataSavePath, m_dataSaveFileName + k_SaveFileExtension);
        }
    }
}
