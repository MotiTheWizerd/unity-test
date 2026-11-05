using System;
using Forge.Streaming;
using UnityEngine;

namespace Forge.World
{
    /// <summary>
    /// The world root as the world's frame in the scene. Everything that stands in the world (the land, the objects,
    /// the water, the gizmos drawn on the ground) is a child of <see cref="Root"/> and sits at its world coordinates,
    /// so the scene is rebased by moving the root alone: <see cref="Follow"/> keeps the <see cref="SceneOrigin"/> near
    /// the focus (the camera), and when it moves, <see cref="Shifted"/> tells what is not under the root (the camera)
    /// how far the scene moved. The two conversions are the only bridge between the scene's coordinates (rays from
    /// the camera, physics hits) and the world's (records, tools).
    /// </summary>
    public sealed class WorldFrame
    {
        readonly int _maxDistance;

        /// <param name="root">The transform the world is built under; it is moved and must not be rotated or scaled.</param>
        /// <param name="maxDistanceChunks">How many chunks the focus may stray from the origin before the scene is rebased.</param>
        public WorldFrame(Transform root, int maxDistanceChunks)
        {
            Root = root != null ? root : throw new ArgumentNullException(nameof(root));
            if (maxDistanceChunks < 0)
                throw new ArgumentOutOfRangeException(nameof(maxDistanceChunks), maxDistanceChunks, "The distance must not be negative.");
            _maxDistance = maxDistanceChunks;
            Root.position = Vector3.zero;
        }

        public Transform Root { get; }

        public SceneOrigin Origin { get; private set; } = SceneOrigin.Zero;

        /// <summary>Raised after a rebase with how far everything in the scene moved; the camera must move by the same.</summary>
        public event Action<Vector3> Shifted;

        public Vector3 ToScene(Vector3 world) => Origin.ToScene(world.ToNumerics()).ToUnity();

        public Vector3 ToWorld(Vector3 scene) => Origin.ToWorld(scene.ToNumerics()).ToUnity();

        public Ray ToScene(Ray world) => new Ray(ToScene(world.origin), world.direction);

        public Ray ToWorld(Ray scene) => new Ray(ToWorld(scene.origin), scene.direction);

        /// <summary>Rebases the scene if <paramref name="worldFocus"/> has strayed far enough from the origin. True when it did.</summary>
        public bool Follow(Vector3 worldFocus)
        {
            var next = Origin.Follow(worldFocus.ToNumerics(), _maxDistance);
            if (next == Origin)
                return false;

            // Scene = world − origin, so a new origin moves everything in the scene by the old origin minus the new.
            var delta = (Origin.Position - next.Position).ToUnity();
            Origin = next;
            Root.position = -next.Position.ToUnity();
            Shifted?.Invoke(delta);
            return true;
        }
    }
}
