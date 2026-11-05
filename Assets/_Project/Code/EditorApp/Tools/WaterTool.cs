using System;
using System.Linq;
using Forge.Data;
using Forge.Data.Records;
using Forge.Editing.Water;
using UnityEngine;

namespace Forge.EditorApp.Tools
{
    /// <summary>
    /// Lakes, like a paint bucket: press in a hollow and it fills, almost to its rim; drag up or down to set the
    /// level (Ctrl: half meters), which never rises past the rim, so the water can't run off. Release places it as
    /// one undo step. Pressing an existing lake changes its level the same way. The lake last made or clicked is the
    /// current one: its level shows in the options bar, and Del removes it. Esc cancels a drag, or returns to Select.
    /// </summary>
    public sealed class WaterTool : IEditorTool
    {
        /// <summary>Pointer travel per meter of level: 50 pixels.</summary>
        const float MetersPerPixel = 0.02f;

        const float SnapStep = 0.5f;

        /// <summary>The shallowest a lake can be made, so it never vanishes under the drag.</summary>
        const float MinDepth = 0.05f;

        /// <summary>How near its point a lake that spills (and shows no water) can still be picked, in meters.</summary>
        const float PickSpilledWithin = 3f;

        readonly EditorContext _context;
        readonly ScenePicker _picker;
        readonly EditorCommands _commands;
        readonly WaterView _water;
        Gesture _gesture;

        public WaterTool(EditorContext context, ScenePicker picker, EditorCommands commands, WaterView water)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _picker = picker ?? throw new ArgumentNullException(nameof(picker));
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
            _water = water ?? throw new ArgumentNullException(nameof(water));
        }

        // The controller shows the tool's hint (ToolCatalog).
        public void Activate()
        {
        }

        public void Deactivate() => Cancel();

        public void Tick(in ToolInput input)
        {
            if (input.Cancel)
            {
                if (_gesture != null)
                {
                    Cancel();
                    _context.Report("Water cancelled");
                }
                else
                {
                    _context.SelectTool(ToolKind.Select);
                }

                return;
            }

            if (_gesture == null)
            {
                if (input.Delete)
                    _commands.RemoveCurrentLake();
                else if (input.PrimaryPressed && _picker.TryPickTerrain(input.Ray, out var hit))
                    Begin(Flat(hit.Point), input.Pointer.y);
                return;
            }

            if (input.PrimaryHeld)
                Drag(input.Pointer.y, input.Snap);
            if (input.PrimaryReleased || !input.PrimaryHeld)
                Commit();
        }

        void Begin(System.Numerics.Vector2 at, float pointerY)
        {
            var map = _water.Map;
            if (map == null)
                return;

            var lake = map.LakeAt(at)?.Record ?? SpilledLakeNear(map, at);
            var point = lake != null ? new System.Numerics.Vector2(lake.X, lake.Z) : at;
            if (!(LakeFill.Survey(map.Terrain, point) is LakeBasin basin))
                return;

            if (lake != null)
                _context.CurrentLake = lake.Id;
            if (!basin.HoldsWater)
            {
                _context.Report(lake != null
                    ? "This lake spills: its hollow no longer holds water. Rebuild the shore, or Del removes it."
                    : "Water: no hollow here, the water would run off. Dig one first (Sculpt, Lower).");
                return;
            }

            _gesture = new Gesture(point, basin, lake?.Id, lake?.Level ?? basin.FullLevel, pointerY);
            Show(_gesture.StartLevel);
        }

        // A lake whose shore was dug away shows no water, so it can't be clicked on; its point still can.
        static LakeRecord SpilledLakeNear(WaterMap map, System.Numerics.Vector2 at) =>
            map.Lakes
                .Where(lake => lake.Extent.Spills)
                .Select(lake => lake.Record)
                .Where(lake => System.Numerics.Vector2.Distance(new System.Numerics.Vector2(lake.X, lake.Z), at) <= PickSpilledWithin)
                .OrderBy(lake => System.Numerics.Vector2.Distance(new System.Numerics.Vector2(lake.X, lake.Z), at))
                .FirstOrDefault();

        void Drag(float pointerY, bool snap)
        {
            var level = _gesture.StartLevel + (pointerY - _gesture.PressY) * MetersPerPixel;
            if (snap)
                level = Mathf.Round(level / SnapStep) * SnapStep;
            level = Mathf.Clamp(level, _gesture.Basin.Ground + MinDepth, _gesture.Basin.Brim);
            if (Mathf.Abs(level - _gesture.Level) > 0.005f)
                Show(level);
        }

        // Previews the water at that level, live, as the tool would place it.
        void Show(float level)
        {
            var extent = LakeFill.Fill(_water.Map.Terrain, _gesture.Point, level);
            if (extent.Spills)
                return;

            _gesture.Level = level;
            _water.Preview(extent, level, _gesture.Lake);
            _context.Report(FormattableString.Invariant(
                $"Water: {level:0.##} m, up to {extent.Deepest:0.#} m deep (it can rise to {_gesture.Basin.Brim:0.##} m). Drag up or down; release to place."));
        }

        void Commit()
        {
            var gesture = _gesture;
            Cancel();
            if (float.IsNaN(gesture.Level))
                return;
            if (gesture.Lake is RecordId existing)
            {
                var changed = false;
                if (_context.TryEdit("Lake level", () => changed = WaterEdits.SetLakeLevel(_context.Session, existing, gesture.Level)) && changed)
                    _context.Report(_context.Session.UndoDescription);
                return;
            }

            LakeRecord created = null;
            if (_context.TryEdit("Add lake", () => created = WaterEdits.AddLake(_context.Session, _context.ActiveSpace, gesture.Point, gesture.Level)))
            {
                _context.CurrentLake = created.Id;
                _context.Report(_context.Session.UndoDescription);
            }
        }

        void Cancel()
        {
            if (_gesture == null)
                return;
            _gesture = null;
            _water.EndPreview();
        }

        static System.Numerics.Vector2 Flat(Vector3 point) => new System.Numerics.Vector2(point.x, point.z);

        sealed class Gesture
        {
            public Gesture(System.Numerics.Vector2 point, LakeBasin basin, RecordId? lake, float startLevel, float pressY)
            {
                Point = point;
                Basin = basin;
                Lake = lake;
                StartLevel = startLevel;
                PressY = pressY;
                Level = float.NaN;
            }

            /// <summary>Where the water fills from: the lake's own point, or where the user pressed.</summary>
            public System.Numerics.Vector2 Point { get; }

            public LakeBasin Basin { get; }

            /// <summary>The lake being changed, or null for a new one.</summary>
            public RecordId? Lake { get; }

            public float StartLevel { get; }

            public float PressY { get; }

            /// <summary>The level shown now, which release places.</summary>
            public float Level { get; set; }
        }
    }
}
