using UnityEngine;

namespace Forge.World
{
    /// <summary>World-space bounds of everything an object draws, including its child parts.</summary>
    public static class RendererBounds
    {
        /// <returns>False if <paramref name="go"/> has no renderers.</returns>
        public static bool TryGet(GameObject go, out Bounds bounds)
        {
            bounds = default;
            var found = false;
            foreach (var renderer in go.GetComponentsInChildren<Renderer>())
            {
                if (found)
                {
                    bounds.Encapsulate(renderer.bounds);
                }
                else
                {
                    bounds = renderer.bounds;
                    found = true;
                }
            }

            return found;
        }

        /// <summary>
        /// How far the pivot sits above the object's lowest drawn point (0 without renderers).
        /// Placing the pivot this far above a surface rests the object on it.
        /// </summary>
        public static float PivotAboveBottom(GameObject go) =>
            TryGet(go, out var bounds) ? go.transform.position.y - bounds.min.y : 0f;
    }
}
