using System;
using System.Collections.Generic;
using System.Linq;
using Forge.Data.Records;
using Forge.Data.Terrain;
using UnityEngine;

namespace Forge.EditorApp.Agent
{
    /// <summary>
    /// What the agent is told about the editor with each message: where the camera is looking (the default
    /// building site), how far the terrain reaches, and what is selected.
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
            var lines = new List<string> { View(), Terrain(), Selection() };
            return string.Join("\n", lines.Where(line => line != null));
        }

        string View()
        {
            var ground = _groundInView();
            return ground.HasValue
                ? FormattableString.Invariant($"The camera is looking at the ground at x={ground.Value.x:0.#}, z={ground.Value.z:0.#} (height {ground.Value.y:0.#} m).")
                : "The camera is not looking at the ground.";
        }

        string Terrain()
        {
            var chunks = _context.Session.World.Records.OfType<TerrainChunkRecord>().Select(c => c.Coord).ToList();
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
