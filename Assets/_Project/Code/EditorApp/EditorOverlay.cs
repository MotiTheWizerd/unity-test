using System;
using System.Linq;
using Forge.Data.Records;
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

            GUILayout.Label("Palette (1-9)");
            var palette = world.Records.OfType<BaseObjectRecord>().OrderBy(r => r.Id).ToList();
            for (var i = 0; i < palette.Count; i++)
            {
                var selected = palette[i].Id == _context.SelectedBaseId;
                var label = $"{(i < 9 ? (i + 1) + ". " : "")}{palette[i].DisplayName}{(selected ? "  ◀" : "")}";
                if (GUILayout.Toggle(selected, label, GUI.skin.button) && !selected)
                    _context.SelectedBaseId = palette[i].Id;
            }

            GUILayout.Space(6f);
            if (GUILayout.Button(session.IsDirty ? "Save (Ctrl+S) *" : "Save (Ctrl+S)"))
                _context.Save();

            GUILayout.Space(6f);
            GUILayout.Label($"Load order: {string.Join(" → ", world.LoadOrder)}");
            GUILayout.Label($"Records: {world.Records.Count}   Conflicts: {world.Conflicts.Count}   Warnings: {world.Warnings.Count}");
            GUILayout.Label(_context.Status);

            GUILayout.Space(6f);
            GUILayout.Label("Click: place   Del: delete under cursor\nRight-drag: look   WASD/QE: fly   Shift: fast");

            GUILayout.EndVertical();
            if (Event.current.type == EventType.Repaint)
            {
                var last = GUILayoutUtility.GetLastRect();
                _panel = new Rect(Margin, Margin, Width, last.height);
            }

            GUILayout.EndArea();
        }

        static GUIStyle RichLabel() => new GUIStyle(GUI.skin.label) { richText = true };
    }
}
