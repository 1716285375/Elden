using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Provides dictionary semantics through parallel lists supported by Unity serialization.
    /// </summary>
    [Serializable]
    public class SerializableDictionary<TKey, TValue>
    {
        [SerializeField] private List<TKey> m_keys = new();
        [SerializeField] private List<TValue> m_values = new();

        public int Count
        {
            get
            {
                EnsureLists();
                return Mathf.Min(m_keys.Count, m_values.Count);
            }
        }

        public TValue this[TKey key]
        {
            get
            {
                if (TryGetValue(key, out TValue value))
                {
                    return value;
                }

                throw new KeyNotFoundException($"The key '{key}' was not found.");
            }
            set
            {
                EnsureLists();
                int keyIndex = FindKeyIndex(key);
                if (keyIndex >= 0)
                {
                    m_values[keyIndex] = value;
                    return;
                }

                m_keys.Add(key);
                m_values.Add(value);
            }
        }

        public void Add(TKey key, TValue value)
        {
            EnsureLists();
            if (ContainsKey(key))
            {
                throw new ArgumentException(
                    $"An element with the key '{key}' already exists.",
                    nameof(key));
            }

            m_keys.Add(key);
            m_values.Add(value);
        }

        public bool ContainsKey(TKey key)
        {
            EnsureLists();
            return FindKeyIndex(key) >= 0;
        }

        public bool Remove(TKey key)
        {
            EnsureLists();
            int keyIndex = FindKeyIndex(key);
            if (keyIndex < 0)
            {
                return false;
            }

            m_keys.RemoveAt(keyIndex);
            m_values.RemoveAt(keyIndex);
            return true;
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            EnsureLists();
            int keyIndex = FindKeyIndex(key);
            if (keyIndex >= 0 && keyIndex < m_values.Count)
            {
                value = m_values[keyIndex];
                return true;
            }

            value = default;
            return false;
        }

        public void Clear()
        {
            EnsureLists();
            m_keys.Clear();
            m_values.Clear();
        }

        private int FindKeyIndex(TKey key)
        {
            EqualityComparer<TKey> comparer = EqualityComparer<TKey>.Default;
            int validEntryCount = Mathf.Min(m_keys.Count, m_values.Count);
            for (int keyIndex = 0; keyIndex < validEntryCount; keyIndex++)
            {
                if (comparer.Equals(m_keys[keyIndex], key))
                {
                    return keyIndex;
                }
            }

            return -1;
        }

        private void EnsureLists()
        {
            m_keys ??= new List<TKey>();
            m_values ??= new List<TValue>();
            while (m_keys.Count > m_values.Count)
            {
                m_keys.RemoveAt(m_keys.Count - 1);
            }

            while (m_values.Count > m_keys.Count)
            {
                m_values.RemoveAt(m_values.Count - 1);
            }
        }
    }
}
