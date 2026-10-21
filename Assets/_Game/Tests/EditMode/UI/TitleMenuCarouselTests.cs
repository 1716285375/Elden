using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace ZZ.Tests
{
    public sealed class TitleMenuCarouselTests
    {
        [Test]
        public void SelectionShowsOnlyThreeCommandsAndSkipsDisabledContinue()
        {
            var root = new GameObject("Carousel", typeof(RectTransform));
            try
            {
                var type = Type.GetType("ZZ.TitleMenuCarousel, Assembly-CSharp", true);
                Component carousel = root.AddComponent(type);
                var buttons = new Button[6];
                for (int index = 0; index < buttons.Length; index++)
                {
                    var child = new GameObject("Command", typeof(RectTransform), typeof(Button));
                    child.transform.SetParent(root.transform, false);
                    buttons[index] = child.GetComponent<Button>();
                }
                buttons[0].interactable = false;
                type.GetMethod("Configure").Invoke(carousel, new object[] { buttons });
                for (int selected = 1; selected < buttons.Length; selected++)
                {
                    type.GetMethod("Select").Invoke(carousel, new object[] { buttons[selected] });
                    int visible = 0;
                    foreach (Button button in buttons)
                    {
                        CanvasGroup group = button.GetComponent<CanvasGroup>();
                        if (group.alpha > 0f)
                        {
                            visible++;
                        }
                        Assert.That(group.blocksRaycasts, Is.EqualTo(group.alpha > 0f));
                    }
                    Assert.That(visible, Is.EqualTo(3));
                    Assert.That(buttons[selected].GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
                    Assert.That(buttons[selected].navigation.selectOnUp, Is.Not.EqualTo(buttons[0]));
                    Assert.That(buttons[selected].navigation.selectOnDown, Is.Not.EqualTo(buttons[0]));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
