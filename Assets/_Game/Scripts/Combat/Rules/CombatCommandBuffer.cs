using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZZ
{
    /// <summary>One pressed command and the time it was pressed.</summary>
    public readonly struct BufferedCommand
    {
        public BufferedCommand(CombatCommand command, float timestamp)
        {
            Command = command;
            Timestamp = timestamp;
        }

        public CombatCommand Command { get; }
        public float Timestamp { get; }
    }

    /// <summary>
    /// A real input buffer: every command is recorded on the frame it is pressed and survives until
    /// it expires or an action becomes able to consume it. This is what stops early presses from
    /// being swallowed outside the combo window.
    /// </summary>
    public sealed class CombatCommandBuffer
    {
        public const float k_DefaultLifetime = 0.3f;

        /// <summary>When several commands are buffered, the earlier entry in this array wins.</summary>
        private static readonly CombatCommand[] s_priority =
        {
            CombatCommand.Dodge,
            CombatCommand.Parry,
            CombatCommand.Skill,
            CombatCommand.Jump,
            CombatCommand.UseItem,
            CombatCommand.Guard,
            CombatCommand.HeavyAttack,
            CombatCommand.LightAttack
        };

        private readonly List<BufferedCommand> m_commands = new();
        private readonly List<BufferedCommand> m_ordering = new();
        private bool m_isExecuting;
        private uint m_clearVersion;

        public float Lifetime { get; set; } = k_DefaultLifetime;
        public IReadOnlyList<BufferedCommand> Pending => m_commands;
        public int Count => m_commands.Count;

        public void Push(CombatCommand command)
        {
            if (!IsEdgeCommand(command))
            {
                return;
            }

            m_commands.Add(new BufferedCommand(command, Time.time));
        }

        public void Clear()
        {
            m_commands.Clear();
            m_clearVersion++;
        }

        public bool HasPending(CombatCommand command)
        {
            RemoveExpired(Time.time);
            foreach (BufferedCommand buffered in m_commands)
            {
                if (buffered.Command == command)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Removes and returns the oldest unexpired instance of one command.</summary>
        public bool TryConsume(CombatCommand command)
        {
            RemoveExpired(Time.time);
            for (int index = 0; index < m_commands.Count; index++)
            {
                if (m_commands[index].Command != command)
                {
                    continue;
                }

                m_commands.RemoveAt(index);
                return true;
            }

            return false;
        }

        /// <summary>Returns the highest priority unexpired command without removing it.</summary>
        public bool TryPeek(out BufferedCommand command)
        {
            RemoveExpired(Time.time);
            command = default;
            bool found = false;
            foreach (BufferedCommand candidate in m_commands)
            {
                if (found && PriorityOf(candidate.Command) >= PriorityOf(command.Command))
                {
                    continue;
                }

                command = candidate;
                found = true;
            }

            return found;
        }

        /// <summary>
        /// Whether a command is edge-triggered and therefore belongs in the buffer. Held intents
        /// such as Guard, Aim or Sprint are state, not commands: buffering them would raise a shield
        /// the player already released.
        /// </summary>
        public static bool IsEdgeCommand(CombatCommand command)
        {
            return command != CombatCommand.None && command != CombatCommand.Guard;
        }

        /// <summary>Removes and returns the highest priority unexpired command.</summary>
        public bool TryConsumeHighest(out CombatCommand command)
        {
            if (!TryPeek(out BufferedCommand buffered))
            {
                command = CombatCommand.None;
                return false;
            }

            command = buffered.Command;
            m_commands.Remove(buffered);
            return true;
        }

        /// <summary>
        /// Attempts buffered commands in priority order until one actually runs, then consumes it.
        /// This is "highest priority executable", not "highest priority": an illegal Dodge must
        /// never starve a legal LightAttack.
        /// </summary>
        public bool TryConsumeFirstExecutable(Func<CombatCommand, bool> execute,
            out CombatCommand consumed)
        {
            consumed = CombatCommand.None;
            if (execute == null || m_isExecuting)
            {
                return false;
            }

            RemoveExpired(Time.time);
            m_ordering.Clear();
            m_ordering.AddRange(m_commands);
            m_ordering.Sort(CompareByPriority);
            uint clearVersion = m_clearVersion;
            m_isExecuting = true;
            try
            {
                foreach (BufferedCommand candidate in m_ordering)
                {
                    if (clearVersion != m_clearVersion)
                    {
                        break;
                    }

                    if (!m_commands.Contains(candidate) || !execute(candidate.Command))
                    {
                        continue;
                    }

                    if (clearVersion == m_clearVersion)
                    {
                        m_commands.Remove(candidate);
                    }
                    consumed = candidate.Command;
                    return true;
                }
            }
            finally
            {
                m_isExecuting = false;
                m_ordering.Clear();
            }

            return false;
        }

        public void RemoveExpired(float now)
        {
            for (int index = m_commands.Count - 1; index >= 0; index--)
            {
                if (now - m_commands[index].Timestamp > Lifetime)
                {
                    m_commands.RemoveAt(index);
                }
            }
        }

        /// <summary>Lower values win when two commands compete for the same frame.</summary>
        public static int PriorityOf(CombatCommand command)
        {
            for (int index = 0; index < s_priority.Length; index++)
            {
                if (s_priority[index] == command)
                {
                    return index;
                }
            }

            return s_priority.Length;
        }

        /// <summary>Orders by priority, then by arrival so equal priorities stay first-in-first-out.</summary>
        private static int CompareByPriority(BufferedCommand left, BufferedCommand right)
        {
            int byPriority = PriorityOf(left.Command).CompareTo(PriorityOf(right.Command));
            return byPriority != 0 ? byPriority : left.Timestamp.CompareTo(right.Timestamp);
        }
    }
}
