using System;
using System.Collections.Generic;
using Forge.EditorApp.Diagnostics;
using UnityEngine;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI
{
    /// <summary>
    /// The editor's UI Toolkit panel: a side dock and a status bar laid out in
    /// EditorPanel.uxml and styled by EditorPanel.uss. A thin adapter: each section
    /// is its own presenter (<see cref="IPanelSection"/>), and this component only
    /// composes them, refreshes them every frame and answers whether the pointer is over UI.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class EditorPanel : MonoBehaviour, IPointerBlocker
    {
        // Runtime-only state; see CLAUDE.md (play-mode reload) for why these are [NonSerialized].
        [NonSerialized] UIDocument _document;
        [NonSerialized] List<IPanelSection> _sections;

        VisualElement Root => _document.rootVisualElement;

        void Awake() => _document = GetComponent<UIDocument>();

        public void Initialize(EditorContext context, EditorCommands commands, EditorDiagnostics diagnostics)
        {
            // The document root covers the whole screen; only real panel elements may catch the pointer.
            Root.pickingMode = PickingMode.Ignore;
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
            Refresh();
        }

        /// <summary>Replaces the dock with an error box; the editor could not start.</summary>
        public void ShowFatal(string message)
        {
            PanelElements.SetVisible(PanelElements.Required<VisualElement>(Root, "dock"), false);
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

        void Update() => Refresh();

        void Refresh()
        {
            if (_sections == null)
                return;
            foreach (var section in _sections)
                section.Refresh();
        }
    }
}
