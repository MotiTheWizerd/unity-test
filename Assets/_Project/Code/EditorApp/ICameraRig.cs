using UnityEngine;

namespace Forge.EditorApp
{
    /// <summary>The editor camera as commands see it.</summary>
    public interface ICameraRig
    {
        /// <summary>Moves the camera, keeping its direction, so the sphere (center, radius) fills the view.</summary>
        void Frame(Vector3 center, float radius);
    }
}
