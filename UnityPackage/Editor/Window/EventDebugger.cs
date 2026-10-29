using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace reromanlee.EventAggregator.Editor
{
    /// <summary>
    /// Live view of every <see cref="EventBus"/> in the session: which events fired, who published them, which
    /// listeners received them, and how deep each chain reaction went. Rows are virtualized, so only the visible
    /// ones exist however long the trace grows. The view follows new events until the user scrolls up, and
    /// resumes following once scrolled back to the bottom. Buses report to it only while the window is open and
    /// recording, so a closed or paused window costs the publish path a single flag check.
    /// </summary>
    internal sealed class EventDebugger : EditorWindow
    {
        private const string WindowTitle = "Event Debugger";
        private const string AllBusesChoice = "All Buses";
        private const string DisposedSuffix = " (disposed)";

        // Assets are resolved by GUID so the window works wherever the package lives (registry, git, embedded)
        // and survives layouts restored from older package versions.
        private const string StyleSheetGuid = "3ade6b81fed3c69479eedbf8d28fece4";
        private const string IconForLightSkinGuid = "7231dbdc248b6ee44ab6c50fd0fad9e8";
        private const string IconForDarkSkinGuid = "1df4d2c5ce9f8da4d81e426bef126369";

        private const int MaxRows = EventBusDebugger.Capacity;
        private const int TrimChunk = MaxRows / 10;
        private const long PollIntervalMilliseconds = 100;
        private const float RowHeight = 20f;

        // One time per digit: proportional editor fonts give digits different widths, and the time column is
        // sized for the widest so every row's tree starts at the same position.
        private static readonly string[] TimeWidthSamples =
        {
            "00:00:00.000", "11:11:11.111", "22:22:22.222", "33:33:33.333", "44:44:44.444",
            "55:55:55.555", "66:66:66.666", "77:77:77.777", "88:88:88.888", "99:99:99.999",
        };

        // Serialized so the window keeps its setup across domain reloads and editor restarts.
        [SerializeField] private string selectedBusName = AllBusesChoice;
        [SerializeField] private string searchText = string.Empty;
        [SerializeField] private bool isRecordingEnabled = true;
        [SerializeField] private bool clearOnPlay = true;

        private readonly List<EventBusDebugger.TraceEntry> retainedEntries = new();
        private readonly List<EventBusDebugger.TraceEntry> visibleEntries = new();
        private readonly List<EventBusDebugger.TraceEntry> pendingEntries = new();
        private readonly List<EventBusDebugger.BusInfo> buses = new();

        // Remembers every bus name seen, so rows keep filtering correctly after their bus is forgotten by the hub.
        private readonly Dictionary<int, string> busNamesById = new();
        private readonly List<(string Name, bool IsDisposed)> busMenuItems = new();
        private readonly List<(string Name, bool IsDisposed)> nextBusMenuItems = new();

        private ListView listView;
        private ScrollView scrollView;
        private ToolbarMenu busMenu;
        private Label emptyLabel;
        private Label timeProbe;
        private float timeColumnWidth;

        // Follow intent changes only through user scrolling, never through layout, so resizing, re-docking or a
        // delayed layout pass cannot silently stop the view from following new events.
        private bool isFollowing = true;
        private bool isScrollingProgrammatically;

        private bool isRecorderActive;
        private int lastVersion = -1;
        private int lastClearCount = -1;
        private long lastSequence = -1;

        [MenuItem("Window/Analysis/Event Debugger")]
        public static void ShowWindow()
        {
            GetWindow<EventDebugger>();
        }

        private void OnEnable()
        {
            Texture2D icon = LoadAsset<Texture2D>(EditorGUIUtility.isProSkin ? IconForDarkSkinGuid : IconForLightSkinGuid);
            titleContent = new GUIContent(WindowTitle, icon);
            minSize = new Vector2(480f, 200f);
            SetRecorderActive(isRecordingEnabled);
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            SetRecorderActive(false);
        }

        private void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            StyleSheet styleSheet = LoadAsset<StyleSheet>(StyleSheetGuid);
            if (styleSheet != null)
            {
                root.styleSheets.Add(styleSheet);
            }
            root.AddToClassList("event-debugger");
            root.AddToClassList(EditorGUIUtility.isProSkin ? "event-debugger--dark" : "event-debugger--light");

            root.Add(CreateToolbar());

            VisualElement body = new();
            body.AddToClassList("event-debugger__body");
            root.Add(body);

            listView = new ListView(visibleEntries, RowHeight, () => new TraceRow(), BindRow)
            {
                selectionType = SelectionType.None,
                showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
            };
            listView.AddToClassList("event-debugger__list");
            body.Add(listView);
            scrollView = listView.Q<ScrollView>();
            scrollView.verticalScroller.valueChanged += OnScrolled;
            scrollView.contentViewport.RegisterCallback<GeometryChangedEvent>(OnListGeometryChanged);
            scrollView.contentContainer.RegisterCallback<GeometryChangedEvent>(OnListGeometryChanged);

            emptyLabel = new Label { pickingMode = PickingMode.Ignore };
            emptyLabel.AddToClassList("event-debugger__empty");
            body.Add(emptyLabel);

            // An invisible time label styled like the rows, so the column is measured with the real editor font.
            timeProbe = new Label(TimeWidthSamples[0]) { pickingMode = PickingMode.Ignore };
            timeProbe.AddToClassList("trace-row__time");
            timeProbe.AddToClassList("event-debugger__time-probe");
            timeProbe.RegisterCallback<GeometryChangedEvent>(OnTimeProbeGeometryChanged);
            body.Add(timeProbe);

            ResetRows();
            root.schedule.Execute(Poll).Every(PollIntervalMilliseconds);
        }

        private Toolbar CreateToolbar()
        {
            Toolbar toolbar = new();
            toolbar.Add(new ToolbarButton(OnClearClicked) { text = "Clear", tooltip = "Discard all recorded events." });

            ToolbarToggle clearOnPlayToggle = new() { text = "Clear on Play", value = clearOnPlay, tooltip = "Discard recorded events when entering Play Mode." };
            clearOnPlayToggle.RegisterValueChangedCallback(change => clearOnPlay = change.newValue);
            toolbar.Add(clearOnPlayToggle);

            ToolbarToggle recordToggle = new() { text = "Record", value = isRecordingEnabled, tooltip = "Capture publishes and deliveries. Paused buses skip all tracing work." };
            recordToggle.RegisterValueChangedCallback(change =>
            {
                isRecordingEnabled = change.newValue;
                SetRecorderActive(isRecordingEnabled);
                UpdateEmptyLabel();
            });
            toolbar.Add(recordToggle);

            VisualElement spacer = new();
            spacer.AddToClassList("event-debugger__spacer");
            toolbar.Add(spacer);

            ToolbarSearchField searchField = new() { tooltip = "Filter by event, publisher or listener name." };
            searchField.SetValueWithoutNotify(searchText);
            searchField.AddToClassList("event-debugger__search");
            searchField.RegisterValueChangedCallback(change =>
            {
                searchText = change.newValue ?? string.Empty;
                RebuildVisibleEntries(scrollToBottom: true);
            });
            toolbar.Add(searchField);

            busMenu = new ToolbarMenu { text = selectedBusName, tooltip = "Show events of one bus, or of all buses." };
            busMenu.AddToClassList("event-debugger__bus-menu");
            toolbar.Add(busMenu);
            return toolbar;
        }

        private void SetRecorderActive(bool isActive)
        {
            if (isActive == isRecorderActive)
            {
                return;
            }
            isRecorderActive = isActive;
            if (isActive)
            {
                EventBusDebugger.AddRecorder();
            }
            else
            {
                EventBusDebugger.RemoveRecorder();
            }
        }

        private void OnClearClicked()
        {
            EventBusDebugger.Clear();
            ResetRows();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            // Cleared before Play Mode code runs, so events from the first Awake/OnEnable calls are kept.
            if (change != PlayModeStateChange.ExitingEditMode || !clearOnPlay)
            {
                return;
            }

            EventBusDebugger.Clear();
            if (listView != null)
            {
                ResetRows();
            }
        }

        /// <summary>Cheap poll: does real work only when the trace changed since the last one.</summary>
        private void Poll()
        {
            int version = EventBusDebugger.Version;
            if (version == lastVersion)
            {
                return;
            }
            lastVersion = version;

            if (EventBusDebugger.ClearCount != lastClearCount)
            {
                ResetRows();
                return;
            }
            RefreshBuses();
            AppendNewEntries();
        }

        /// <summary>Discards all rows and rebuilds from whatever the trace currently retains.</summary>
        private void ResetRows()
        {
            lastClearCount = EventBusDebugger.ClearCount;
            lastSequence = -1;
            retainedEntries.Clear();
            visibleEntries.Clear();
            busNamesById.Clear();
            RefreshBuses();

            lastSequence = EventBusDebugger.CopyEntriesSince(lastSequence, retainedEntries);
            RebuildVisibleEntries(scrollToBottom: true);
        }

        private void AppendNewEntries()
        {
            pendingEntries.Clear();
            lastSequence = EventBusDebugger.CopyEntriesSince(lastSequence, pendingEntries);
            if (pendingEntries.Count == 0)
            {
                return;
            }

            retainedEntries.AddRange(pendingEntries);
            if (retainedEntries.Count > MaxRows)
            {
                TrimOldestEntries();
                return;
            }

            foreach (EventBusDebugger.TraceEntry entry in pendingEntries)
            {
                if (Matches(entry))
                {
                    visibleEntries.Add(entry);
                }
            }
            listView.RefreshItems();
            if (isFollowing)
            {
                ScrollToBottom();
            }
            UpdateEmptyLabel();
        }

        /// <summary>Drops the oldest rows in one chunk. A user reading older rows keeps seeing the same ones.</summary>
        private void TrimOldestEntries()
        {
            int removedCount = retainedEntries.Count - MaxRows + TrimChunk;
            long firstKeptSequence = retainedEntries[removedCount].Sequence;
            retainedEntries.RemoveRange(0, removedCount);

            int removedVisibleCount = 0;
            while (removedVisibleCount < visibleEntries.Count && visibleEntries[removedVisibleCount].Sequence < firstKeptSequence)
            {
                removedVisibleCount++;
            }
            float keptOffset = scrollView.verticalScroller.value - removedVisibleCount * RowHeight;

            RebuildVisibleEntries(scrollToBottom: isFollowing);
            if (!isFollowing)
            {
                SetScrollOffset(Mathf.Max(0f, keptOffset));
            }
        }

        private void RebuildVisibleEntries(bool scrollToBottom)
        {
            visibleEntries.Clear();
            foreach (EventBusDebugger.TraceEntry entry in retainedEntries)
            {
                if (Matches(entry))
                {
                    visibleEntries.Add(entry);
                }
            }
            listView.RefreshItems();
            if (scrollToBottom)
            {
                isFollowing = true;
                ScrollToBottom();
            }
            UpdateEmptyLabel();
        }

        private bool Matches(in EventBusDebugger.TraceEntry entry)
        {
            if (selectedBusName != AllBusesChoice
                && (!busNamesById.TryGetValue(entry.BusId, out string busName) || busName != selectedBusName))
            {
                return false;
            }

            return searchText.Length == 0
                || TypeNames.Get(entry.EventType).IndexOf(searchText, System.StringComparison.OrdinalIgnoreCase) >= 0
                || TypeNames.Get(entry.SubjectType).IndexOf(searchText, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void BindRow(VisualElement element, int index)
        {
            EventBusDebugger.TraceEntry entry = visibleEntries[index];
            bool isChainStart = index > 0 && visibleEntries[index - 1].ChainId != entry.ChainId;
            ((TraceRow)element).Bind(entry, isChainStart, timeColumnWidth);
        }

        /// <summary>Syncs bus names and the bus menu. Buses sharing a display name are listed, and filtered, as one.</summary>
        private void RefreshBuses()
        {
            EventBusDebugger.GetBuses(buses);

            bool isRenamed = false;
            foreach (EventBusDebugger.BusInfo bus in buses)
            {
                if (busNamesById.TryGetValue(bus.Id, out string knownName) && knownName == bus.DisplayName)
                {
                    continue;
                }
                isRenamed |= knownName != null;
                busNamesById[bus.Id] = bus.DisplayName;
            }

            if (UpdateBusMenuItems())
            {
                RebuildBusMenu();
            }
            if (isRenamed && selectedBusName != AllBusesChoice)
            {
                RebuildVisibleEntries(scrollToBottom: false);
            }
        }

        /// <summary>Recomputes the distinct bus names; returns true when the menu needs rebuilding.</summary>
        private bool UpdateBusMenuItems()
        {
            nextBusMenuItems.Clear();
            foreach (EventBusDebugger.BusInfo bus in buses)
            {
                int index = IndexOfName(nextBusMenuItems, bus.DisplayName);
                if (index < 0)
                {
                    nextBusMenuItems.Add((bus.DisplayName, bus.IsDisposed));
                }
                else if (!bus.IsDisposed)
                {
                    // A name reads as disposed only while every bus carrying it is disposed.
                    nextBusMenuItems[index] = (bus.DisplayName, false);
                }
            }

            bool isChanged = nextBusMenuItems.Count != busMenuItems.Count;
            for (int i = 0; !isChanged && i < busMenuItems.Count; i++)
            {
                isChanged = busMenuItems[i] != nextBusMenuItems[i];
            }

            if (isChanged)
            {
                busMenuItems.Clear();
                busMenuItems.AddRange(nextBusMenuItems);
            }
            return isChanged;
        }

        private static int IndexOfName(List<(string Name, bool IsDisposed)> items, string name)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Name == name)
                {
                    return i;
                }
            }
            return -1;
        }

        private void RebuildBusMenu()
        {
            DropdownMenu menu = busMenu.menu;
            menu.MenuItems().Clear();
            AppendBusAction(menu, AllBusesChoice, AllBusesChoice);
            if (busMenuItems.Count > 0)
            {
                menu.AppendSeparator();
            }
            foreach ((string name, bool isDisposed) in busMenuItems)
            {
                AppendBusAction(menu, isDisposed ? name + DisposedSuffix : name, name);
            }
            busMenu.text = selectedBusName;
        }

        private void AppendBusAction(DropdownMenu menu, string label, string busName)
        {
            menu.AppendAction(
                label,
                _ => SelectBus(busName),
                _ => busName == selectedBusName ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
        }

        private void SelectBus(string busName)
        {
            selectedBusName = busName;
            busMenu.text = busName;
            RebuildVisibleEntries(scrollToBottom: true);
        }

        private void UpdateEmptyLabel()
        {
            bool isEmpty = visibleEntries.Count == 0;
            emptyLabel.style.display = isEmpty ? DisplayStyle.Flex : DisplayStyle.None;
            if (isEmpty)
            {
                emptyLabel.text = retainedEntries.Count > 0 ? "No events match the current filter."
                    : isRecordingEnabled ? "No events recorded yet.\nEvents published while this window is open appear here."
                    : "Recording is paused.\nEnable Record in the toolbar to capture events.";
            }
        }

        /// <summary>Sizes the time column for the widest possible time in the font the rows actually render with.</summary>
        private void OnTimeProbeGeometryChanged(GeometryChangedEvent change)
        {
            float width = 0f;
            foreach (string sample in TimeWidthSamples)
            {
                width = Mathf.Max(width, timeProbe.MeasureTextSize(sample, 0f, VisualElement.MeasureMode.Undefined, 0f, VisualElement.MeasureMode.Undefined).x);
            }
            width = Mathf.Ceil(width);
            if (width > 0f && !Mathf.Approximately(width, timeColumnWidth))
            {
                timeColumnWidth = width;
                listView.RefreshItems();
            }
        }

        /// <summary>Only user scrolling changes the follow intent: scrolling up stops following, reaching the bottom resumes it.</summary>
        private void OnScrolled(float value)
        {
            if (!isScrollingProgrammatically)
            {
                isFollowing = IsScrolledToBottom();
            }
        }

        /// <summary>Re-snaps after every resize of the list or its content, which may land after the rows were added.</summary>
        private void OnListGeometryChanged(GeometryChangedEvent change)
        {
            if (isFollowing)
            {
                ScrollToBottom();
            }
        }

        /// <summary>True when there is nothing to scroll yet, or the view is within one row of the bottom.</summary>
        private bool IsScrolledToBottom()
        {
            Scroller scroller = scrollView.verticalScroller;
            float highValue = scroller.highValue;
            return float.IsNaN(highValue) || highValue <= 0f || scroller.value >= highValue - RowHeight;
        }

        private void ScrollToBottom()
        {
            if (visibleEntries.Count == 0)
            {
                return;
            }
            isScrollingProgrammatically = true;
            try
            {
                listView.ScrollToItem(-1);
            }
            finally
            {
                isScrollingProgrammatically = false;
            }
        }

        private void SetScrollOffset(float offset)
        {
            isScrollingProgrammatically = true;
            try
            {
                scrollView.verticalScroller.value = offset;
            }
            finally
            {
                isScrollingProgrammatically = false;
            }
        }

        private static T LoadAsset<T>(string guid) where T : Object
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<T>(path);
        }
    }
}
