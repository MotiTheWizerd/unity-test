using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Forge.Data;
using Forge.Data.Records;
using Forge.Data.Terrain;
using Forge.Streaming;
using UnityEngine;
using UnityEngine.Rendering;

namespace Forge.World
{
    /// <summary>
    /// The grass near the camera: each chunk within <see cref="Radius"/> grows its tufts from its paint
    /// (<see cref="GrassScatter"/>) into a GPU buffer, drawn every frame as one instanced draw of
    /// <see cref="GrassTuftMesh"/>, with no game objects and no colliders (picking and seating never hit grass).
    /// A chunk's grass is regrown when its terrain record or the sea changes; patches are built nearest first within a
    /// frame budget. Patches are placed through the world root's transform, so a rebase moves them for free.
    /// </summary>
    /// <remarks>
    /// The grass a ground layer grows is read when a patch is built; changing a layer's grass settings (no tool does)
    /// shows on patches built after, e.g. after switching space.
    /// </remarks>
    public sealed class GrassView : IDisposable
    {
        static readonly int TuftsId = Shader.PropertyToID("_Tufts");
        static readonly int ChunkToWorldId = Shader.PropertyToID("_ChunkToWorld");
        static readonly int FadeEndId = Shader.PropertyToID("_FadeEnd");

        readonly Transform _root;
        readonly Material _material;
        readonly Mesh _mesh;
        readonly Func<RecordId, Color> _colorOf;
        readonly Dictionary<ChunkCoord, Patch> _patches = new Dictionary<ChunkCoord, Patch>();
        readonly List<ChunkCoord> _wanted = new List<ChunkCoord>();
        IGrassSource _source;

        /// <param name="colorOf">The color (linear) of the grass a ground layer grows: its look's map color.</param>
        public GrassView(Transform root, Material material, Func<RecordId, Color> colorOf)
        {
            _root = root != null ? root : throw new ArgumentNullException(nameof(root));
            _material = material != null ? material : throw new ArgumentNullException(nameof(material));
            _colorOf = colorOf ?? throw new ArgumentNullException(nameof(colorOf));
            _mesh = GrassTuftMesh.Build();
        }

        /// <summary>How far from the camera grass shows, in meters: where the material's fade ends.</summary>
        public float Radius => _material.HasFloat(FadeEndId) ? _material.GetFloat(FadeEndId) : 60f;

        /// <summary>Patches built and pending, in the stats' chunk terms.</summary>
        public StreamingStats Stats { get; } = new StreamingStats();

        public int PatchCount => _patches.Count;

        /// <summary>Tufts in the patches shown.</summary>
        public int TuftCount
        {
            get
            {
                var count = 0;
                foreach (var patch in _patches.Values)
                    count += patch.Count;
                return count;
            }
        }

        /// <summary>Grows grass from <paramref name="source"/> from now on (a new space); every patch is regrown.</summary>
        public void Show(IGrassSource source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            RemoveAll();
        }

        /// <summary>
        /// Brings the patches around <paramref name="focus"/> (world coordinates) up to date: drops the ones out of
        /// reach, then grows new or changed ones nearest first while they fit in <paramref name="budgetMs"/> (at least
        /// one). Returns how many were grown.
        /// </summary>
        public int Tick(Vector3 focus, double budgetMs)
        {
            if (_source == null)
                return 0;

            var step = Stopwatch.StartNew();
            Wanted(focus);
            var unloads = 0;
            foreach (var coord in new List<ChunkCoord>(_patches.Keys))
            {
                if (!_wanted.Contains(coord))
                {
                    Remove(coord);
                    unloads++;
                }
            }

            Stats.RecordUnloads(unloads);

            var water = _source.WaterLevel;
            var grown = 0;
            var pending = 0;
            foreach (var coord in _wanted)
            {
                var chunk = _source.ChunkAt(coord);
                if (_patches.TryGetValue(coord, out var patch) && ReferenceEquals(patch.Chunk, chunk) && patch.Water == water)
                    continue;
                if (grown > 0 && step.Elapsed.TotalMilliseconds + Stats.AverageBuildMs > budgetMs)
                {
                    pending++;
                    continue;
                }

                var started = step.Elapsed.TotalMilliseconds;
                Grow(coord, chunk, water);
                Stats.RecordBuild(step.Elapsed.TotalMilliseconds - started);
                grown++;
            }

            Stats.RecordStep(step.Elapsed.TotalMilliseconds, pending);
            return grown;
        }

        /// <summary>Draws the patches for this frame's cameras (call once a frame, after <see cref="Tick"/>).</summary>
        public void Draw(Vector3 camera)
        {
            var radius = Radius;
            foreach (var pair in _patches)
            {
                var patch = pair.Value;
                if (patch.Count == 0)
                    continue;

                var chunkToWorld = _root.localToWorldMatrix * Matrix4x4.Translate(Origin(pair.Key));
                var bounds = patch.Bounds;
                var scene = new Bounds(chunkToWorld.MultiplyPoint3x4(bounds.center), bounds.size);
                if (scene.SqrDistance(camera) > radius * radius)
                    continue;

                patch.Properties.SetMatrix(ChunkToWorldId, chunkToWorld);
                var parameters = new RenderParams(_material)
                {
                    worldBounds = scene,
                    matProps = patch.Properties,
                    shadowCastingMode = ShadowCastingMode.Off,
                    receiveShadows = false,
                    layer = 0,
                };
                Graphics.RenderMeshPrimitives(parameters, _mesh, 0, patch.Count);
            }
        }

        public void Dispose()
        {
            RemoveAll();
            UnityEngine.Object.Destroy(_mesh);
        }

