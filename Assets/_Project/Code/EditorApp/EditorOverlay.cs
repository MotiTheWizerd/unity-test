using System;
using System.Linq;
using Forge.Data.Records;
using Forge.EditorApp.Tools;
using Forge.Editing.Terrain;
using UnityEngine;

namespace Forge.EditorApp
{
    /// <summary>
    /// Minimal on-screen panel: palette, save, world stats and help.
    /// </summary>
    /// <remarks>
    /// SHORTCUT: immediate-mode GUI (OnGUI) for the walking skeleton. Replace
    /// with a UI Toolkit panel when the editor UI is designed (Phase 4).
    /// </remarks>
    public sealed class EditorOverlay : MonoBehaviour
    {
        const float Width = 280f;
        const float Margin = 10f;

        // Runtime-only state. [NonSerialized] stops Unity's play-mode script reload
        // from turning these into empty values that look like real state.
        [NonSerialized] EditorContext _context;
        [NonSerialized] string _fatal;
        [NonSerialized] Rect _panel = new Rect(Margin, Margin, Width, 0f);

        public void Initialize(EditorContext context) => _context = context;

        public void ShowFatal(string message) => _fatal = message;

        /// <summary>Screen-space check (origin bottom-left, as reported by the Input System).</summary>
        public bool IsPointerOverPanel(Vector2 screenPosition)
        {
            var guiPoint = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
            return _panel.Contains(guiPoint);
        }

        void OnGUI()
        {
            if (!string.IsNullOrEmpty(_fatal))
            {
                GUI.Box(new Rect(Margin, Margin, 520f, 120f), "Forge failed to start");
                GUI.Label(new Rect(Margin * 2, Margin * 3, 500f, 90f), _fatal);
                return;
            }

            if (_context == null)
                return;

            GUILayout.BeginArea(new Rect(Margin, Margin, Width, Screen.height - Margin * 2));
            GUILayout.BeginVertical(GUI.skin.box);

            var session = _context.Session;
            var world = session.World;
            GUILayout.Label($"<b>Forge Editor</b>  —  {session.ActivePluginId}{(session.IsDirty ? " *" : "")}", RichLabel());

            var selecting = _context.ActiveTool == ToolKind.Select;
            if (GUILayout.Toggle(selecting, selecting ? "Select / Move  ◀" : "Select / Move (Esc)", GUI.skin.button) && !selecting)
                _context.ActiveTool = ToolKind.Select;
            GUILayout.Label($"Selected: {_context.Selection.Count}");

            DrawSculptSection();

            GUILayout.Label("Place (1-9)");
            var palette = world.Records.OfType<BaseObjectRecord>().OrderBy(r => r.Id).ToList();
            for (var i = 0; i < palette.Count; i++)
            {
                var active = !selecting && palette[i].Id == _context.SelectedBaseId;
                var label = $"{(i < 9 ? (i + 1) + ". " : "")}{palette[i].DisplayName}{(active ? "  ◀" : "")}";
                if (GUILayout.Toggle(active, label, GUI.skin.button) && !active)
                {
                    _context.SelectedBaseId = palette[i].Id;
                    _context.ActiveTool = ToolKind.Place;
                }
            }

            GUILayout.Space(6f);
            GUILayout.BeginHorizontal();
            GUI.enabled = session.CanUndo;
            if (GUILayout.Button("Undo (Ctrl+Z)"))
                _context.Undo();
            GUI.enabled = session.CanRedo;
            if (GUILayout.Button("Redo (Ctrl+Y)"))
                _context.Redo();
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            if (session.CanUndo)
                GUILayout.Label($"Undo: {session.UndoDescription}");

            if (GUILayout.Button(session.IsDirty ? "Save (Ctrl+S) *" : "Save (Ctrl+S)"))
                _context.Save();

            GUILayout.Space(6f);
            GUILayout.Label($"Load order: {string.Join(" → ", world.LoadOrder)}");
            GUILayout.Label($"Records: {world.Records.Count}   Conflicts: {world.Conflicts.Count}   Warnings: {world.Warnings.Count}");
            GUILayout.Label(_context.Status);

            GUILayout.Space(6f);
            GUILayout.Label(
                "Select: click / Shift+click, drag gizmo arrows or center ball\n" +
                "Del: delete selection   Esc: cancel / deselect\n" +
                "B: sculpt (hold left mouse)   [ ]: brush size\n" +
                "Hold right mouse: look + WASD/QE fly, Shift fast");

            GUILayout.EndVertical();
            if (Event.current.type == EventType.Repaint)
            {
                var last = GUILayoutUtility.GetLastRect();
                _panel = new Rect(Margin, Margin, Width, last.height);
            }

            GUILayout.EndArea();
        }

        void DrawSculptSection()
        {
            var sculpting = _context.ActiveTool == ToolKind.Sculpt;
            if (GUILayout.Toggle(sculpting, sculpting ? "Sculpt terrain  ◀" : "Sculpt terrain (B)", GUI.skin.button) && !sculpting)
                _context.ActiveTool = ToolKind.Sculpt;
            if (!sculpting)
                return;

            GUILayout.BeginHorizontal();
            foreach (BrushMode mode in Enum.GetValues(typeof(BrushMode)))
            {
                if (GUILayout.Toggle(_context.BrushMode == mode, mode.ToString(), GUI.skin.button))
                    _context.BrushMode = mode;
            }

            GUILayout.EndHorizontal();
            GUILayout.Label($"Radius: {_context.BrushRadius:0.0} m   ([ ] keys)");
            _context.BrushRadius = GUILayout.HorizontalSlider(_context.BrushRadius, BrushSettings.MinRadius, 32f);
            GUILayout.Label($"Strength: {_context.BrushStrength:0.00}");
            _context.BrushStrength = GUILayout.HorizontalSlider(_context.BrushStrength, 0.05f, 1f);
            GUILayout.Space(4f);
        }

        static GUIStyle RichLabel() => new GUIStyle(GUI.skin.label) { richText = true };
    }
}
