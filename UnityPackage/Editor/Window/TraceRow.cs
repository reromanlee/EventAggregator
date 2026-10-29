using System;
using System.Globalization;
using UnityEngine.UIElements;

namespace reromanlee.EventAggregator.Editor
{
    /// <summary>
    /// One reusable, virtualized row of the trace. A publish reads "publisher fired event", a delivery reads
    /// "event went to listener"; both are timestamped and indented by chain depth.
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

        private float appliedTimeWidth;

        public TraceRow()
        {
            AddToClassList("trace-row");
            AddPart(timeLabel, "trace-row__time");
            AddPart(indent, "trace-row__indent");
            AddPart(leadingIcon, "trace-row__icon");
            AddPart(leadingLabel, "trace-row__label");
            AddPart(connectorIcon, "trace-row__connector");
            AddPart(trailingIcon, "trace-row__icon");
            AddPart(trailingLabel, "trace-row__label");
        }

        /// <param name="entry">The trace entry to display.</param>
        /// <param name="isChainStart">Draws a divider above the row to separate it from the previous chain.</param>
        /// <param name="timeWidth">Fixed width of the time column, so every row's tree lines up; 0 sizes it to its text.</param>
        public void Bind(in EventBusDebugger.TraceEntry entry, bool isChainStart, float timeWidth)
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
        }

        private void AddPart(VisualElement part, string className)
        {
            part.AddToClassList(className);
            part.pickingMode = PickingMode.Ignore;
            Add(part);
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
