using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace ZZ.Tests
{
    public class PlayerLockOnTargetSelectorTests
    {
        private readonly List<GameObject> m_objects = new();
        private Type m_characterType;
        private Type m_selectorType;
        private IList m_targets;

        [SetUp]
        public void SetUp()
        {
            m_characterType = Type.GetType("ZZ.CharacterManager, Assembly-CSharp", true);
            m_selectorType = Type.GetType("ZZ.PlayerLockOnTargetSelector, Assembly-CSharp", true);
            m_targets = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(m_characterType));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject instance in m_objects)
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
            m_objects.Clear();
        }

        [Test]
        public void AcquisitionPrefersCameraCenterAndRejectsBehindCamera()
        {
            AddTarget(new Vector3(0f, 0f, -1f));
            AddTarget(new Vector3(3f, 0f, 2f));
            Component center = AddTarget(new Vector3(0f, 0f, 10f));
            Transform reference = CreateObject().transform;
            object selected = m_selectorType.GetMethod("SelectClosestTarget").Invoke(null,
                new object[] { m_targets, Vector3.zero, reference });
            Assert.That(selected, Is.SameAs(center));
        }

        [Test]
        public void SwitchingChoosesAdjacentAngleInsteadOfNearestWorldDistance()
        {
            Component current = AddTarget(new Vector3(0f, 0f, 10f));
            Component adjacent = AddTarget(new Vector3(2f, 0f, 10f));
            AddTarget(new Vector3(3f, 0f, 1f));
            AddTarget(new Vector3(-1f, 0f, 10f));
            object selected = m_selectorType.GetMethod("SelectDirectionalTarget").Invoke(null,
                new object[] { m_targets, current, CreateObject().transform, Vector3.zero, 1f });
            Assert.That(selected, Is.SameAs(adjacent));
        }

        [Test]
        public void SwitchingWithoutCandidateKeepsCurrentTarget()
        {
            Component current = AddTarget(new Vector3(0f, 0f, 10f));
            AddTarget(new Vector3(-1f, 0f, 10f));
            object selected = m_selectorType.GetMethod("SelectDirectionalTarget").Invoke(null,
                new object[] { m_targets, current, CreateObject().transform, Vector3.zero, 1f });
            Assert.That(selected, Is.Null);
        }

        private Component AddTarget(Vector3 position)
        {
            GameObject instance = CreateObject();
            instance.transform.position = position;
            Component target = instance.AddComponent(m_characterType);
            m_targets.Add(target);
            return target;
        }

        private GameObject CreateObject()
        {
            var instance = new GameObject("Lock-on test");
            instance.SetActive(false);
            m_objects.Add(instance);
            return instance;
        }
    }
}
