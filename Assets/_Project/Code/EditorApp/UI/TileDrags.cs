using System;
using Forge.Data;
using Forge.EditorApp.UI.Controls;

namespace Forge.EditorApp.UI
{
    /// <summary>
    /// Dragging a tile into the world (<see cref="TileDrag"/>), wired to the editor: while it is carried
    /// <see cref="EditorContext.Drag"/> says what, so the scene shows where it would land.
    /// </summary>
    public static class TileDrags
    {
        /// <param name="drop">What letting go over the world does.</param>
        public static TileDrag Of(EditorContext context, AssetDragKind kind, RecordId id, Action drop)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            return new TileDrag(() => context.Drag = new AssetDrag(kind, id), drop, () => context.Drag = null);
        }
    }
}
