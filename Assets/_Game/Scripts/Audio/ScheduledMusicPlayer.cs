using UnityEngine;

namespace ZZ
{
    /// <summary>Plays the authored introduction once and schedules a seamless repeating segment.</summary>
    public sealed class ScheduledMusicPlayer : MonoBehaviour
    {
        [SerializeField] private AudioSource m_intro;
        [SerializeField] private AudioSource m_loop;

        public bool IsScheduled { get; private set; }

        private void OnEnable()
        {
            if (!Application.isPlaying || m_intro?.clip == null || m_loop?.clip == null) { return; }
            double start = AudioSettings.dspTime + 0.25d;
            double introDuration = (double)m_intro.clip.samples / m_intro.clip.frequency;
            m_intro.loop = false;
            m_loop.loop = true;
            m_intro.PlayScheduled(start);
            m_intro.SetScheduledEndTime(start + introDuration);
            m_loop.PlayScheduled(start + introDuration);
            IsScheduled = true;
        }

        private void OnDisable()
        {
            m_intro?.Stop();
            m_loop?.Stop();
            IsScheduled = false;
        }
    }
}
