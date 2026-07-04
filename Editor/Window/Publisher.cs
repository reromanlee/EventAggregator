using UnityEngine.UIElements;

namespace reromanlee.EventAggregator.Editor
{
    internal class Publisher : VisualElement
    {
        public Publisher(VisualTreeAsset publisherAsset)
        {
            publisherAsset.CloneTree(this);
        }

        public VisualElement EventContainerElement
        {
            get => this.Q<VisualElement>("event-container");
        }

        public string PublisherName
        {
            get => this.Q<Label>("publisher-name").text;
            set => this.Q<Label>("publisher-name").text = value;
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