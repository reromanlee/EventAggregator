using System;
using System.Globalization;
using UnityEngine.UIElements;

namespace reromanlee.EventAggregator.Editor
{
    /// <summary>
    /// One reusable, virtualized row of the trace. A publish reads "publisher fired event" and names its bus, a
    /// delivery reads "event went to listener"; both are timestamped and indented by chain depth.
    /// </summary>
    internal sealed class TraceRow : VisualElement
    {
        private const string TimeFormat = "HH:mm:ss.fff";

        private readonly Label timeLabel = new();
        private readonly VisualElement indent = new();
        private readonly VisualElement leadingIcon = new();
        private readonly Label leadingLabel = new();
        private readonly VisualElement connectorIcon = new();
        private readonly VisualElement trailingIcon = new();
        private readonly Label trailingLabel = new();
        private readonly VisualElement busContainer = new();
        private readonly Label busLabel = new();

        private float appliedTimeWidth;
        private bool isBusShown = true;

        public TraceRow()
        {
            AddToClassList("trace-row");
            AddPart(this, timeLabel, "trace-row__time");
            AddPart(this, indent, "trace-row__indent");
            AddPart(this, leadingIcon, "trace-row__icon");
            AddPart(this, leadingLabel, "trace-row__label");
            AddPart(this, connectorIcon, "trace-row__connector");
            AddPart(this, trailingIcon, "trace-row__icon");
            AddPart(this, trailingLabel, "trace-row__label");
            trailingLabel.AddToClassList("trace-row__label--trailing");

            // The width cap sits on a plain container: Unity's text measurement applies a percentage max-width set
            // on a label a second time, cutting bus names far shorter.
            AddPart(this, busContainer, "trace-row__bus");
            AddPart(busContainer, busLabel, "trace-row__bus-name");
        }

        /// <param name="entry">The trace entry to display.</param>
        /// <param name="isChainStart">Draws a divider above the row to separate it from the previous chain.</param>
        /// <param name="busName">Shown at the row's end; null shows nothing.</param>
        /// <param name="timeWidth">Fixed width of the time column, so every row's tree lines up; 0 sizes it to its text.</param>
        public void Bind(in EventBusDebugger.TraceEntry entry, bool isChainStart, string busName, float timeWidth)
        {
            EnableInClassList("trace-row--chain-start", isChainStart);
            SetTimeWidth(timeWidth);
            timeLabel.text = new DateTime(entry.TimestampTicks, DateTimeKind.Utc).ToLocalTime().ToString(TimeFormat, CultureInfo.InvariantCulture);
            SetIndent(entry.Depth);

            bool isPublish = entry.Kind == EventBusDebugger.EntryKind.Publish;
            string subjectName = TypeNames.Get(entry.SubjectType);
            string eventName = TypeNames.Get(entry.EventType);

            SetIcon(leadingIcon, isEvent: !isPublish);
            leadingLabel.text = isPublish ? subjectName : eventName;
            connectorIcon.EnableInClassList("trace-row__connector--broadcast", isPublish);
            connectorIcon.EnableInClassList("trace-row__connector--arrow", !isPublish);
            SetIcon(trailingIcon, isEvent: isPublish);
            trailingLabel.text = isPublish ? eventName : subjectName;
            SetBusName(busName);
        }

        private static void AddPart(VisualElement parent, VisualElement part, string className)
        {
            part.AddToClassList(className);
            part.pickingMode = PickingMode.Ignore;
            parent.Add(part);
        }

        private void SetBusName(string busName)
        {
            bool isShown = busName != null;
            if (isShown != isBusShown)
            {
                busContainer.style.display = isShown ? DisplayStyle.Flex : DisplayStyle.None;
                isBusShown = isShown;
            }

            if (isShown)
            {
                busLabel.text = busName;
            }
        }

        private void SetTimeWidth(float width)
        {
            if (width == appliedTimeWidth)
            {
                return;
            }
            appliedTimeWidth = width;
            timeLabel.style.width = width > 0f ? new StyleLength(width) : new StyleLength(StyleKeyword.Null);
        }

        /// <summary>Reuses guide elements so scrolling through rows of varying depth allocates nothing in steady state.</summary>
        private void SetIndent(int depth)
        {
            while (indent.childCount < depth)
            {
                VisualElement guide = new() { pickingMode = PickingMode.Ignore };
                guide.AddToClassList("trace-row__indent-guide");
                indent.Add(guide);
            }
            while (indent.childCount > depth)
            {
                indent.RemoveAt(indent.childCount - 1);
            }
        }

        private static void SetIcon(VisualElement icon, bool isEvent)
        {
            icon.EnableInClassList("trace-row__icon--event", isEvent);
            icon.EnableInClassList("trace-row__icon--publisher", !isEvent);
        }
    }
}
