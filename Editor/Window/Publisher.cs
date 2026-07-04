using UnityEngine.UIElements;

namespace reromanlee.EventAggregator.Editor
{
    /// <summary>A trace row reading "publisher fired event", indented by chain depth.</summary>
    internal class Publisher : VisualElement
    {
        public Publisher(VisualTreeAsset publisherAsset, string publisherName, string eventName, int eventDepth)
        {
            publisherAsset.CloneTree(this);
            this.Q<Label>("publisher-name").text = publisherName;
            this.Q<Label>("event-name").text = eventName;

            VisualElement container = this.Q<VisualElement>("event-container");
            for (int i = 0; i < eventDepth; i++)
            {
                VisualElement indentGuide = new();
                indentGuide.AddToClassList("border-left");
                container.Insert(0, indentGuide);
            }
        }
    }
}
