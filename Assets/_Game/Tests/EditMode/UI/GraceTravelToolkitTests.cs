using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace ZZ.Tests
{
    public class GraceTravelToolkitTests
    {
        [Test]
        public void GraceServicesAndTravelConfirmationHaveIndependentNativeControls()
        {
            VisualElement grace = Resources.Load<VisualTreeAsset>("UI/SiteOfGrace").CloneTree();
            foreach (string command in new[] { "level-up", "storage", "travel", "leave" })
            {
                Assert.That(grace.Q<Button>(command), Is.Not.Null, command);
            }
            VisualElement travel = Resources.Load<VisualTreeAsset>("UI/TravelMenu").CloneTree();
            Assert.That(travel.Q<ListView>("destinations"), Is.Not.Null);
            Assert.That(travel.Q("travel-content").Contains(travel.Q("travel-confirmation")), Is.False);
            Assert.That(travel.Q<Button>("confirm"), Is.Not.Null);
            Assert.That(travel.Q<Button>("cancel"), Is.Not.Null);
        }

        [Test]
        public void ClosedTravelRejectsRequestsAndCloseDiscardsPendingDestination()
        {
            Type type = Type.GetType("ZZ.PlayerUITeleportLocationManager, Assembly-CSharp", true);
            GameObject owner = new GameObject("Travel Lifecycle Test");
            try
            {
                Component manager = owner.AddComponent(type);
                type.GetMethod("RequestTravel").Invoke(manager, new object[] { 1234 });
                Assert.That(type.GetProperty("IsConfirmationOpen").GetValue(manager), Is.False);
                FieldInfo pending = type.GetField("m_pendingDestination", BindingFlags.Instance | BindingFlags.NonPublic);
                pending.SetValue(manager, 1234);
                type.GetMethod("CloseMenu").Invoke(manager, null);
                Assert.That(type.GetProperty("IsConfirmationOpen").GetValue(manager), Is.False);
                Assert.DoesNotThrow(() => type.GetMethod("ConfirmTravel").Invoke(manager, null));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }
    }
}
