using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using ZZ.Demo;

namespace ZZ.Editor
{
    /// <summary>
    /// Editor-only capture session: plays a clip with <see cref="DemoPlayback"/>, injects its input
    /// with <see cref="DemoInputInjector"/>, and writes the Game View as a PNG sequence.
    /// </summary>
    /// <remarks>
    /// Runs from <see cref="EditorApplication.update"/> rather than a component, because Unity
    /// refuses to attach a MonoBehaviour that lives under an <c>Editor</c> folder. Everything here is
    /// Editor-only on purpose: the reusable pieces live in <c>Media/Core</c> and carry no
    /// <c>UnityEditor</c> dependency, so embedding them in the game later is a file move.
    /// </remarks>
    public sealed class DemoCaptureDirector
    {
        private const string k_OutputRoot = ".utmp/demo-capture";
        private const int k_BootFrameLimit = 2400;

        private static DemoCaptureDirector s_instance;

        private readonly string m_outputDirectory;
        private readonly DemoPlayback m_playback;
        private DemoInputInjector m_injector;
        private int m_bootFrames;

        private DemoCaptureDirector(DemoClip clip, string outputDirectory)
        {
            m_outputDirectory = outputDirectory;
            m_playback = new DemoPlayback(clip);
        }

        /// <summary>Gets whether a capture is already in flight.</summary>
        public static bool IsRunning => s_instance != null;

        /// <summary>Starts a capture. Requires Play Mode.</summary>
        public static void Play(DemoClip clip)
        {
            if (!Application.isPlaying)
            {
                throw new InvalidOperationException("Demo capture requires Play Mode.");
            }

            if (s_instance != null)
            {
                throw new InvalidOperationException("A demo capture is already running.");
            }

            string outputDirectory = Path.GetFullPath(Path.Combine(k_OutputRoot, clip.Name));
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, true);
            }

            Directory.CreateDirectory(outputDirectory);

            s_instance = new DemoCaptureDirector(clip, outputDirectory);
            s_instance.Begin();
        }

        private void Begin()
        {
            if (m_playback.Clip.BootIntoGameplay)
            {
                DemoStage.BootIntoGameplay();
            }
            else
            {
                BeginCapture();
            }

            EditorApplication.update += Tick;
        }

        private void Tick()
        {
            if (s_instance != this)
            {
                return;
            }

            if (m_injector == null)
            {
                WaitForBoot();
                return;
            }

            CaptureFrame();
        }

        private void WaitForBoot()
        {
            if (DemoStage.IsGameplayReady())
            {
                BeginCapture();
                return;
            }

            if (++m_bootFrames > k_BootFrameLimit)
            {
                Abort(new TimeoutException("Demo capture timed out waiting for the gameplay scene."));
            }
        }

        private void BeginCapture()
        {
            // Unity drops queued screenshots when the Game View is not the visible window.
            EditorApplication.ExecuteMenuItem("Window/General/Game");
            Time.captureFramerate = m_playback.FrameRate;
            m_injector = new DemoInputInjector();
        }

        private void CaptureFrame()
        {
            // PlayerInputManager disables itself whenever the Editor loses focus, which would
            // silently drop every scripted input, so gameplay clips re-arm it each frame.
            if (m_playback.Clip.ForceEnableInput)
            {
                PlayerInputManager.Instance?.EnablePlayerControls();
            }

            m_playback.AdvanceFrame();
            if (m_playback.IsFinished)
            {
                Finish();
                return;
            }

            m_injector.Push(m_playback.CurrentInput);
            ScreenCapture.CaptureScreenshot(FramePath(m_playback.FrameIndex - 1));
        }

        private void Finish()
        {
            int written = Directory.GetFiles(m_outputDirectory, "frame_*.png").Length;
            File.WriteAllText(Path.Combine(m_outputDirectory, "DONE.txt"),
                $"clip={m_playback.Clip.Name}{Environment.NewLine}" +
                $"frameRate={m_playback.FrameRate}{Environment.NewLine}" +
                $"frames={m_playback.TotalFrames}{Environment.NewLine}" +
                $"written={written}{Environment.NewLine}");
            Debug.Log($"[DemoCapture] '{m_playback.Clip.Name}': {m_playback.TotalFrames} frames at " +
                $"{m_playback.FrameRate} fps -> {m_outputDirectory}");
            if (written == 0)
            {
                Debug.LogError(
                    "[DemoCapture] No screenshots were written. Make sure the Game View is open " +
                    "and the Editor window is visible, then record again.");
            }

            Stop();
        }

        private void Abort(Exception exception)
        {
            Debug.LogException(exception);
            Stop();
        }

        private void Stop()
        {
            EditorApplication.update -= Tick;
            m_injector?.Dispose();
            m_injector = null;
            Time.captureFramerate = 0;
            if (s_instance == this)
            {
                s_instance = null;
            }
        }

        private string FramePath(int index)
        {
            return Path.Combine(m_outputDirectory, $"frame_{index:D5}.png");
        }
    }
}
