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
        /// Publishes a complete JSON snapshot while preserving the previous save if writing fails.
        /// </summary>
        public void SaveFile(CharacterSaveData characterData)
        {
            if (characterData == null)
            {
                throw new ArgumentNullException(nameof(characterData));
            }

            Directory.CreateDirectory(m_dataSavePath);
            string json = JsonUtility.ToJson(characterData, true);

            string fullSavePath = GetFullSavePath();
            string temporaryPath = fullSavePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (FileStream stream = new(
                           temporaryPath,
                           FileMode.CreateNew,
                           FileAccess.Write,
                           FileShare.None))
                using (StreamWriter writer = new(stream))
                {
                    writer.Write(json);
                    writer.Flush();
                    stream.Flush(true);
                }

                // Both files share a directory so replacing the published snapshot stays atomic.
                if (File.Exists(fullSavePath))
                {
                    File.Replace(temporaryPath, fullSavePath, null);
                }
                else
                {
                    File.Move(temporaryPath, fullSavePath);
                }
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
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
            CharacterSaveData characterData = JsonUtility.FromJson<CharacterSaveData>(json);
            characterData?.MigrateToLatestVersion();
            return characterData;
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
