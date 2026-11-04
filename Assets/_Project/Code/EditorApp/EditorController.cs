using System;
using System.Collections.Generic;
using System.Linq;
using Forge.EditorApp.Agent;
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
    /// Builds the editing services the tools share (picking, shapes, seating, commands, the agent) and routes
    /// input: global shortcuts (Ctrl+S save, Ctrl+Z / Ctrl+Y or Ctrl+Shift+Z undo/redo, 1-9 palette, B sculpt,
    /// W/E/R move/rotate/scale, [ ] brush size, End drop to ground, F frame selection, M mark for the agent) are
    /// handled here; everything else is passed to the active tool. Pointer clicks over the UI panel are filtered
    /// out, and so are shortcuts while a UI text field has the keyboard. While the agent's draft is open, the
    /// tools rest, except the mark tool, which edits nothing.
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
        [NonSerialized] MarkView _markView;

        /// <summary>One-shot actions, shared with the UI. Available after <see cref="Initialize"/>.</summary>
        public EditorCommands Commands { get; private set; }

        /// <summary>The world-building agent, or null when the editor has no agent settings.</summary>
        public AgentHost Agent { get; private set; }

        /// <param name="ui">Clicks over this UI are kept from the tools; typing into it is kept from the shortcuts.</param>
        /// <param name="assets">The world's asset catalog, so tool previews look like the real objects.</param>
        /// <param name="agent">How the world-building agent runs; null leaves the editor without one.</param>
        public void Initialize(EditorContext context, WorldView view, IInputBlocker ui, IAssetCatalog assets, AgentSettings agent)
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
                { ToolKind.Mark, new MarkTool(context, picker) },
            };
            _markView = new MarkView(new TerrainSurface(picker));

            if (agent != null)
                Agent = new AgentHost(context, seating, new EditorBriefing(context, () => GroundInView(picker)), agent);
        }

        void OnDestroy()
        {
            Agent?.Dispose();
            _markView?.Dispose();
            if (_tools == null)
                return;
            foreach (var tool in _tools.Values.OfType<IDisposable>())
                tool.Dispose();
        }

        void Update()
        {
            // The agent's tool calls and replies land here, on the main thread, whatever the input.
            Agent?.Tick();

            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (_context == null || keyboard == null || mouse == null)
                return;

            _markView.Refresh(_context.Mark, _context.Session.World);

            // Keys typed into a UI text field (palette search, slider inputs) are text, not commands.
            var typing = _ui.IsKeyboardCaptured;
            if (!typing)
                HandleViewShortcuts(keyboard);

            if (_context.IsDraftOpen && _context.ActiveTool != ToolKind.Mark)
            {
                RestWhileDraftIsOpen();
                return;
            }

            if (!typing && !_context.IsDraftOpen)
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

        // Shortcuts that never edit the world, so they work while the agent's build is open too:
        // F frames the selection, M toggles the mark tool.
        void HandleViewShortcuts(Keyboard keyboard)
        {
            // Hold right mouse = flying; the camera owns the keyboard then.
            if (keyboard.ctrlKey.isPressed || Mouse.current.rightButton.isPressed)
                return;

            if (keyboard.fKey.wasPressedThisFrame)
                Commands.FrameSelection();

            if (keyboard.mKey.wasPressedThisFrame)
                _context.ActiveTool = _context.ActiveTool == ToolKind.Mark ? ToolKind.Select : ToolKind.Mark;
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

        // While the agent builds, or its build waits for review, nothing else edits the world: the active tool
        // rests (its ghosts and gizmos go away) until the draft is accepted or discarded. The camera still flies,
        // F still frames the build, and the mark tool (which edits nothing) still works.
        void RestWhileDraftIsOpen()
        {
            if (_activeKind.HasValue)
            {
                _tools[_activeKind.Value].Deactivate();
                _activeKind = null;
            }
        }

        // The terrain at the middle of the view: where "here" is when the user asks the agent to build.
        Vector3? GroundInView(ScenePicker picker) =>
            picker.TryPickTerrain(_camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f)), out var hit) ? hit.point : (Vector3?)null;
    }
}
