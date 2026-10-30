using System;
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

        [Test]
        public void TraceRow_ShowsTheBusNameOnlyWhenGiven()
        {
            EventBusDebugger.TraceEntry entry = new(0, 0, DateTime.UtcNow.Ticks, typeof(WindowProbe), typeof(ProbeListener), 0, 0, EventBusDebugger.EntryKind.Publish);
            TraceRow row = new();
            VisualElement bus = row.Q(className: "trace-row__bus");
            Label busLabel = bus.Q<Label>(className: "trace-row__bus-name");

            row.Bind(entry, isChainStart: false, busName: "Row Bus", timeWidth: 0f);
            Assert.That(busLabel.text, Is.EqualTo("Row Bus"));
            Assert.That(bus.style.display.value, Is.EqualTo(DisplayStyle.Flex));

            row.Bind(entry, isChainStart: false, busName: null, timeWidth: 0f);
            Assert.That(bus.style.display.value, Is.EqualTo(DisplayStyle.None));

            row.Bind(entry, isChainStart: false, busName: "Row Bus", timeWidth: 0f);
            Assert.That(bus.style.display.value, Is.EqualTo(DisplayStyle.Flex));
        }
    }
}
