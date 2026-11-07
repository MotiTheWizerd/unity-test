using System;
using Forge.Data.Terrain;

namespace Forge.Editing.Terrain
{
    /// <summary>
    /// The paint tool's snow line for Auto: whether Auto lays snow, and the height it lies from. The height is kept while
    /// snow is off, so turning it back on finds the line where it was.
    /// </summary>
    public sealed class AutoSnowLine
    {
        /// <summary>The height a world starts with, a little over half the highest ground can be.</summary>
        public const float DefaultHeight = 110f;

        float _height = DefaultHeight;

        /// <summary>Whether Auto lays snow above <see cref="Height"/>.</summary>
        public bool On { get; set; }

        /// <summary>The height snow lies from, in meters; clamped to the heights land can have, a non-number ignored.</summary>
        public float Height
        {
            get => _height;
            set
            {
                if (!float.IsNaN(value))
                    _height = Math.Max(0f, Math.Min(WorldGrid.MaxTerrainHeight, value));
            }
        }

        /// <summary>The snow line for <see cref="AutoPaintRules.SnowLine"/>: the height while snow is on, else null.</summary>
        public float? Line => On ? _height : (float?)null;
    }
}
