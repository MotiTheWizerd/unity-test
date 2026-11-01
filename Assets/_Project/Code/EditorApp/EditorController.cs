using System;
using System.Collections.Generic;
using System.Linq;
using Forge.Data.Records;
using Forge.EditorApp.Tools;
using Forge.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Forge.EditorApp
{
    /// <summary>
    /// Routes input: global shortcuts (Ctrl+S save, Ctrl+Z / Ctrl+Y or
    /// Ctrl+Shift+Z undo/redo, 1-9 palette) are handled here; everything else is
    /// passed to the active tool. Pointer clicks over the UI panel are filtered out.
    /// </summary>
    public sealed class EditorController : MonoBehaviour
    {
        [SerializeField] Camera _camera;
        [SerializeField] EditorOverlay _overlay;
        [SerializeField] float _maxRayDistance = 2000f;

        [NonSerialized] EditorContext _context;
        [NonSerialized] Dictionary<ToolKind, IEditorTool> _tools;
        [NonSerialized] ToolKind? _activeKind;

        public void Initialize(EditorContext context, WorldView view)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            var picker = new ScenePicker(_maxRayDistance);
            _tools = new Dictionary<ToolKind, IEditorTool>
            {
                { ToolKind.Select, new SelectMoveTool(context, view, picker, _camera) },
                { ToolKind.Place, new PlaceTool(context, picker) },
            };
        }

        void OnDestroy()
        {
            if (_tools == null)
                return;
            foreach (var tool in _tools.Values.OfType<IDisposable>())
                tool.Dispose();
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (_context == null || keyboard == null || mouse == null)
                return;

            HandleShortcuts(keyboard);
            SwitchToolIfNeeded();

            // Right mouse belongs to the fly camera; tools see the left button only.
            var pointer = mouse.position.ReadValue();
            var overPanel = _overlay.IsPointerOverPanel(pointer);
            var looking = mouse.rightButton.isPressed;
            var input = new ToolInput(
                _camera.ScreenPointToRay(pointer),
                primaryPressed: mouse.leftButton.wasPressedThisFrame && !overPanel && !looking,
                primaryHeld: mouse.leftButton.isPressed,
                primaryReleased: mouse.leftButton.wasReleasedThisFrame,
                additive: keyboard.shiftKey.isPressed,
                cancel: keyboard.escapeKey.wasPressedThisFrame,
                delete: keyboard.deleteKey.wasPressedThisFrame);

            _tools[_activeKind.Value].Tick(input);
        }

        void HandleShortcuts(Keyboard keyboard)
        {
            if (keyboard.ctrlKey.isPressed)
            {
                if (keyboard.sKey.wasPressedThisFrame)
                    _context.Save();
                else if (keyboard.zKey.wasPressedThisFrame && !keyboard.shiftKey.isPressed)
                    _context.Undo();
                else if (keyboard.yKey.wasPressedThisFrame || (keyboard.zKey.wasPressedThisFrame && keyboard.shiftKey.isPressed))
                    _context.Redo();
                return;
            }

            var palette = _context.Session.World.Records.OfType<BaseObjectRecord>().OrderBy(r => r.Id).ToList();
            for (var i = 0; i < Math.Min(9, palette.Count); i++)
            {
                if (keyboard[Key.Digit1 + i].wasPressedThisFrame)
                {
                    _context.SelectedBaseId = palette[i].Id;
                    _context.ActiveTool = ToolKind.Place;
                }
            }
        }

        void SwitchToolIfNeeded()
        {
            if (_activeKind == _context.ActiveTool)
                return;

            if (_activeKind.HasValue)
                _tools[_activeKind.Value].Deactivate();
            _activeKind = _context.ActiveTool;
            _tools[_activeKind.Value].Activate();
        }
    }
}
