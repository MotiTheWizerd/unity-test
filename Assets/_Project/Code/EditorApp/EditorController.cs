using System;
using System.Collections.Generic;
using System.Linq;
using Forge.EditorApp.Gizmos;
using Forge.EditorApp.Tools;
using Forge.Editing.Placement;
using Forge.World;
using Forge.World.Assets;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Forge.EditorApp
{
    /// <summary>
    /// Routes input: global shortcuts (Ctrl+S save, Ctrl+Z / Ctrl+Y or
    /// Ctrl+Shift+Z undo/redo, 1-9 palette, B sculpt, W/E/R move/rotate/scale,
    /// [ ] brush size, End drop to ground, F frame selection) are handled here; everything else is
    /// passed to the active tool. Pointer clicks over the UI panel are filtered out, and so are
    /// shortcuts while a UI text field has the keyboard.
    /// </summary>
    public sealed class EditorController : MonoBehaviour
    {
        [SerializeField] Camera _camera;
        [SerializeField] FlyCamera _flyCamera;
        [SerializeField] float _maxRayDistance = 2000f;

        [SerializeField, Tooltip("Always-on-top material for gizmos (Forge/GizmoOverlay).")]
        Material _gizmoMaterial;

        [SerializeField, Tooltip("See-through material for the place tool's preview (Forge/PlacementGhost).")]
        Material _ghostMaterial;

        [NonSerialized] EditorContext _context;
        [NonSerialized] IInputBlocker _ui;
        [NonSerialized] Dictionary<ToolKind, IEditorTool> _tools;
        [NonSerialized] ToolKind? _activeKind;

        /// <summary>One-shot actions, shared with the UI. Available after <see cref="Initialize"/>.</summary>
        public EditorCommands Commands { get; private set; }

        /// <param name="ui">Clicks over this UI are kept from the tools; typing into it is kept from the shortcuts.</param>
        /// <param name="assets">The world's asset catalog, so tool previews look like the real objects.</param>
        public void Initialize(EditorContext context, WorldView view, IInputBlocker ui, IAssetCatalog assets)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _ui = ui ?? throw new ArgumentNullException(nameof(ui));
            var picker = new ScenePicker(_maxRayDistance);
            var shapes = new AssetShapes(assets);
            var slopes = new SlopeAlignment(context, shapes);
            var seating = new ObjectSeating(picker.TerrainHeightAt, slopes.ShapeToAlign, shapes.ShapeOf);
            _flyCamera.Initialize(ui, ray => picker.TryPickWorld(ray, out var hit) ? hit.distance : (float?)null);
            Commands = new EditorCommands(context, view, picker, _flyCamera, slopes);
            _tools = new Dictionary<ToolKind, IEditorTool>
            {
                { ToolKind.Select, new SelectTransformTool(context, view, picker, _camera, _gizmoMaterial) },
                { ToolKind.Place, new PlaceTool(context, picker, new PlacementGhost(assets, shapes, _ghostMaterial), seating) },
                { ToolKind.Sculpt, new SculptTool(context, view, picker, slopes) },
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

            // Keys typed into a UI text field (palette search, slider inputs) are text, not commands.
            var typing = _ui.IsKeyboardCaptured;
            if (!typing)
                HandleShortcuts(keyboard);
            SwitchToolIfNeeded();

            // Right mouse belongs to the fly camera; tools see the left button only.
            var pointer = mouse.position.ReadValue();
            var overPanel = _ui.IsPointerOverUI(pointer);
            var looking = mouse.rightButton.isPressed;
            var input = new ToolInput(
                _camera.ScreenPointToRay(pointer),
                pointerOverUI: overPanel,
                primaryPressed: mouse.leftButton.wasPressedThisFrame && !overPanel && !looking,
                primaryHeld: mouse.leftButton.isPressed,
                primaryReleased: mouse.leftButton.wasReleasedThisFrame,
                additive: keyboard.shiftKey.isPressed,
                cancel: keyboard.escapeKey.wasPressedThisFrame && !typing,
                delete: keyboard.deleteKey.wasPressedThisFrame && !typing,
                snap: keyboard.ctrlKey.isPressed,
                deltaTime: Time.unscaledDeltaTime);

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

            // Hold right mouse = flying; the camera owns the keyboard then.
            if (Mouse.current.rightButton.isPressed)
                return;

            if (keyboard.bKey.wasPressedThisFrame)
                _context.ActiveTool = ToolKind.Sculpt;

            if (keyboard.wKey.wasPressedThisFrame)
                _context.SelectTransformMode(TransformMode.Move);
            else if (keyboard.eKey.wasPressedThisFrame)
                _context.SelectTransformMode(TransformMode.Rotate);
            else if (keyboard.rKey.wasPressedThisFrame)
                _context.SelectTransformMode(TransformMode.Scale);

            if (keyboard.endKey.wasPressedThisFrame)
                Commands.DropSelectionToGround();

            if (keyboard.fKey.wasPressedThisFrame)
                Commands.FrameSelection();

            // [ ] adjust the active tool: brush size while sculpting, rotation while placing.
            var bracket = (keyboard.rightBracketKey.wasPressedThisFrame ? 1 : 0) - (keyboard.leftBracketKey.wasPressedThisFrame ? 1 : 0);
            if (bracket != 0 && _context.ActiveTool == ToolKind.Sculpt)
                _context.BrushRadius *= Mathf.Pow(1.25f, bracket);
            else if (bracket != 0 && _context.ActiveTool == ToolKind.Place)
                _context.RotatePlacement(bracket * (keyboard.shiftKey.isPressed ? EditorContext.PlacementCoarseStep : EditorContext.PlacementStep));

            var palette = _context.Palette.Objects;
            for (var i = 0; i < Math.Min(9, palette.Count); i++)
            {
                if (keyboard[Key.Digit1 + i].wasPressedThisFrame)
                    _context.SelectPlacement(palette[i].Id);
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
