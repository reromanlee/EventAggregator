using System;
using UnityEngine.UIElements;

namespace reromanlee.EventAggregator.Editor
{
    /// <summary>A trace row reading "publisher fired event", timestamped and indented by chain depth.
    /// The timestamp stays at the left edge; indent guides are inserted after it.</summary>
    internal class Publisher : VisualElement
    {
        public Publisher(VisualTreeAsset publisherAsset, string publisherName, string eventName, int eventDepth, DateTime timestamp)
        {
            publisherAsset.CloneTree(this);
            this.Q<Label>("publisher-name").text = publisherName;
            this.Q<Label>("event-name").text = eventName;

            VisualElement container = this.Q<VisualElement>("event-container");
            Label timeLabel = this.Q<Label>("time");
            int guideIndex = 0;
            if (timeLabel != null)
            {
                timeLabel.text = $"[{timestamp:HH:mm:ss}]";
                guideIndex = container.IndexOf(timeLabel) + 1;
            }
            for (int i = 0; i < eventDepth; i++)
            {
                VisualElement indentGuide = new();
                indentGuide.AddToClassList("border-left");
                container.Insert(guideIndex, indentGuide);
            }
        }
    }
}
