using System;
using Forge.World.Assets;
using UnityEngine;
using UnityEngine.UIElements;

namespace Forge.Play.Hud
{
    /// <summary>
    /// A <see cref="TurntablePreview"/> in the UI: the live picture fills the element at its own resolution, and a
    /// <see cref="TurnManipulator"/> lets a hand turn it every way and zoom it. Whoever hosts it says what to show and ticks
    /// it while it is on screen; the inventory's item now, the character later.
    /// </summary>
    public sealed class TurntableView : VisualElement, IDisposable
    {
        public const string UssClass = "forge-turntable";

        readonly TurntablePreview _preview;

        public TurntableView(IAssetCatalog assets)
        {
            _preview = new TurntablePreview(assets);
            AddToClassList(UssClass);
            RegisterCallback<GeometryChangedEvent>(_ => FitTexture());
            this.AddManipulator(new TurnManipulator(_preview.Turn));
        }

        /// <summary>Shows <paramref name="assetKey"/>'s asset; null shows nothing.</summary>
        public void Show(string assetKey) => _preview.Show(assetKey);

        /// <summary>Turns and draws the picture for <paramref name="deltaTime"/> seconds; call it each frame it is on screen.</summary>
        public void Tick(float deltaTime)
        {
            _preview.Tick(deltaTime);
            MarkDirtyRepaint();
        }

        public void Dispose() => _preview.Dispose();

        // The picture as many pixels as the element covers on screen.
        void FitTexture()
        {
            var scale = panel != null ? panel.scaledPixelsPerPoint : 1f;
            var width = Mathf.RoundToInt(resolvedStyle.width * scale);
            var height = Mathf.RoundToInt(resolvedStyle.height * scale);
            if (width <= 0 || height <= 0)
                return;
            _preview.Resize(width, height);
            style.backgroundImage = Background.FromRenderTexture(_preview.Texture);
        }
    }
}
