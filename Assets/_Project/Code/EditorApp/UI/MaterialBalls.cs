using System;
using Forge.Data.Records;
using Forge.World;
using Forge.World.Assets;
using UnityEngine;

namespace Forge.EditorApp.UI
{
    /// <summary>
    /// The pictures of materials: a lit ball wearing each (<see cref="IAssetThumbnails.GetBall"/> with
    /// <see cref="MaterialLooks.CreatePreviewMaterial"/>), shared by every list that shows materials so one look is
    /// rendered once. Without thumbnails or looks there are no pictures and lists keep their placeholder icons.
    /// </summary>
    public sealed class MaterialBalls
    {
        readonly IAssetThumbnails _thumbnails;
        readonly MaterialLooks _looks;

        public MaterialBalls(IAssetThumbnails thumbnails, MaterialLooks looks)
        {
            _thumbnails = thumbnails;
            _looks = looks;
        }

        /// <summary>The ball of <paramref name="material"/>, or null when there are no pictures.</summary>
        public Texture2D Of(MaterialRecord material)
        {
            if (material == null)
                throw new ArgumentNullException(nameof(material));
            if (_thumbnails == null || _looks == null)
                return null;
            // Ball pictures are keyed by look, so a material and a ground of one key can't share one.
            var id = material.Id;
            return _thumbnails.GetBall(material.AssetKey, () => _looks.CreatePreviewMaterial(id));
        }
    }
}