        // Chunks whose ground comes within the radius of the focus, nearest first.
        void Wanted(Vector3 focus)
        {
            _wanted.Clear();
            var radius = Radius;
            var min = WorldGrid.ChunkOf(focus.x - radius, focus.z - radius);
            var max = WorldGrid.ChunkOf(focus.x + radius, focus.z + radius);
            for (var z = min.Z; z <= max.Z; z++)
            for (var x = min.X; x <= max.X; x++)
            {
                var coord = new ChunkCoord(x, z);
                if (DistanceSq(coord, focus) <= radius * radius)
                    _wanted.Add(coord);
            }

            _wanted.Sort((a, b) => DistanceSq(a, focus).CompareTo(DistanceSq(b, focus)));
        }

        static float DistanceSq(ChunkCoord coord, Vector3 focus)
        {
            var origin = Origin(coord);
            var dx = Math.Max(Math.Max(origin.x - focus.x, focus.x - (origin.x + WorldGrid.ChunkSize)), 0f);
            var dz = Math.Max(Math.Max(origin.z - focus.z, focus.z - (origin.z + WorldGrid.ChunkSize)), 0f);
            return dx * dx + dz * dz;
        }

        static Vector3 Origin(ChunkCoord coord) => new Vector3(coord.X * WorldGrid.ChunkSize, 0f, coord.Z * WorldGrid.ChunkSize);

        // SHORTCUT: grown on the main thread (~10 ms for a 20k-tuft patch in the editor), so a new chunk can cost a
        // frame. Remove when hitches show while flying: grow on a worker thread (GrassScatter is engine-free), upload here.
        void Grow(ChunkCoord coord, TerrainChunkRecord chunk, float? water)
        {
            Remove(coord);
            var grass = chunk != null
                ? GrassScatter.Grow(chunk, _source.GrassOf, _source.UnpaintedGround, water)
                : GrassPatch.Empty;
            _patches.Add(coord, new Patch(chunk, water, grass, _colorOf));
        }

        void Remove(ChunkCoord coord)
        {
            if (_patches.TryGetValue(coord, out var patch))
                patch.Dispose();
            _patches.Remove(coord);
        }

        void RemoveAll()
        {
            foreach (var patch in _patches.Values)
                patch.Dispose();
            _patches.Clear();
        }

        /// <summary>One tuft as the shader reads it (<c>Grass.shader</c>'s <c>Tuft</c>): 32 bytes.</summary>
        [StructLayout(LayoutKind.Sequential)]
        struct Tuft
        {
            public Vector3 Position;
            public float Height;
            public Vector3 Color;
            public float Yaw;
        }

        /// <summary>A chunk's grass on the GPU, and what it was grown from.</summary>
        sealed class Patch : IDisposable
        {
            readonly GraphicsBuffer _buffer;

            public Patch(TerrainChunkRecord chunk, float? water, GrassPatch grass, Func<RecordId, Color> colorOf)
            {
                Chunk = chunk;
                Water = water;
                Count = grass.Tufts.Count;
                Properties = new MaterialPropertyBlock();
                if (Count == 0)
                    return;

                var colors = new Color[grass.Layers.Count];
                for (var i = 0; i < colors.Length; i++)
                    colors[i] = colorOf(grass.Layers[i]);

                var tufts = new Tuft[Count];
                var low = float.MaxValue;
                var high = float.MinValue;
                var tallest = 0f;
                for (var i = 0; i < Count; i++)
                {
                    var tuft = grass.Tufts[i];
                    // A little brightness of its own, so a meadow isn't one flat color.
                    var shade = 0.85f + 0.3f * Fraction(tuft.Yaw * 7.31f);
                    var color = colors[tuft.Layer] * shade;
                    tufts[i] = new Tuft
                    {
                        Position = new Vector3(tuft.X, tuft.Y, tuft.Z),
                        Height = tuft.Height,
                        Color = new Vector3(color.r, color.g, color.b),
                        Yaw = tuft.Yaw,
                    };
                    low = Math.Min(low, tuft.Y);
                    high = Math.Max(high, tuft.Y);
                    tallest = Math.Max(tallest, tuft.Height);
                }

                // Chunk-local; widened by the tallest tuft and its sway, so a tuft is never culled while it shows.
                var margin = tallest + 0.5f;
                var size = new Vector3(WorldGrid.ChunkSize + margin * 2f, high - low + margin * 2f, WorldGrid.ChunkSize + margin * 2f);
                Bounds = new Bounds(new Vector3(WorldGrid.ChunkSize * 0.5f, (low + high) * 0.5f, WorldGrid.ChunkSize * 0.5f), size);

                _buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Count, Marshal.SizeOf<Tuft>());
                _buffer.SetData(tufts);
                Properties.SetBuffer(TuftsId, _buffer);
            }

            public TerrainChunkRecord Chunk { get; }

            public float? Water { get; }

            public int Count { get; }

            public Bounds Bounds { get; }

            public MaterialPropertyBlock Properties { get; }

            public void Dispose() => _buffer?.Release();

            static float Fraction(float value) => value - (float)Math.Floor(value);
        }
    }

    /// <summary>What grass grows from in the shown space: its land, which layers grow what, and where the sea is.</summary>
    public interface IGrassSource
    {
        /// <summary>The terrain record of the chunk at <paramref name="coord"/>, or null where there is no land.</summary>
        TerrainChunkRecord ChunkAt(ChunkCoord coord);

        /// <summary>The grass ground layer <paramref name="layer"/> grows, or null for none.</summary>
        GrassSettings GrassOf(RecordId layer);

        /// <summary>The layer unpainted ground counts as; null grows nothing there.</summary>
        RecordId? UnpaintedGround { get; }

        /// <summary>The sea's surface; nothing grows below it. Null for no sea.</summary>
        float? WaterLevel { get; }
    }
}
