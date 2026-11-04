using System;
using System.Collections.Generic;
using System.Linq;
using Forge.Data.Records;
using Forge.Data.Sky;
using Forge.Data.Terrain;
using UnityEngine;

namespace Forge.EditorApp.Agent
{
    /// <summary>
    /// What the agent is told about the editor with each message: the space being built in, the time of day, where the camera is
    /// looking, the spot or area the user marked (where "here" is), how far the terrain reaches, and what is selected.
    /// </summary>
    public sealed class EditorBriefing
    {
        const int MaxListedSelection = 12;

        readonly EditorContext _context;
        readonly Func<Vector3?> _groundInView;

        /// <param name="groundInView">The ground point at the middle of the view, or null when the camera sees no ground.</param>
        public EditorBriefing(EditorContext context, Func<Vector3?> groundInView)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _groundInView = groundInView ?? throw new ArgumentNullException(nameof(groundInView));
        }

        public string Describe()
        {
            var lines = new List<string> { Space(), Time(), View(), Mark(), Terrain(), Selection() };
            return string.Join("\n", lines.Where(line => line != null));
        }

        // The editor's time of day, so "at night" or "at sunset" in a request can be checked against what the user sees.
        string Time()
        {
            var hour = _context.Clock.Hour;
            var sun = SunPath.At(_context.DayCycle, hour);
            return $"It is {WorldClock.Format(hour)} in the editor, {(sun.IsUp ? "day: the sun is up" : "night: the sun is down")}.";
        }

        string View()
        {
            var ground = _groundInView();
            return ground.HasValue
                ? FormattableString.Invariant($"The camera is looking at the ground at x={ground.Value.x:0.#}, z={ground.Value.z:0.#} (height {ground.Value.y:0.#} m).")
                : "The camera is not looking at the ground.";
        }

        string Mark()
        {
            var mark = _context.Mark;
            return mark == null ? "The user has marked nothing." : $"The user marked {mark.Describe()}.";
        }

        string Space()
        {
            var space = _context.ActiveSpaceRecord;
            if (space == null)
                return "No space is open.";
            if (space.Kind == SpaceKind.Interior)
                return $"You are building inside {space.DisplayName}, an interior: there is no terrain, floors and walls are objects.";
            return space.WaterLevel is float sea
                ? FormattableString.Invariant($"You are building in {space.DisplayName}, a worldspace with a sea at {sea:0.##} m: ground below that is under water.")
                : $"You are building in {space.DisplayName}, a worldspace without a sea.";
        }

        string Terrain()
        {
            var space = _context.ActiveSpace;
            var chunks = _context.Session.World.Records.OfType<TerrainChunkRecord>().Where(c => c.Space == space).Select(c => c.Coord).ToList();
            if (chunks.Count == 0)
                return "There is no terrain.";

            var size = WorldGrid.ChunkSize;
            return FormattableString.Invariant(
                $"The terrain spans x {chunks.Min(c => c.X) * size:0} to {(chunks.Max(c => c.X) + 1) * size:0} m and z {chunks.Min(c => c.Z) * size:0} to {(chunks.Max(c => c.Z) + 1) * size:0} m.");
        }

        string Selection()
        {
            var records = _context.Session.World.Records;
            var selected = _context.Selection.Ids
                .Select(id => records.TryGet(id, out var record) ? record as PlacedObjectRecord : null)
                .Where(placed => placed != null)
                .ToList();
            if (selected.Count == 0)
                return "Nothing is selected.";

            var listed = selected.Take(MaxListedSelection).Select(placed => FormattableString.Invariant(
                $"{_context.DisplayNameOf(placed.BaseId)} {placed.Id} at ({placed.Position.X:0.#}, {placed.Position.Z:0.#})"));
            var more = selected.Count > MaxListedSelection ? $", and {selected.Count - MaxListedSelection} more" : string.Empty;
            return $"Selected ({selected.Count}): {string.Join("; ", listed)}{more}.";
        }
    }
}
