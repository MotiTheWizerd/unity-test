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
using Forge.Editing.Water;

namespace Forge.Editing.Building
{
    /// <summary>
    /// What the world-building commands work with: the session they edit, the space they build in (the one the
    /// user is viewing), how new objects are seated on the ground, and the user's placement options, shared with
    /// the place tool so both place alike.
    /// </summary>
    public sealed class BuildContext
    {
        readonly Func<RecordId> _space;
        readonly Func<BaseObjectRecord, IReadOnlyList<string>> _slotsOf;
        readonly Func<RecordId?> _unpaintedGround;
        ResolvedWorld _terrainWorld;
        TerrainGrid _terrain;
        WaterMap _water;

        /// <param name="space">The space the commands build in, read on every use (it follows the editor's view).</param>
        /// <param name="slotsOf">The material slots each kind of object has (they come from its asset); null: none known.</param>
        /// <param name="unpaintedGround">
        /// The ground layer unpainted ground shows (as the editor's paint tool knows it), read on every use; null, or
        /// null from it, when there is none, and then the ground can't be painted.
        /// </param>
        public BuildContext(EditSession session, Func<RecordId> space, ObjectSeating seating, PlacementOptions options,
            Func<BaseObjectRecord, IReadOnlyList<string>> slotsOf = null, Func<RecordId?> unpaintedGround = null)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            _space = space ?? throw new ArgumentNullException(nameof(space));
            Seating = seating ?? throw new ArgumentNullException(nameof(seating));
            Options = options ?? throw new ArgumentNullException(nameof(options));
            _slotsOf = slotsOf ?? (_ => Array.Empty<string>());
            _unpaintedGround = unpaintedGround ?? (() => null);
        }

        /// <summary>The ground layer unpainted ground shows, or null when the world has none.</summary>
        public RecordId? UnpaintedGround => _unpaintedGround();

        /// <summary>
        /// The ground layer with id or name <paramref name="idOrName"/> (names in any case, e.g. "dirt"); refused, with
        /// the names there are, when there is none.
        /// </summary>
        public GroundLayerRecord Ground(string idOrName)
        {
            var records = Session.World.Records;
            if (RecordId.TryParse(idOrName, out var recordId) && records.TryGet(recordId, out var record) && record is GroundLayerRecord byId)
                return byId;

            var layers = GroundLayers.Of(records);
            var byName = layers.FirstOrDefault(layer => string.Equals(layer.DisplayName, idOrName?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (byName != null)
                return byName;

            throw new CommandException(layers.Count == 0
                ? "This world has no grounds to paint with."
                : $"There is no ground '{idOrName}'. The grounds are: {string.Join(", ", layers.Select(layer => layer.DisplayName))}.");
        }

        /// <summary>The material slots <paramref name="baseObject"/> has, named after the materials it wears in them.</summary>
        public IReadOnlyList<string> SlotsOf(BaseObjectRecord baseObject) => _slotsOf(baseObject);

        /// <summary>The material with id <paramref name="id"/>; refused, with a hint, when there is none.</summary>
        public MaterialRecord Material(string id)
        {
            if (RecordId.TryParse(id, out var recordId)
                && Session.World.Records.TryGet(recordId, out var record)
                && record is MaterialRecord material)
                return material;

            throw new CommandException($"There is no material '{id}'. list_materials gives the ids of the materials.");
        }

        public EditSession Session { get; }

        /// <summary>The space the commands build in.</summary>
        public RecordId Space => _space();

        public ObjectSeating Seating { get; }

        public PlacementOptions Options { get; }

        /// <summary>The space's terrain as data (loaded in the scene or not); rebuilt only when the world or space changes.</summary>
        public TerrainGrid Terrain
        {
            get
            {
                Refresh();
                return _terrain;
            }
        }

        /// <summary>The space's water over that terrain (the sea), cached alongside it.</summary>
        public WaterMap Water
        {
            get
            {
                Refresh();
                return _water;
            }
        }

        /// <summary>The record of the space the commands build in, or null if it has none.</summary>
        public SpaceRecord SpaceRecord => Session.World.Records.TryGet(Space, out var record) ? record as SpaceRecord : null;

        void Refresh()
        {
            var space = Space;
            if (ReferenceEquals(_terrainWorld, Session.World) && _terrain.Space == space)
                return;

            _terrainWorld = Session.World;
            _terrain = new TerrainGrid(_terrainWorld.Records, space);
            _water = WaterMap.Of(_terrainWorld.Records, _terrain);
        }

        /// <summary>
        /// Keeps the objects standing on terrain that a command reshapes on the ground, re-tilting those that follow
        /// the slope, as the sculpt brush does.
        /// </summary>
        public TerrainFollower FollowTerrain() =>
            new TerrainFollower(Session.World.Records, Space, placed => Seating.SlopeShapeOf(placed.BaseId));

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

        /// <summary>The placed object <paramref name="id"/> in the space the commands build in, or null.</summary>
        public PlacedObjectRecord PlacedInSpace(string id) =>
            RecordId.TryParse(id, out var recordId)
            && Session.World.Records.TryGet(recordId, out var record)
            && record is PlacedObjectRecord placed
            && placed.Space == Space
                ? placed
                : null;

        /// <summary>The lake <paramref name="id"/> in the space the commands build in; refused, with a hint, when there is none.</summary>
        public LakeRecord LakeInSpace(string id)
        {
            if (RecordId.TryParse(id, out var recordId)
                && Session.World.Records.TryGet(recordId, out var record)
                && record is LakeRecord lake
                && lake.Space == Space)
                return lake;

            throw new CommandException($"There is no lake '{id}' here. inspect_area lists the lakes in an area.");
        }

        /// <summary>Placed objects in the space the commands build in. Reads every chunk of the space.</summary>
        public IEnumerable<PlacedObjectRecord> PlacedObjects()
        {
            var records = Session.World.Records;
            var space = Space;
            return records.ObjectChunks(space).SelectMany(coord => records.ObjectsIn(space, coord));
        }

        /// <summary>The display name of base object <paramref name="baseId"/>, or its id if it is missing.</summary>
        public string NameOf(RecordId baseId) =>
            Session.World.Records.TryGet(baseId, out var record) && record is BaseObjectRecord baseObject
                ? baseObject.DisplayName
                : baseId.ToString();

        /// <summary>The display name of ground layer <paramref name="layerId"/>, or its id if it is missing.</summary>
        public string NameOfGround(RecordId layerId) =>
            Session.World.Records.TryGet(layerId, out var record) && record is GroundLayerRecord layer
                ? layer.DisplayName
                : layerId.ToString();

        /// <summary>How many of each kind, most first, e.g. "8 Pine Tree, 4 Bush".</summary>
        public string CountByName(IEnumerable<PlacedObjectRecord> placed) =>
            string.Join(", ", placed
                .GroupBy(p => p.BaseId)
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key)
                .Select(g => $"{g.Count()} {NameOf(g.Key)}"));
    }
}
