using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace reromanlee.EventAggregator.Editor
{
    /// <summary>
    /// Live view of every <see cref="EventBus"/> in the current session: which events fired,
    /// who published them, which listeners received them, and how deep the chain reaction went.
    /// Data comes from the editor-only <see cref="EventBusDebugger"/> trace; player builds are
    /// completely unaffected.
    /// </summary>
    internal class EventDebugger : EditorWindow
    {
        private const string WindowName = "Event Debugger";
        private const string AllBusesChoice = "All Buses";
        private const string DisposedSuffix = " (disposed)";
        // Rows kept in the UI; oldest are removed in pairs so the zebra striping never flips.
        private const int MaxVisibleRows = 2000;
        private const double PollInterval = 0.1;

        [SerializeField] private VisualTreeAsset eventDebuggerAsset;
        [SerializeField] private VisualTreeAsset publisherAsset;
        [SerializeField] private VisualTreeAsset listenerAsset;

        [SerializeField] private Texture2D windowIconDark;
        [SerializeField] private Texture2D windowIconLight;

        // Serialized so the selection survives domain reloads. Bus display names are deterministic
        // per session ("EventBus #1", ...), so the selection also carries across play sessions.
        [SerializeField] private string selectedBus = AllBusesChoice;

        private readonly List<EventBusDebugger.TraceEntry> entryBuffer = new();
        private readonly List<EventBusDebugger.BusInfo> busBuffer = new();

        private VisualElement eventDebuggerElement;
        private int lastVersion = -1;
        private int lastClearCount;
        private long lastSequence = -1;
        private int visibleRowCount;
        private double nextPollTime;

        private VisualElement EventContainerElement
        {
            get => eventDebuggerElement.Q<VisualElement>("event-container");
        }

        private VisualElement ClearButtonElement
        {
            get => eventDebuggerElement.Q<VisualElement>("clear-button");
        }

        private DropdownField InstanceDropdownElement
        {
            get => eventDebuggerElement.Q<DropdownField>("instance-dropdown");
        }

        private ScrollView ScrollViewElement
        {
            get => eventDebuggerElement.Q<ScrollView>();
        }

        [MenuItem("Tools/Event Debugger")]
        public static void ShowWindow()
        {
            EventDebugger window = GetWindow<EventDebugger>();
            if (EditorGUIUtility.isProSkin)
            {
                window.titleContent = new GUIContent(WindowName, window.windowIconLight);
            }
            else
            {
                window.titleContent = new GUIContent(WindowName, window.windowIconDark);
            }
            window.minSize = new Vector2(600, 400);
            window.Show();
        }

        private void OnEnable()
        {
            eventDebuggerElement = eventDebuggerAsset.CloneTree();
            eventDebuggerElement.style.flexGrow = 1;

            ClearButtonElement.RegisterCallback<ClickEvent>(OnClearButtonClick);
            InstanceDropdownElement.RegisterValueChangedCallback(OnDropdownValueChange);

            rootVisualElement.Add(eventDebuggerElement);

            RebuildView();
            EditorApplication.update += PollTrace;
        }

        private void OnDisable()
        {
            EditorApplication.update -= PollTrace;
        }

        private void OnClearButtonClick(ClickEvent callback)
        {
            EventBusDebugger.Clear();
            RebuildView();
        }

        private void OnDropdownValueChange(ChangeEvent<string> callback)
        {
            string value = callback.newValue ?? AllBusesChoice;
            selectedBus = value.EndsWith(DisposedSuffix)
                ? value.Substring(0, value.Length - DisposedSuffix.Length)
                : value;
            RebuildView();
        }

        /// <summary>Cheap per-editor-frame poll: does real work only when the trace changed.</summary>
        private void PollTrace()
        {
            if (EditorApplication.timeSinceStartup < nextPollTime)
            {
                return;
            }
            nextPollTime = EditorApplication.timeSinceStartup + PollInterval;

            int traceVersion = EventBusDebugger.Version;
            if (traceVersion == lastVersion)
            {
                return;
            }
            lastVersion = traceVersion;

            if (EventBusDebugger.ClearCount != lastClearCount)
            {
                RebuildView();
                return;
            }
            RefreshDropdownChoices();
            AppendNewEntries();
        }

        /// <summary>Discards all rows and rebuilds from the currently retained trace.</summary>
        private void RebuildView()
        {
            lastClearCount = EventBusDebugger.ClearCount;
            lastSequence = -1;
            visibleRowCount = 0;
            EventContainerElement.Clear();
            RefreshDropdownChoices();
            AppendNewEntries();
        }

        private void RefreshDropdownChoices()
        {
            EventBusDebugger.GetBuses(busBuffer);

            List<string> choices = new(busBuffer.Count + 1) { AllBusesChoice };
            string currentLabel = selectedBus;
            foreach (EventBusDebugger.BusInfo info in busBuffer)
            {
                string label = info.IsDisposed ? info.DisplayName + DisposedSuffix : info.DisplayName;
                choices.Add(label);
                if (info.DisplayName == selectedBus)
                {
                    currentLabel = label;
                }
            }
            InstanceDropdownElement.choices = choices;
            InstanceDropdownElement.SetValueWithoutNotify(currentLabel);
        }

        private void AppendNewEntries()
        {
            entryBuffer.Clear();
            lastSequence = EventBusDebugger.CopyEntriesSince(lastSequence, entryBuffer);
            if (entryBuffer.Count == 0)
            {
                return;
            }

            int selectedBusId = ResolveSelectedBusId();
            bool stickToBottom = IsScrolledToBottom();
            VisualElement container = EventContainerElement;

            foreach (EventBusDebugger.TraceEntry entry in entryBuffer)
            {
                if (selectedBusId != -1 && entry.BusId != selectedBusId)
                {
                    continue;
                }
                container.Add(CreateRow(entry));
                visibleRowCount++;
            }

            // Drop the oldest rows in pairs so odd/even classes stay aligned with row parity.
            while (container.childCount > MaxVisibleRows)
            {
                container.RemoveAt(0);
                if (container.childCount > 0)
                {
                    container.RemoveAt(0);
                }
            }

            if (stickToBottom)
            {
                ScrollToBottomDeferred();
            }
        }

        /// <returns>-1 for all buses; <see cref="int.MinValue"/> when the selected bus is unknown,
        /// which matches no entries until a bus with that name registers.</returns>
        private int ResolveSelectedBusId()
        {
            if (selectedBus == AllBusesChoice)
            {
                return -1;
            }
            foreach (EventBusDebugger.BusInfo info in busBuffer)
            {
                if (info.DisplayName == selectedBus)
                {
                    return info.Id;
                }
            }
            return int.MinValue;
        }

        private VisualElement CreateRow(in EventBusDebugger.TraceEntry entry)
        {
            VisualElement row = entry.Kind == EventBusDebugger.EntryKind.Publish
                ? new Publisher(publisherAsset, entry.SourceName, entry.EventName, entry.Depth)
                : new Listener(listenerAsset, entry.TargetName, entry.EventName, entry.Depth);
            row.AddToClassList(visibleRowCount % 2 == 0 ? "container-odd" : "container-even");
            return row;
        }

        private bool IsScrolledToBottom()
        {
            Scroller scroller = ScrollViewElement.verticalScroller;
            return scroller.highValue <= 0 || scroller.value >= scroller.highValue - 1;
        }

        private void ScrollToBottomDeferred()
        {
            ScrollView scrollView = ScrollViewElement;
            // Deferred one tick so the freshly added rows have a layout before scrolling to them.
            scrollView.schedule.Execute(() => scrollView.verticalScroller.value = scrollView.verticalScroller.highValue);
        }
    }
}
