using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZZ
{
    /// <summary>Plays original root tracks and TAE hit volumes through the existing combat resolver.</summary>
    [RequireComponent(typeof(Animator))]
    public sealed class OriginalCreatureCombat : MonoBehaviour
    {
        [SerializeField] private TextAsset m_source;
        [SerializeField] private string m_actor;
        [SerializeField] private Transform m_motionRoot;
        [SerializeField] private LayerMask m_hurtBoxLayers = ~0;

        private static readonly Dictionary<TextAsset, SourceDocument> s_documents = new();
        private readonly Dictionary<int, SourceAction> m_states = new();
        private readonly Dictionary<string, SourceAction> m_clips = new(StringComparer.Ordinal);
        private readonly Dictionary<AnimationClip, SourceAction> m_clipActions = new();
        private readonly Dictionary<string, Transform> m_bones = new(StringComparer.Ordinal);
        private readonly Dictionary<SourceAnchor, Transform> m_anchorTransforms = new();
        private readonly Dictionary<SourceAnchor, Vector3> m_previousPoints = new();
        private readonly List<AnimatorClipInfo> m_clipInfo = new();
        private readonly Collider[] m_contacts = new Collider[64];
        private readonly List<VolumeSlot> m_volumes = new();
        private Animator m_animator;
        private AICharacterManager m_character;
        private AICharacterCombatManager m_combat;
        private SourceAction m_previousAction;
        private float m_previousTime;
        private int m_actionLayer;
        private bool m_reportedOverflow;

        /// <summary>Counts executed source hit-volume queries for runtime verification.</summary>
        public ulong CompletedSweeps { get; private set; }

        private void Awake()
        {
            m_animator = GetComponent<Animator>();
            m_character = GetComponentInParent<AICharacterManager>();
            m_combat = GetComponentInParent<AICharacterCombatManager>();
            if (m_source == null || m_motionRoot == null || m_character == null || m_combat == null)
            {
                throw new InvalidOperationException("Original creature requires its source data, motion root and AI.");
            }
            if (!s_documents.TryGetValue(m_source, out SourceDocument document))
            {
                document = JsonUtility.FromJson<SourceDocument>(m_source.text);
                s_documents.Add(m_source, document);
            }
            foreach (Transform bone in GetComponentsInChildren<Transform>(true))
            {
                m_bones.TryAdd(bone.name, bone);
            }
            foreach (SourceAction action in document.Actions)
            {
                if (action.Actor != m_actor)
                {
                    continue;
                }
                string state = "a" + (action.AnimationID / 1000000).ToString("000") + "_" +
                    (action.AnimationID % 1000000).ToString("000000");
                m_states.Add(Animator.StringToHash("Action Override." + state), action);
                m_clips.TryAdd(m_actor + "_" + action.PoseSource, action);
                int volumeCount = 0;
                foreach (SourceHit hit in action.Hits)
                {
                    foreach (SourceVolume volume in hit.Volumes)
                    {
                        Bind(volume.Start[0]);
                        Bind(volume.End[0]);
                        volumeCount++;
                    }
                }
                while (m_volumes.Count < volumeCount)
                {
                    var root = new GameObject("Original attack volume");
                    root.layer = LayerMask.NameToLayer("Damage Collider");
                    root.transform.SetParent(transform, false);
                    var collider = root.AddComponent<CapsuleCollider>();
                    collider.direction = 2;
                    collider.isTrigger = true;
                    var detector = root.AddComponent<AIDamageCollider>();
                    detector.SetDamageSource(m_character);
                    detector.CloseDamageCollider();
                    m_volumes.Add(new VolumeSlot { Collider = collider, Detector = detector });
                }
            }
            foreach (AnimationClip clip in m_animator.runtimeAnimatorController.animationClips)
            {
                if (m_clips.TryGetValue(clip.name, out SourceAction action))
                {
                    m_clipActions[clip] = action;
                }
            }
            m_actionLayer = m_animator.GetLayerIndex("Action Override");
        }

        private void LateUpdate()
        {
            if (m_character.IsDead && !m_character.IsPerformingAction)
            {
                CloseVolumes();
            }
            AnimatorStateInfo state = m_actionLayer >= 0
                ? m_animator.GetCurrentAnimatorStateInfo(m_actionLayer) : default;
            if (m_actionLayer >= 0 && m_animator.IsInTransition(m_actionLayer))
            {
                state = m_animator.GetNextAnimatorStateInfo(m_actionLayer);
            }
            m_states.TryGetValue(state.fullPathHash, out SourceAction action);
            if (action == null && m_actionLayer >= 0)
            {
                m_animator.GetCurrentAnimatorClipInfo(m_actionLayer, m_clipInfo);
                float weight = 0f;
                foreach (AnimatorClipInfo info in m_clipInfo)
                {
                    if (info.weight > weight && m_clipActions.TryGetValue(info.clip, out SourceAction candidate))
                    {
                        action = candidate;
                        weight = info.weight;
                    }
                }
            }
            if (action == null)
            {
                m_animator.GetCurrentAnimatorClipInfo(0, m_clipInfo);
                float weight = 0f;
                foreach (AnimatorClipInfo info in m_clipInfo)
                {
                    if (info.weight > weight && m_clipActions.TryGetValue(info.clip, out SourceAction candidate))
                    {
                        action = candidate;
                        weight = info.weight;
                    }
                }
                state = m_animator.GetCurrentAnimatorStateInfo(0);
            }
            if (action == null)
            {
                CloseVolumes();
                m_previousAction = null;
                return;
            }
            float progress = state.loop ? Mathf.Repeat(state.normalizedTime, 1f) : Mathf.Clamp01(state.normalizedTime);
            float seconds = progress * action.Duration;
            bool continued = m_previousAction == action && seconds >= m_previousTime;
            if (!continued)
            {
                m_previousPoints.Clear();
            }
            float previous = continued ? m_previousTime : 0f;
            Vector4 motion = SampleRoot(action.RootMotion, progress);
            Quaternion rotation = Quaternion.AngleAxis(-motion.w * Mathf.Rad2Deg, Vector3.up);
            Vector3 translation = new(-motion.x, motion.y, motion.z);
            if (action.RootMotion.Length > 0)
            {
                // Native curves retain the verified source pose; extract the root track at runtime exactly once.
                m_motionRoot.localPosition = Quaternion.Inverse(rotation) * (m_motionRoot.localPosition - translation);
                m_motionRoot.localRotation = Quaternion.Inverse(rotation) * m_motionRoot.localRotation;
                if (m_character.IsServer && m_character.ShouldApplyRootMotion)
                {
                    Vector4 before = SampleRoot(action.RootMotion, previous / action.Duration);
                    Vector3 delta = new(-(motion.x - before.x), 0f, motion.z - before.z);
                    m_character.ApplyRootMotion(transform.TransformVector(delta),
                        Quaternion.AngleAxis(-(motion.w - before.w) * Mathf.Rad2Deg, Vector3.up));
                }
            }
            bool canHit = m_character.IsServer && !m_character.IsDead && m_character.IsPerformingAction;
            int slot = 0;
            bool hasHitWindow = false;
            foreach (SourceHit hit in action.Hits)
            {
                bool active = canHit && seconds >= hit.StartSeconds && previous < hit.EndSeconds;
                foreach (SourceVolume volume in hit.Volumes)
                {
                    VolumeSlot target = m_volumes[slot++];
                    SourceAnchor first = volume.Start[0];
                    SourceAnchor second = volume.End[0];
                    Vector3 start = Point(first);
                    Vector3 end = Point(second);
                    Vector3 lastStart = m_previousPoints.TryGetValue(first, out Vector3 oldStart) ? oldStart : start;
                    Vector3 lastEnd = m_previousPoints.TryGetValue(second, out Vector3 oldEnd) ? oldEnd : end;
                    m_previousPoints[first] = start;
                    m_previousPoints[second] = end;
                    if (!active)
                    {
                        target.Detector.CloseDamageCollider();
                        continue;
                    }
                    hasHitWindow = true;
                    m_combat.ActionRunner.SetWindow(CombatWindowType.Hit, true);
                    m_character.CharacterNetworkManager.SetParryableState(hit.Parryable);
                    target.Collider.transform.SetPositionAndRotation((start + end) * 0.5f,
                        (end - start).sqrMagnitude > 0.000001f ? Quaternion.LookRotation(end - start) : Quaternion.identity);
                    target.Collider.radius = volume.Radius;
                    target.Collider.height = Vector3.Distance(start, end) + volume.Radius * 2f;
                    target.Detector.SetDamageValues(hit.PhysicalDamage, hit.MagicDamage, hit.FireDamage,
                        hit.LightningDamage, hit.HolyDamage, hit.PoiseDamage);
                    target.Detector.OpenDamageCollider(hit.Group);
                    float span = seconds - previous;
                    float begin = span > 0f ? Mathf.Clamp01((hit.StartSeconds - previous) / span) : 0f;
                    float finish = span > 0f ? Mathf.Clamp01((hit.EndSeconds - previous) / span) : 1f;
                    float distance = Mathf.Max(Vector3.Distance(lastStart, start), Vector3.Distance(lastEnd, end));
                    int samples = Mathf.Clamp(Mathf.CeilToInt(distance / Mathf.Max(volume.Radius * 0.5f, 0.01f)), 1, 12);
                    for (int sample = 0; sample <= samples; sample++)
                    {
                        float interpolation = Mathf.Lerp(begin, finish, sample / (float)samples);
                        QueryVolume(target.Detector, Vector3.Lerp(lastStart, start, interpolation),
                            Vector3.Lerp(lastEnd, end, interpolation), volume.Radius);
                        if (!m_character.IsPerformingAction || m_character.IsDead)
                        {
                            break;
                        }
                    }
                    target.Detector.CloseDamageCollider();
                }
            }
            m_combat.ActionRunner.SetWindow(CombatWindowType.Hit,
                hasHitWindow && m_character.IsPerformingAction && !m_character.IsDead);
            m_previousAction = action;
            m_previousTime = seconds;
        }

        private void OnDisable()
        {
            CloseVolumes();
            m_previousAction = null;
        }

        private void QueryVolume(AIDamageCollider detector, Vector3 start, Vector3 end, float radius)
        {
            if (!m_character.IsPerformingAction || m_character.IsDead)
            {
                return;
            }
            int count = Physics.OverlapCapsuleNonAlloc(start, end, radius, m_contacts,
                m_hurtBoxLayers, QueryTriggerInteraction.Collide);
            CompletedSweeps++;
            if (count == m_contacts.Length && !m_reportedOverflow)
            {
                m_reportedOverflow = true;
                Debug.LogWarning("Original creature contact buffer is full; increase its measured bound.", this);
            }
            for (int index = 0; index < count; index++)
            {
                detector.TryProcessHit(m_contacts[index]);
                m_contacts[index] = null;
                if (!m_character.IsPerformingAction || m_character.IsDead)
                {
                    Array.Clear(m_contacts, 0, count);
                    break;
                }
            }
        }

        private void CloseVolumes()
        {
            foreach (VolumeSlot slot in m_volumes)
            {
                slot.Detector.CloseDamageCollider();
            }
            m_combat?.ActionRunner.SetWindow(CombatWindowType.Hit, false);
        }

        private void Bind(SourceAnchor anchor)
        {
            m_anchorTransforms[anchor] = string.IsNullOrEmpty(anchor.Bone) ? transform :
                m_bones.TryGetValue(anchor.Bone, out Transform bone) ? bone :
                throw new InvalidOperationException("Missing original attack bone: " + anchor.Bone);
        }

        private Vector3 Point(SourceAnchor anchor)
        {
            return m_anchorTransforms[anchor].TransformPoint(
                new Vector3(anchor.Position[0], anchor.Position[1], anchor.Position[2]));
        }

        internal static Vector4 SampleRoot(Vector4[] frames, float progress)
        {
            if (frames.Length == 0)
            {
                return Vector4.zero;
            }
            float sample = Mathf.Clamp01(progress) * (frames.Length - 1);
            int index = Mathf.FloorToInt(sample);
            return Vector4.Lerp(frames[index], frames[Mathf.Min(index + 1, frames.Length - 1)], sample - index);
        }

        [Serializable]
        private sealed class SourceDocument
        {
            public SourceAction[] Actions;
        }

        [Serializable]
        private sealed class SourceAction
        {
            public string Actor;
            public int AnimationID;
            public string PoseSource;
            public float Duration;
            public Vector4[] RootMotion;
            public SourceHit[] Hits;
        }

        [Serializable]
        private sealed class SourceHit
        {
            public float StartSeconds;
            public float EndSeconds;
            public int Group;
            public float PhysicalDamage;
            public float MagicDamage;
            public float FireDamage;
            public float LightningDamage;
            public float HolyDamage;
            public float PoiseDamage;
            public bool Parryable;
            public SourceVolume[] Volumes;
        }

        [Serializable]
        private sealed class SourceVolume
        {
            public float Radius;
            public SourceAnchor[] Start;
            public SourceAnchor[] End;
        }

        [Serializable]
        private sealed class SourceAnchor
        {
            public string Bone;
            public float[] Position;
        }

        private sealed class VolumeSlot
        {
            public CapsuleCollider Collider;
            public AIDamageCollider Detector;
        }
    }
}
