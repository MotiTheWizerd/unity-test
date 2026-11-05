using System;
using UnityEngine;

namespace Forge.World.Assets
{
    /// <summary>Pictures of assets for the UI, by the asset key records use.</summary>
    public interface IAssetThumbnails
    {
        /// <summary>The picture for <paramref name="assetKey"/>; never null.</summary>
        Texture2D Get(string assetKey);

        /// <summary>
        /// A material ball for <paramref name="key"/>: a sphere wearing what <paramref name="createMaterial"/> makes
        /// (called only when the picture isn't cached yet; the material is destroyed after). Never null.
        /// </summary>
        Texture2D GetBall(string key, Func<Material> createMaterial);
    }
}
