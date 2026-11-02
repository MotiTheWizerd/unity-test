using UnityEngine;

namespace Forge.EditorApp
{
    /// <summary>
    /// Tells input routing when the UI owns the input: clicks over the UI don't reach the
    /// world, and keys typed into a UI text field don't fire editor shortcuts.
    /// </summary>
    public interface IInputBlocker
    {
        /// <param name="screenPosition">Pointer position in screen pixels, origin bottom-left (as the Input System reports it).</param>
        bool IsPointerOverUI(Vector2 screenPosition);

        /// <summary>True while a UI text field has keyboard focus (e.g. the palette search).</summary>
        bool IsKeyboardCaptured { get; }
    }
}
