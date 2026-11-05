using System;
using System.Collections.Generic;
using System.Diagnostics;
using Forge.Data;
using Forge.Data.Terrain;
using Forge.Streaming;
using UnityEngine;

namespace Forge.World
{
    /// <summary>
    /// The land beyond the loaded chunks, drawn as coarse <see cref="FarTile"/> meshes in a window of tiles around the
    /// focus (a <see cref="ChunkWindow"/> over tile coordinates). Tiles cover the near land too, a little below it,
    /// so the real chunks draw over them and no seam shows where the two meet; the drop hides z-fighting. Tiles are
    /// built within a frame budget like chunks, and rebuilt when the terrain records under them change
    /// (<see cref="Invalidate"/>). Everything sits under the world root at world coordinates, so a rebase is free.
    /// </summary>
    public sealed class FarTerrainView : IDisposable
    {
        /// <summary>Chunks per tile edge: 4 chunks = 256 m tiles.</summary>
        public const int ChunksPerTile = 4;

        /// <summary>Meters between far samples: 8 m, so a tile is 33 × 33 vertices.</summary>
        public const float Step = 8f;

        /// <summary>How far below the real land the far land sits, so the two never z-fight.</summary>
        public const float Drop = 2f;

        readonly Transform _root;
        readonly Material _material;
        readonly ChunkWindow _window;
        // Tiles shown, by tile coordinate; null for a tile with no land (nothing to draw, but nothing to rebuild either).
        readonly Dictionary<ChunkCoord, GameObject> _tiles = new Dictionary<ChunkCoord, GameObject>();
        readonly HashSet<ChunkCoord> _dirty = new HashSet<ChunkCoord>();
        readonly Func<RecordId?, Color> _colorOf;
        Func<ChunkCoord, ChunkHeights> _terrainAt = coord => null;
        Func<ChunkCoord, ChunkPaint> _paintAt = coord => null;
        ChunkCoord? _lastFocus;

        /// <param name="radiusChunks">How far from the focus, in chunks, far land is shown; rounded up to whole tiles.</param>
        /// <param name="ground">How ground layers look from afar; null draws all far land in one neutral color.</param>
        public FarTerrainView(Transform root, Material material, int radiusChunks, GroundLooks ground = null)
        {
            _root = root != null ? root : throw new ArgumentNullException(nameof(root));
            _material = material != null ? material : throw new ArgumentNullException(nameof(material));
            _colorOf = ground != null ? ground.MapColorOf : (Func<RecordId?, Color>)(layer => Color.gray);
            if (radiusChunks < 0)
                throw new ArgumentOutOfRangeException(nameof(radiusChunks), radiusChunks, "Radius must not be negative.");

            var tiles = (radiusChunks + ChunksPerTile - 1) / ChunksPerTile;
            _window = new ChunkWindow(tiles, tiles + 1);
        }

        /// <summary>Far reach in meters: the edge of the far land.</summary>
        public float Reach => _window.LoadRadius * ChunksPerTile * WorldGrid.ChunkSize;

        /// <summary>
        /// Where the fog should hide everything: one tile short of the reach, so the outer ring of tiles (a staircase
        /// of 256 m steps against the sky from high up) fades out before it ends.
        /// </summary>
        public float FogReach => Math.Max(Reach - ChunksPerTile * WorldGrid.ChunkSize, ChunksPerTile * WorldGrid.ChunkSize);

        /// <summary>Tiles built and pending, in the stats' chunk terms.</summary>
        public StreamingStats Stats { get; } = new StreamingStats();

        public int TileCount => _tiles.Count;

        /// <summary>
        /// Shows the land <paramref name="terrainAt"/> answers for (null where a chunk has none), painted as
        /// <paramref name="paintAt"/> says (null where unpainted), instead of the current one.
        /// </summary>
        public void ShowSpace(Func<ChunkCoord, ChunkHeights> terrainAt, Func<ChunkCoord, ChunkPaint> paintAt)
        {
            _terrainAt = terrainAt ?? throw new ArgumentNullException(nameof(terrainAt));
            _paintAt = paintAt ?? throw new ArgumentNullException(nameof(paintAt));
            RemoveAll();
            _lastFocus = null;
        }

        /// <summary>Marks the tiles holding <paramref name="chunks"/> for a rebuild, if they are shown.</summary>
        public void Invalidate(IEnumerable<ChunkCoord> chunks)
        {
            if (chunks == null)
                throw new ArgumentNullException(nameof(chunks));

            foreach (var chunk in chunks)
            {
                var tile = FarTiles.TileOf(chunk, ChunksPerTile);
                if (_tiles.ContainsKey(tile))
                    _dirty.Add(tile);
                // A chunk on a tile's west or south edge is also the last column or row of the neighbour there.
                var west = new ChunkCoord(tile.X - 1, tile.Z);
                var south = new ChunkCoord(tile.X, tile.Z - 1);
                if (chunk.X == tile.X * ChunksPerTile && _tiles.ContainsKey(west))
                    _dirty.Add(west);
                if (chunk.Z == tile.Z * ChunksPerTile && _tiles.ContainsKey(south))
                    _dirty.Add(south);
            }
        }

        /// <summary>
        /// Streams tiles towards <paramref name="focus"/> (a chunk): drops the ones out of reach at once, then rebuilds
        /// dirty tiles and builds missing ones nearest first while they fit in <paramref name="budgetMs"/>; at least one
        /// when <paramref name="atLeastOne"/>, so progress never stalls. Returns how many tiles were built.
        /// </summary>
        public int Tick(ChunkCoord focus, double budgetMs, bool atLeastOne)
        {
            var tile = FarTiles.TileOf(focus, ChunksPerTile);
            if (tile == _lastFocus && _dirty.Count == 0 && Stats.PendingLoads == 0)
                return 0;
            _lastFocus = tile;

            var step = Stopwatch.StartNew();
            var plan = _window.Plan(tile, _tiles.Keys);
            foreach (var coord in plan.Unload)
                Remove(coord);
            Stats.RecordUnloads(plan.Unload.Count);

            var work = new List<ChunkCoord>(_dirty);
            work.AddRange(plan.Load);
            var built = 0;
            while (built < work.Count && ((built == 0 && atLeastOne) || step.Elapsed.TotalMilliseconds + Stats.AverageBuildMs <= budgetMs))
            {
                var started = step.Elapsed.TotalMilliseconds;
                Build(work[built]);
                Stats.RecordBuild(step.Elapsed.TotalMilliseconds - started);
                built++;
            }

            Stats.RecordStep(step.Elapsed.TotalMilliseconds, work.Count - built);
            return built;
        }

        public void Dispose() => RemoveAll();

        void Build(ChunkCoord coord)
        {
            Remove(coord);
            _dirty.Remove(coord);
            var tile = FarTiles.Sample(coord, ChunksPerTile, Step, _terrainAt, _paintAt);
            _tiles.Add(coord, tile.IsEmpty ? null : FarTerrainMesh.Build(tile, _root, _material, Drop, _colorOf));
        }

        void Remove(ChunkCoord coord)
        {
            if (_tiles.TryGetValue(coord, out var go) && go != null)
                FarTerrainMesh.Destroy(go);
            _tiles.Remove(coord);
            _dirty.Remove(coord);
        }

        void RemoveAll()
        {
            foreach (var go in _tiles.Values)
            {
                if (go != null)
                    FarTerrainMesh.Destroy(go);
            }

            _tiles.Clear();
            _dirty.Clear();
        }
    }
}
