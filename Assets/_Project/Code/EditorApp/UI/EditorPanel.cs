using System;
using System.Collections.Generic;
using Forge.Agent;
using Forge.EditorApp.Diagnostics;
using Forge.EditorApp.UI.Controls;
using Forge.World;
using Forge.World.Assets;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI
{
    /// <summary>
    /// The editor's UI Toolkit panel: the tool strip down the left edge, the tool options bar across the top (its gear
    /// opens the settings card), the assets browser along the bottom, the agent dock on the right and a status bar,
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
        [NonSerialized] TooltipLayer _tooltips; // keeps the panel's tooltips alive for as long as the panel

        VisualElement Root => _document.rootVisualElement;

        void Awake() => _document = GetComponent<UIDocument>();

        /// <param name="thumbnails">Pictures for the assets browser's object tiles; null leaves the placeholder icons.</param>
        /// <param name="groundLooks">Pictures for the assets browser's ground tiles; null leaves the placeholder icons.</param>
        /// <param name="agent">The world-building agent's conversation; null hides the agent dock.</param>
        public void Initialize(EditorContext context, EditorCommands commands, EditorDiagnostics diagnostics, IAssetThumbnails thumbnails, GroundLooks groundLooks, AgentConversation agent)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            // The document root covers the whole screen; only real panel elements may catch the pointer.
            Root.pickingMode = PickingMode.Ignore;
            DisableKeyboardNavigation();
            _editing = PanelElements.Required<VisualElement>(Root, "editing");
            _tooltips = new TooltipLayer(Root);
            _sections = new List<IPanelSection>
            {
                new WorldSection(Root, context),
                new SpaceSection(Root, context),
                new TimeSection(Root, context),
                new SelectionBoxView(Root, context),
                new ToolStrip(Root, context),
                new ToolOptionsBar(Root, context),
                new SelectionSection(Root, context, commands),
                new RectangleSection(Root, context),
                new PlacementSection(Root, context),
                new SculptSection(Root, context),
                new PaintSection(Root, context, commands),
                new WaterSection(Root, context, commands),
                new AssetsBrowser(Root, context, thumbnails, groundLooks),
                new HistorySection(Root, context),
                new FlySpeedSection(Root, context),
                new SettingsSection(Root),
                new PerformanceSection(Root, context, diagnostics),
                new StatusBar(Root, context),
            };

            if (agent != null)
            {
                _sections.Add(new AgentSection(Root, agent, context));
                _sections.Add(new AgentDockToggle(Root, agent));
            }
            else
            {
                PanelElements.SetVisible(PanelElements.Required<VisualElement>(Root, "agent-dock"), false);
                PanelElements.SetVisible(PanelElements.Required<VisualElement>(Root, "agent-toggle"), false);
            }
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
            ForgePopup.CloseAll(Root);
            PanelElements.SetVisible(PanelElements.Required<VisualElement>(Root, "tool-strip"), false);
            PanelElements.SetVisible(PanelElements.Required<VisualElement>(Root, "tool-options"), false);
            PanelElements.SetVisible(PanelElements.Required<VisualElement>(Root, "assets"), false);
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

        // Esc always hands the keyboard back to the shortcuts and closes popups (sections may do more, e.g. clear a search).
        void ReleaseFocusOnEscape()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame)
                return;
            FocusedElement?.Blur();
            ForgePopup.CloseAll(Root);
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
            // A press on the world never reaches the panel, so popups learn of it here.
            if (worldClick || mouse.rightButton.wasPressedThisFrame)
                ForgePopup.CloseAll(Root);
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
