using UnityEngine;

namespace Forge.World.Assets
{
    /// <summary>Pictures of assets for the UI, by the asset key records use.</summary>
    public interface IAssetThumbnails
    {
        /// <summary>The picture for <paramref name="assetKey"/>; never null.</summary>
        Texture2D Get(string assetKey);
    }
}
