using UnityEngine;

namespace Forge.EditorApp
{
    /// <summary>The editor camera as commands and tools see it.</summary>
    public interface ICameraRig
    {
        /// <summary>Moves the camera, keeping its direction, so the sphere (center, radius) fills the view.</summary>
        void Frame(Vector3 center, float radius);

        /// <summary>Grabs the world under <paramref name="screenPoint"/> (pixels from the bottom left) for <see cref="Pan"/>.</summary>
        void BeginPan(Vector2 screenPoint);

        /// <summary>Moves the camera sideways so the grabbed point follows the pointer by <paramref name="screenDelta"/> pixels.</summary>
        void Pan(Vector2 screenDelta);
    }
}
