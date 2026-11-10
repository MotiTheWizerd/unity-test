using System;
using UnityEngine;

namespace Forge.Play.Sound
{
    /// <summary>
    /// The ears the game is heard with: the scene's one AudioListener, turned as the camera looks, so what is on the
    /// screen's left is heard on the left. They are at the camera (flying about the editor), or, while the player plays, at
    /// its head (<see cref="HearFrom"/>): a third-person camera hanging back behind it then doesn't hear the player's own
    /// steps from behind, and a sound in the world is as near as it is to the player, not to the camera.
    /// </summary>
    public sealed class SoundListener : IDisposable
    {
        readonly Transform _ears;

        /// <param name="camera">The camera they turn with, and are at when no one plays.</param>
        public SoundListener(Transform camera)
        {
            if (camera == null)
                throw new ArgumentNullException(nameof(camera));
            _ears = new GameObject("Ears", typeof(AudioListener)).transform;
            _ears.SetParent(camera, false);
        }

        /// <summary>Where they are now (scene space).</summary>
        public Vector3 Position => _ears.position;

        /// <summary>Which way they face now: the camera's way.</summary>
        public Quaternion Rotation => _ears.rotation;

        /// <summary>
        /// They hear from <paramref name="head"/> (scene space), still turned the camera's way; null puts them back at the
        /// camera. Told every frame once the camera is posed, before the frame's sounds play, since they ride with the camera.
        /// </summary>
        public void HearFrom(Vector3? head)
        {
            if (head is Vector3 at)
                _ears.position = at;
            else
                _ears.localPosition = Vector3.zero;
        }

        public void Dispose()
        {
            if (_ears != null)
                UnityEngine.Object.DestroyImmediate(_ears.gameObject);
        }
    }
}
