using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Forge.Data;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Editing.Commands;
using Forge.Editing.Placement;
using Forge.Editing.Terrain;

namespace Forge.Editing.Building
{
    /// <summary>
    /// What the world-building commands work with: the session they edit, how new objects are seated on the
    /// ground, and the user's placement options, shared with the place tool so both place alike.
    /// </summary>
    public sealed class BuildContext
    {
        ResolvedWorld _terrainWorld;
        TerrainGrid _terrain;

        public BuildContext(EditSession session, ObjectSeating seating, PlacementOptions options)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            Seating = seating ?? throw new ArgumentNullException(nameof(seating));
            Options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public EditSession Session { get; }

        public ObjectSeating Seating { get; }

        public PlacementOptions Options { get; }

        /// <summary>The terrain as data (loaded in the scene or not); rebuilt only when the world changes.</summary>
        public TerrainGrid Terrain
        {
            get
            {
                if (!ReferenceEquals(_terrainWorld, Session.World))
                {
                    _terrainWorld = Session.World;
                    _terrain = new TerrainGrid(_terrainWorld.Records);
                }

                return _terrain;
            }
        }

        /// <summary>
        /// Keeps the objects standing on terrain that a command reshapes on the ground, re-tilting those that follow
        /// the slope, as the sculpt brush does.
        /// </summary>
        public TerrainFollower FollowTerrain() =>
            new TerrainFollower(Session.World.Records, placed => Seating.SlopeShapeOf(placed.BaseId));

        /// <summary>Why nothing can be set down at <paramref name="at"/>: no terrain there, or terrain the scene hasn't loaded.</summary>
        public string WhyNoGround(Vector2 at) =>
            Terrain.HeightAt(at).HasValue
                ? FormattableString.Invariant($"the terrain at ({at.X:0.#}, {at.Y:0.#}) isn't loaded; build nearer the camera")
                : FormattableString.Invariant($"no terrain at ({at.X:0.#}, {at.Y:0.#})");

        /// <summary>The base object with id <paramref name="id"/>; refused, with a hint, when there is none.</summary>
        public BaseObjectRecord BaseObject(string id)
        {
            if (RecordId.TryParse(id, out var recordId)
                && Session.World.Records.TryGet(recordId, out var record)
                && record is BaseObjectRecord baseObject)
                return baseObject;

            throw new CommandException($"There is no object '{id}'. list_objects gives the ids that can be placed.");
        }

        /// <summary>The display name of base object <paramref name="baseId"/>, or its id if it is missing.</summary>
        public string NameOf(RecordId baseId) =>
            Session.World.Records.TryGet(baseId, out var record) && record is BaseObjectRecord baseObject
                ? baseObject.DisplayName
                : baseId.ToString();

        /// <summary>How many of each kind, most first, e.g. "8 Pine Tree, 4 Bush".</summary>
        public string CountByName(IEnumerable<PlacedObjectRecord> placed) =>
            string.Join(", ", placed
                .GroupBy(p => p.BaseId)
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key)
                .Select(g => $"{g.Count()} {NameOf(g.Key)}"));
    }
}
