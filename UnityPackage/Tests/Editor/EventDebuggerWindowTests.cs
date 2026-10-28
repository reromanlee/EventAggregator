using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace reromanlee.EventAggregator.Editor.Tests
{
    public sealed class EventDebuggerWindowTests
    {
        private readonly struct WindowProbe
        {
        }

        private sealed class ProbeListener : IEventListener<WindowProbe>
        {
            public void Handle(WindowProbe eventData)
            {
            }
        }

        [UnityTest]
        public IEnumerator OpenWindow_RecordsAndListsPublishedEvents()
        {
            EventDebugger window = EditorWindow.GetWindow<EventDebugger>();
            EventBus bus = new();
            try
            {
                Assert.That(EventBusDebugger.IsRecording, Is.True, "An open window must enable recording.");

                bus.SetDebugName(nameof(EventDebuggerWindowTests));
                bus.Subscribe(new ProbeListener());
                bus.Publish(new WindowProbe());

                ListView listView = null;
                double deadline = EditorApplication.timeSinceStartup + 5.0;
                while (EditorApplication.timeSinceStartup < deadline)
                {
                    listView ??= window.rootVisualElement.Q<ListView>();
                    if (listView != null && listView.itemsSource.Count >= 2)
                    {
                        break;
                    }
                    yield return null;
                }

                Assert.That(listView, Is.Not.Null, "The window did not build its UI.");
                Assert.That(listView.itemsSource.Count, Is.GreaterThanOrEqualTo(2), "The publish and its delivery should be listed.");
            }
            finally
            {
                bus.Dispose();
                window.Close();
            }
        }
    }
}
