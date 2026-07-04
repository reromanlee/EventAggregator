using UnityEngine.UIElements;

namespace reromanlee.EventAggregator.Editor
{
    /// <summary>A trace row reading "event went to listener", indented by chain depth.</summary>
    internal class Listener : VisualElement
    {
        public Listener(VisualTreeAsset listenerAsset, string listenerName, string eventName, int eventDepth)
        {
            listenerAsset.CloneTree(this);
            this.Q<Label>("listener-name").text = listenerName;
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
