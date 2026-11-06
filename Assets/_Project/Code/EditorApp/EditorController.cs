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
    /// input: global shortcuts (each tool's key from <see cref="ToolCatalog"/>, Space held for the hand, Ctrl+S save,
    /// Ctrl+Z / Ctrl+Y or Ctrl+Shift+Z undo/redo, 1-9 palette, [ ] brush size or placement turn, End drop to ground,
    /// F frame selection) are handled here; everything else is passed to the current tool. Pointer clicks over the
    /// UI panel are filtered out, and so are shortcuts while a UI text field has the keyboard. While the agent's
    /// draft is open, the tools that change the world rest; select, hand and mark keep working.
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
        [NonSerialized] WorldFrame _frame;
        [NonSerialized] IInputBlocker _ui;
        [NonSerialized] Dictionary<ToolKind, IEditorTool> _tools;
        [NonSerialized] ToolKind? _activeKind;
        [NonSerialized] MarkView _markView;

        /// <summary>One-shot actions, shared with the UI. Available after <see cref="Initialize"/>.</summary>
        public EditorCommands Commands { get; private set; }

        /// <summary>The world-building agent, or null when the editor has no agent settings.</summary>
        public AgentHost Agent { get; private set; }

        /// <param name="frame">The world's frame in the scene: tools work in world coordinates, and this is where the camera's rays are converted.</param>
        /// <param name="ui">Clicks over this UI are kept from the tools; typing into it is kept from the shortcuts.</param>
        /// <param name="assets">The world's asset catalog, so tool previews look like the real objects.</param>
        /// <param name="agent">How the world-building agent runs; null leaves the editor without one.</param>
        /// <param name="water">The water on show, which the Water tool reads and previews through.</param>
        public void Initialize(EditorContext context, WorldFrame frame, WorldView view, WaterView water, IInputBlocker ui, IAssetCatalog assets, AgentSettings agent)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _frame = frame ?? throw new ArgumentNullException(nameof(frame));
            _ui = ui ?? throw new ArgumentNullException(nameof(ui));
            var picker = new ScenePicker(frame, _maxRayDistance);
            var shapes = new AssetShapes(assets);
            var slopes = new SlopeAlignment(context, shapes);
            var seating = new ObjectSeating(picker.TerrainHeightAt, slopes.ShapeToAlign, shapes.ShapeOf);
            // The camera's rays are the scene's; the distance along one is the same in either frame.
            _flyCamera.Initialize(ui, ray => picker.TryPickWorld(frame.ToWorld(ray), out var hit) ? hit.Distance : (float?)null, context.FlySpeed);
            var slots = new AssetSlots(assets);
            Commands = new EditorCommands(context, view, picker, _flyCamera, slopes, slots, PointerRay);
            var root = frame.Root;
            _tools = new Dictionary<ToolKind, IEditorTool>
            {
                { ToolKind.Select, new SelectTransformTool(ToolKind.Select, context, view, picker, _camera, _gizmoMaterial, seating, root) },
                { ToolKind.Move, new SelectTransformTool(ToolKind.Move, context, view, picker, _camera, _gizmoMaterial, seating, root) },
                { ToolKind.Rotate, new SelectTransformTool(ToolKind.Rotate, context, view, picker, _camera, _gizmoMaterial, seating, root) },
                { ToolKind.Scale, new SelectTransformTool(ToolKind.Scale, context, view, picker, _camera, _gizmoMaterial, seating, root) },
                { ToolKind.Hand, new HandTool(context, _flyCamera) },
                { ToolKind.Place, new PlaceTool(context, picker, new PlacementGhost(assets, shapes, _ghostMaterial, root), seating) },
                { ToolKind.Outline, new RectangleTool(RectPattern.Outline, context, picker, new PlacementGhost(assets, shapes, _ghostMaterial, root), seating) },
                { ToolKind.Fill, new RectangleTool(RectPattern.Fill, context, picker, new PlacementGhost(assets, shapes, _ghostMaterial, root), seating) },
                { ToolKind.Sculpt, new SculptTool(context, view, picker, slopes, root) },
                { ToolKind.Paint, new PaintTool(context, view, picker, root) },
                { ToolKind.Water, new WaterTool(context, picker, Commands, water) },
                { ToolKind.Mark, new MarkTool(context, picker) },
            };
            _markView = new MarkView(new TerrainSurface(picker), root);

            if (agent != null)
                Agent = new AgentHost(context, seating, slots, new EditorBriefing(context, () => GroundInView(picker)), agent);
        }

        /// <summary>The world ray through the mouse pointer now (where tools aim and dragged tiles land).</summary>
        Ray PointerRay() => WorldRay(Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero);

        // The camera's rays are the scene's; tools, commands and drops work in the world's.
        Ray WorldRay(Vector2 pointer) => _frame.ToWorld(_camera.ScreenPointToRay(pointer));

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
            HoldHandWithSpace(keyboard, mouse, typing);
            if (!typing)
                HandleShortcuts(keyboard, mouse);

            if (_context.IsDraftOpen && ToolCatalog.Of(_context.CurrentTool).EditsWorld)
            {
                RestWhileDraftIsOpen();
                return;
            }

            SwitchToolIfNeeded();

            // Right mouse belongs to the fly camera; tools see the left button only.
            var pointer = mouse.position.ReadValue();
            var overPanel = _ui.IsPointerOverUI(pointer);
            var looking = mouse.rightButton.isPressed;
            var input = new ToolInput(
                WorldRay(pointer),
                pointer,
                mouse.delta.ReadValue(),
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

        // Holding Space lends the hand to any tool, as in image editors. It only starts between drags (never in the
        // middle of one), and ends once Space and the button are both let go, so a pan in progress finishes.
        void HoldHandWithSpace(Keyboard keyboard, Mouse mouse, bool typing)
        {
            var holding = _context.HeldTool == ToolKind.Hand;
            if (!holding && !typing && keyboard.spaceKey.wasPressedThisFrame && !mouse.leftButton.isPressed)
                _context.HeldTool = ToolKind.Hand;
            else if (holding && !keyboard.spaceKey.isPressed && !mouse.leftButton.isPressed)
                _context.HeldTool = null;
        }

        void HandleShortcuts(Keyboard keyboard, Mouse mouse)
        {
            // Saving, undo and redo wait while the agent's build is open; the context says so.
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
            if (mouse.rightButton.isPressed)
                return;

            if (keyboard.fKey.wasPressedThisFrame)
                Commands.FrameSelection();

            // Tools that change the world refuse while the agent's build is open; the context says so.
            foreach (var tool in ToolCatalog.All)
            {
                if (keyboard[tool.Key].wasPressedThisFrame)
                    _context.SelectTool(tool.Kind);
            }

            // The rest edits the world, or the tools that do.
            if (_context.IsDraftOpen)
                return;

            if (keyboard.endKey.wasPressedThisFrame)
                Commands.DropSelectionToGround();

            // [ ] adjust the current tool: brush size while sculpting or painting, the turn of what the place and outline tools lay.
            var bracket = (keyboard.rightBracketKey.wasPressedThisFrame ? 1 : 0) - (keyboard.leftBracketKey.wasPressedThisFrame ? 1 : 0);
            if (bracket != 0 && ToolCatalog.Of(_context.CurrentTool).UsesBrush)
                _context.BrushRadius *= Mathf.Pow(1.25f, bracket);
            else if (bracket != 0 && ToolCatalog.Of(_context.CurrentTool).LaysPaletteObject)
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
            var current = _context.CurrentTool;
            if (_activeKind == current)
                return;

            if (_activeKind.HasValue)
                _tools[_activeKind.Value].Deactivate();
            _activeKind = current;
            var tool = ToolCatalog.Of(current);
            _context.Report($"{tool.Name}: {tool.Hint}");
            // A tool may say something more specific (e.g. which object it places).
            _tools[current].Activate();
        }

        // While the agent builds, or its build waits for review, nothing else edits the world: a tool that would
        // rests (its ghosts and gizmos go away) until the draft is accepted or discarded. The camera still flies,
        // F still frames the build, and the tools that edit nothing (select, hand, mark) still work.
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
            picker.TryPickTerrain(_frame.ToWorld(_camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f))), out var hit) ? hit.Point : (Vector3?)null;
    }
}
