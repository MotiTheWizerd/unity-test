using System;
using System.Collections.Generic;
using Forge.Agent;
using Forge.EditorApp.Diagnostics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI
{
    /// <summary>
    /// The editor's UI Toolkit panel: the editing dock on the left, the agent dock on the right and a status bar,
    /// laid out in EditorPanel.uxml and styled by EditorPanel.uss. A thin adapter: each section is its own
    /// presenter (<see cref="IPanelSection"/>), and this component only composes them, refreshes them every frame
    /// and answers whether the UI owns the input.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class EditorPanel : MonoBehaviour, IInputBlocker
    {
        // Runtime-only state; see CLAUDE.md (play-mode reload) for why these are [NonSerialized].
        [NonSerialized] UIDocument _document;
        [NonSerialized] List<IPanelSection> _sections;
        [NonSerialized] EditorContext _context;
        [NonSerialized] VisualElement _editing;

        VisualElement Root => _document.rootVisualElement;

        void Awake() => _document = GetComponent<UIDocument>();

        /// <param name="agent">The world-building agent's conversation; null hides the agent dock.</param>
        public void Initialize(EditorContext context, EditorCommands commands, EditorDiagnostics diagnostics, AgentConversation agent)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            // The document root covers the whole screen; only real panel elements may catch the pointer.
            Root.pickingMode = PickingMode.Ignore;
            DisableKeyboardNavigation();
            _editing = PanelElements.Required<VisualElement>(Root, "editing");
            _sections = new List<IPanelSection>
            {
                new WorldSection(Root, context),
                new TransformSection(Root, context, commands),
                new SculptSection(Root, context),
                new PaletteSection(Root, context),
                new HistorySection(Root, context),
                new PerformanceSection(Root, context, diagnostics),
                new StatusBar(Root, context),
            };

            if (agent != null)
                _sections.Add(new AgentSection(Root, agent, context));
            else
                PanelElements.SetVisible(PanelElements.Required<VisualElement>(Root, "agent-dock"), false);
            Refresh();
        }

        // UI Toolkit turns arrow keys / WASD into navigation that walks focus between buttons, and
        // Space / Enter into "submit" on the focused one. Here those keys fly the camera and drive
        // shortcuts: holding D walked focus from a clicked button into the search field and then
        // typed into it. The panel is mouse-driven, so navigation is switched off at the root.
        void DisableKeyboardNavigation()
        {
            Root.RegisterCallback<NavigationMoveEvent>(Swallow, TrickleDown.TrickleDown);
            Root.RegisterCallback<NavigationSubmitEvent>(Swallow, TrickleDown.TrickleDown);
            Root.RegisterCallback<NavigationCancelEvent>(Swallow, TrickleDown.TrickleDown);
        }

        void Swallow(EventBase evt)
        {
            evt.StopImmediatePropagation();
            Root.focusController?.IgnoreEvent(evt);
        }

        /// <summary>Replaces the docks with an error box; the editor could not start.</summary>
        public void ShowFatal(string message)
        {
            PanelElements.SetVisible(PanelElements.Required<VisualElement>(Root, "dock"), false);
            PanelElements.SetVisible(PanelElements.Required<VisualElement>(Root, "agent-dock"), false);
            PanelElements.SetVisible(PanelElements.Required<VisualElement>(Root, "status"), false);
            PanelElements.SetVisible(PanelElements.Required<VisualElement>(Root, "fatal"), true);
            PanelElements.SetText(PanelElements.Required<Label>(Root, "fatal-message"), message);
        }

        public bool IsPointerOverUI(Vector2 screenPosition)
        {
            var panel = Root?.panel;
            if (panel == null)
                return false;

            // Panel coordinates have their origin top-left, the Input System's bottom-left.
            var panelPosition = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenPosition.x, Screen.height - screenPosition.y));
            return panel.Pick(panelPosition) != null;
        }

        public bool IsKeyboardCaptured
        {
            get
            {
                // Every text input carries the base text-field class. Focus may land on its inner
                // element (a TextField) or on the field that hosts it (a Slider with a number box
                // keeps focus itself), so look both up and down from the focused element.
                var focused = FocusedElement;
                if (focused == null)
                    return false;
                for (var element = focused; element != null; element = element.parent)
                {
                    if (element.ClassListContains(TextFieldClass))
                        return true;
                }

                return focused.Q(className: TextFieldClass) != null;
            }
        }

        static readonly string TextFieldClass = TextInputBaseField<string>.ussClassName;

        VisualElement FocusedElement => Root?.panel?.focusController?.focusedElement as VisualElement;

        void Update()
        {
            ReleaseFocusOnWorldClick();
            ReleaseFocusOnEscape();
            Refresh();
        }

        // Esc always hands the keyboard back to the shortcuts (sections may do more, e.g. clear a search).
        void ReleaseFocusOnEscape()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                FocusedElement?.Blur();
        }

        // UI Toolkit keeps focus until something else in the panel takes it. Clicking the
        // world should end typing, so the keyboard goes back to shortcuts and the camera.
        // Right mouse always does: holding it flies the camera with WASD, even when the drag
        // starts over the panel, and those keys must not land in a text field.
        void ReleaseFocusOnWorldClick()
        {
            var mouse = Mouse.current;
            if (mouse == null)
                return;

            var worldClick = (mouse.leftButton.wasPressedThisFrame || mouse.middleButton.wasPressedThisFrame)
                             && !IsPointerOverUI(mouse.position.ReadValue());
            if (worldClick || mouse.rightButton.isPressed)
                FocusedElement?.Blur();
        }

        void Refresh()
        {
            if (_sections == null)
                return;

            // While the agent's build is open, the editing controls rest, as the tools do.
            var editable = !_context.IsDraftOpen;
            if (_editing.enabledSelf != editable)
                _editing.SetEnabled(editable);

            foreach (var section in _sections)
                section.Refresh();
        }
    }
}
