using UnityEngine;

namespace Forge.EditorApp
{
    /// <summary>Tells input routing whether a screen point is over UI, so clicks there don't reach the world.</summary>
    public interface IPointerBlocker
    {
        /// <param name="screenPosition">Pointer position in screen pixels, origin bottom-left (as the Input System reports it).</param>
        bool IsPointerOverUI(Vector2 screenPosition);
    }
}
