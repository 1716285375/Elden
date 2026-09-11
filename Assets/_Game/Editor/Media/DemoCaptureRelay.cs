using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZZ.Editor
{
    /// <summary>
    /// Runs demo capture commands dropped as a text file under <c>.utmp</c>, so recordings can be
    /// started from outside the Editor. Commands are <c>record:&lt;clip&gt;</c> and <c>stop</c>, and
    /// they are queued so a batch of clips can be recorded in one pass.
    /// </summary>
    [InitializeOnLoad]
    internal static class DemoCaptureRelay
    {
        private const string k_CommandPath = ".utmp/demo-command.txt";
        private const string k_PendingPath = ".utmp/demo-pending.txt";
        private const string k_QueuePath = ".utmp/demo-queue.txt";
        private const string k_RecordPrefix = "record:";
        private const float k_PollInterval = 0.25f;
        private const int k_PlayModeSettleFrames = 5;

        // Entering and leaving Play Mode reloads the script domain, which clears static state, so
        // both the queue and the pending clip are kept on disk.
        private static double s_nextPollTime;

        static DemoCaptureRelay()
        {
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now < s_nextPollTime)
            {
                return;
            }

            s_nextPollTime = now + k_PollInterval;

            if (File.Exists(k_PendingPath))
            {
                StartPendingClip();
                return;
            }

            if (File.Exists(k_CommandPath))
            {
                EnqueueCommands();
                return;
            }

            if (DemoCaptureDirector.IsRunning)
            {
                return;
            }

            string queued = Dequeue();
            if (queued.Length > 0)
            {
                Execute(queued);
            }
        }

        private static void EnqueueCommands()
        {
            string[] commands;
            try
            {
                commands = File.ReadAllLines(k_CommandPath);
                File.Delete(k_CommandPath);
            }
            catch (IOException)
            {
                return;
            }

            var lines = new List<string>();
            foreach (string command in commands)
            {
                string trimmed = command.Trim();
                if (trimmed.Length > 0 && !trimmed.StartsWith("#", StringComparison.Ordinal))
                {
                    lines.Add(trimmed);
                }
            }

            if (lines.Count == 0)
            {
                return;
            }

            WriteQueue(ReadQueue().Concat(lines));
        }

        private static List<string> ReadQueue()
        {
            try
            {
                return File.Exists(k_QueuePath)
                    ? new List<string>(File.ReadAllLines(k_QueuePath))
                    : new List<string>();
            }
            catch (IOException)
            {
                return new List<string>();
            }
        }

        private static void WriteQueue(IEnumerable<string> commands)
        {
            try
            {
                File.WriteAllLines(k_QueuePath, commands);
            }
            catch (IOException exception)
            {
                Debug.LogWarning($"[DemoCaptureRelay] Could not write the queue: {exception.Message}");
            }
        }

        private static string Dequeue()
        {
            List<string> queue = ReadQueue();
            if (queue.Count == 0)
            {
                return string.Empty;
            }

            string next = queue[0].Trim();
            queue.RemoveAt(0);
            WriteQueue(queue);
            return next;
        }

        private static void Execute(string command)
        {
            if (command == "stop")
            {
                EditorApplication.isPlaying = false;
                return;
            }

            if (command.StartsWith(k_RecordPrefix, StringComparison.Ordinal))
            {
                RequestClip(command.Substring(k_RecordPrefix.Length).Trim());
                return;
            }

            Debug.LogWarning($"[DemoCaptureRelay] Unknown command '{command}'.");
        }

        private static void RequestClip(string clipName)
        {
            if (clipName.Length == 0)
            {
                return;
            }

            // A clip always starts from the title screen, so a leftover session is torn down first.
            if (Application.isPlaying)
            {
                WriteQueue(new[] { k_RecordPrefix + clipName }.Concat(ReadQueue()));
                EditorApplication.isPlaying = false;
                return;
            }

            if (SceneManager.GetActiveScene().path != WorldScenePathLayout.MainMenuScenePath)
            {
                Debug.LogError(
                    "[DemoCaptureRelay] Open SCN_MainMenu before recording, then replay the command.");
                return;
            }

            Directory.CreateDirectory(".utmp");
            File.WriteAllText(k_PendingPath, clipName);
            EditorApplication.isPlaying = true;
        }

        private static void StartPendingClip()
        {
            if (!Application.isPlaying || EditorApplication.isCompiling ||
                Time.frameCount < k_PlayModeSettleFrames)
            {
                return;
            }

            string clipName;
            try
            {
                clipName = File.ReadAllText(k_PendingPath).Trim();
                File.Delete(k_PendingPath);
            }
            catch (IOException)
            {
                return;
            }

            try
            {
                DemoCapture.Record(clipName);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}
