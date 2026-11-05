using System;
using System.Collections.Generic;
using System.Linq;

namespace Forge.Data.Terrain
{
    /// <summary>
    /// A chunk's ground paint, wherever it is: in memory (a <see cref="GroundPaint"/>: new or edited) or stored in a
    /// plugin's terrain file and read the first time it is needed, as heights are (<see cref="ChunkHeights"/>).
    /// <see cref="Layers"/>, the ground layer records it is painted with, are known without loading.
    /// </summary>
    public abstract class ChunkPaint
    {
        /// <summary>
        /// Most layers one chunk is painted with. Painting a ninth lets go of the faintest (see
        /// <see cref="GroundPaint.FromWeights"/>); eight is far more than a chunk's 64 m usually shows.
        /// </summary>
        public const int MaxLayers = 8;

        protected ChunkPaint(IReadOnlyList<RecordId> layers)
        {
            if (layers == null)
                throw new ArgumentNullException(nameof(layers));
            if (layers.Count < 1 || layers.Count > MaxLayers)
                throw new ArgumentException($"A chunk is painted with 1 to {MaxLayers} ground layers, not {layers.Count}.", nameof(layers));
            if (layers.Any(layer => layer.IsDefault))
                throw new ArgumentException("A ground layer id is missing.", nameof(layers));
            if (layers.Distinct().Count() != layers.Count)
                throw new ArgumentException("A chunk names each ground layer once.", nameof(layers));

            Layers = layers.ToArray();
        }

        /// <summary>The ground layer records this chunk is painted with, in the order of their weights.</summary>
        public IReadOnlyList<RecordId> Layers { get; }

        /// <summary>
        /// The weights. Stored paint is read and decoded on first use and kept in a shared cache, so this may touch the
        /// disk: load once per use (a brush stroke, a chunk shown), not once per texel.
        /// </summary>
        /// <exception cref="DataValidationException">The terrain file is missing or damaged.</exception>
        public abstract GroundPaint Load();
    }
}
