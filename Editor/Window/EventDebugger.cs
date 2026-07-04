using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace reromanlee.EventAggregator.Editor
{
    /// <summary>
    /// Live view of every <see cref="EventBus"/> in the current session: which events fired,
    /// who published them, which listeners received them, and how deep the chain reaction went.
    /// Each root publish and everything it caused form one block; blocks are chronological with
    /// the newest at the bottom, and the view follows new events automatically unless the user
    /// scrolled up to read history — returning to the bottom resumes following. Data comes from
    /// the editor-only <see cref="EventBusDebugger"/> trace; player builds are completely unaffected.
    /// </summary>
    internal class EventDebugger : EditorWindow
    {
        private const string WindowName = "Event Debugger";
        private const string AllBusesChoice = "All Buses";
        private const string DisposedSuffix = " (disposed)";
        // Rows kept in the UI; once exceeded, the oldest chain blocks are dropped from the top.
        private const int MaxVisibleRows = 2000;
        private const double PollInterval = 0.1;
        // How close to the bottom (in pixels) the scroll must be to keep following new events.
        // Scrolling up further than this detaches the view; scrolling back re-attaches it.
        private const float StickToBottomSlack = 30f;

        [SerializeField] private VisualTreeAsset eventDebuggerAsset;
        [SerializeField] private VisualTreeAsset publisherAsset;
        [SerializeField] private VisualTreeAsset listenerAsset;

        [SerializeField] private Texture2D windowIconDark;
        [SerializeField] private Texture2D windowIconLight;

        // Serialized so the selection survives domain reloads. Bus display names are deterministic
        // per session ("EventBus 1", ...), so the selection also carries across play sessions.
        [SerializeField] private string selectedBus = AllBusesChoice;

        private readonly List<EventBusDebugger.TraceEntry> entryBuffer = new();
        private readonly List<EventBusDebugger.BusInfo> busBuffer = new();

        private VisualElement eventDebuggerElement;
        private int lastVersion = -1;
        private int lastClearCount;
        private long lastSequence = -1;
        private double nextPollTime;

        // The block currently receiving rows, and which chain it belongs to.
        private VisualElement currentBlock;
        private long currentBlockChainId = -1;
        private int totalRowCount;

        // Zebra state: flipped once per row and never reset across blocks, so stripes stay
        // alternating even between the last row of one chain and the root of the next.
        private bool zebraEven;

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
            // Resolve by index, not by label text: Unity renders menu shortcut notation
            // (a trailing "#N" etc.) specially, so labels are not reliable identifiers.
            int index = InstanceDropdownElement.index;
            selectedBus = index > 0 && index - 1 < busBuffer.Count
                ? busBuffer[index - 1].DisplayName
                : AllBusesChoice;
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
            currentBlock = null;
            currentBlockChainId = -1;
            totalRowCount = 0;
            zebraEven = false;
            EventContainerElement.Clear();
            RefreshDropdownChoices();
            AppendNewEntries();
            // A fresh view (filter change, clear, window open) always starts at the newest events.
            ScrollToBottomDeferred();
        }

        /// <summary>Keeps the dropdown choices aligned with <see cref="busBuffer"/>:
        /// choice 0 is "All Buses", choice N is busBuffer[N - 1].</summary>
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
            bool followBottom = IsNearBottom();
            VisualElement container = EventContainerElement;

            foreach (EventBusDebugger.TraceEntry entry in entryBuffer)
            {
                if (selectedBusId != -1 && entry.BusId != selectedBusId)
                {
                    continue;
                }

                // A new chain starts a new block at the bottom; rows keep appending to the block
                // of their chain so a chain reaction always reads downward in firing order.
                if (currentBlock == null || entry.ChainId != currentBlockChainId)
                {
                    currentBlock = new VisualElement();
                    currentBlock.AddToClassList("event-block");
                    currentBlockChainId = entry.ChainId;
                    container.Add(currentBlock);
                }

                VisualElement row = CreateRow(entry);
                row.AddToClassList(zebraEven ? "container-even" : "container-odd");
                zebraEven = !zebraEven;
                totalRowCount++;
                currentBlock.Add(row);
            }

            TrimOldestBlocks(container);

            if (followBottom)
            {
                ScrollToBottomDeferred();
            }
        }

        /// <summary>Drops whole blocks from the top (the oldest chains) once over the row budget.
        /// The newest block is always kept, however large.</summary>
        private void TrimOldestBlocks(VisualElement container)
        {
            while (totalRowCount > MaxVisibleRows && container.childCount > 1)
            {
                VisualElement oldest = container[0];
                totalRowCount -= oldest.childCount;
                oldest.RemoveFromHierarchy();
            }
        }

        /// <summary>True while the view should follow new events: no scrollbar yet, or scrolled to
        /// within <see cref="StickToBottomSlack"/> of the bottom.</summary>
        private bool IsNearBottom()
        {
            Scroller scroller = ScrollViewElement.verticalScroller;
            return scroller.highValue <= 0 || scroller.value >= scroller.highValue - StickToBottomSlack;
        }

        private void ScrollToBottomDeferred()
        {
            ScrollView scrollView = ScrollViewElement;
            // Deferred one tick so the freshly added rows have a layout before scrolling to them.
            scrollView.schedule.Execute(() => scrollView.verticalScroller.value = scrollView.verticalScroller.highValue);
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
            return entry.Kind == EventBusDebugger.EntryKind.Publish
                ? new Publisher(publisherAsset, entry.SourceName, entry.EventName, entry.Depth, entry.Timestamp)
                : new Listener(listenerAsset, entry.TargetName, entry.EventName, entry.Depth, entry.Timestamp);
        }
    }
}
