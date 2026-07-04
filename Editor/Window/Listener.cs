using UnityEngine.UIElements;

namespace reromanlee.EventAggregator.Editor
{
    internal class Listener : VisualElement
    {
        public Listener(VisualTreeAsset listenerAsset)
        {
            listenerAsset.CloneTree(this);
        }

        public VisualElement EventContainerElement
        {
            get => this.Q<VisualElement>("event-container");
        }

        public string ListenerName
        {
            get => this.Q<Label>("listener-name").text;
            set => this.Q<Label>("listener-name").text = value;
        }

        public string EventName
        {
            get => this.Q<Label>("event-name").text;
            set => this.Q<Label>("event-name").text = value;
        }

        public void SetIndentations(int eventDepth)
        {
            EventContainerElement.Query<VisualElement>("border-left").ForEach(x => x.RemoveFromHierarchy());
            for (int x = 0; x < eventDepth; x++)
            {
                VisualElement borderLeftElement = new();
                borderLeftElement.AddToClassList("border-left");
                EventContainerElement.Insert(0, borderLeftElement);
            }
        }
    }
}