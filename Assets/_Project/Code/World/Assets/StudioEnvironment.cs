using UnityEngine;

namespace Forge.World.Assets
{
    /// <summary>
    /// The directions a cube map's texels look along, as the GPU reads Unity's cube maps (each face's first row is its
    /// top): what <see cref="PreviewStudio"/> paints its surroundings by. Pure math, so it is testable without rendering.
    /// </summary>
    public static class StudioEnvironment
    {
        /// <summary>The direction texel (<paramref name="column"/>, <paramref name="row"/>) of a face <paramref name="size"/> texels wide looks along.</summary>
        public static Vector3 Direction(CubemapFace face, int column, int row, int size)
        {
            // Across the face left to right, and down it top to bottom, from -1 to 1 through the texels' centres.
            var s = 2f * (column + 0.5f) / size - 1f;
            var t = 2f * (row + 0.5f) / size - 1f;
            Vector3 direction;
            switch (face)
            {
                case CubemapFace.PositiveX: direction = new Vector3(1f, -t, -s); break;
                case CubemapFace.NegativeX: direction = new Vector3(-1f, -t, s); break;
                case CubemapFace.PositiveY: direction = new Vector3(s, 1f, t); break;
                case CubemapFace.NegativeY: direction = new Vector3(s, -1f, -t); break;
                case CubemapFace.PositiveZ: direction = new Vector3(s, -t, 1f); break;
                default: direction = new Vector3(-s, -t, -1f); break;
            }

            return direction.normalized;
        }

        /// <summary>How far up (1) or down (-1) that texel looks.</summary>
        public static float Up(CubemapFace face, int column, int row, int size) => Direction(face, column, row, size).y;
    }
}
