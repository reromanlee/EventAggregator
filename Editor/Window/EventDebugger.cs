using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace reromanlee.EventAggregator.Editor
{
    internal class EventDebugger : EditorWindow
    {
        private const string WindowName = "Event Debugger";

        [SerializeField] private VisualTreeAsset eventDebuggerAsset;
        [SerializeField] private VisualTreeAsset publisherAsset;
        [SerializeField] private VisualTreeAsset listenerAsset;

        [SerializeField] private Texture2D windowIconDark;
        [SerializeField] private Texture2D windowIconLight;

        private VisualElement eventDebuggerElement;

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

        private string WindowId
        {
            get => $"{GetType().Name}.{GetEntityId()}";
        }

        private string DropdownValue
        {
            get => EditorPrefs.GetString($"{WindowId}.{nameof(DropdownValue)}", string.Empty);
            set => EditorPrefs.SetString($"{WindowId}.{nameof(DropdownValue)}", value);
        }

        private void OnEnable()
        {
            eventDebuggerElement = eventDebuggerAsset.CloneTree();
            eventDebuggerElement.style.flexGrow = 1;

            ClearButtonElement.RegisterCallback<ClickEvent>(OnClearButtonClick);

            InstanceDropdownElement.value = DropdownValue;
            InstanceDropdownElement.RegisterValueChangedCallback(OnDropdownValueChange);

            rootVisualElement.Add(eventDebuggerElement);
        }

        private void OnDisable()
        {

        }

        private void OnClearButtonClick(ClickEvent callback)
        {
            throw new NotImplementedException();
        }

        private void OnDropdownValueChange(ChangeEvent<string> callback)
        {
            throw new NotImplementedException();
        }
    }
}