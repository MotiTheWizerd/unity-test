using Forge.Data;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.EditorApp.Gizmos;
using Forge.World;
using UnityEngine;

namespace Forge.EditorApp.Tools
{
    /// <summary>
    /// Raycasting shared by all tools. Gizmo handles live on their own layer so
    /// world picks never hit them and handle picks never hit the world.
    /// </summary>
    public sealed class ScenePicker
    {
        public const string GizmoLayerName = "ForgeGizmo";

        readonly float _maxDistance;
        readonly int _gizmoMask;
        readonly int _worldMask;

        public ScenePicker(float maxDistance)
        {
            GizmoLayer = LayerMask.NameToLayer(GizmoLayerName);
            if (GizmoLayer < 0)
                Debug.LogError($"[Forge] Layer '{GizmoLayerName}' is missing from the project's tag manager; gizmos will not be pickable.");

            _maxDistance = maxDistance;
            _gizmoMask = GizmoLayer >= 0 ? 1 << GizmoLayer : 0;
            _worldMask = Physics.DefaultRaycastLayers & ~_gizmoMask;
        }

        public int GizmoLayer { get; }

        public bool TryPickHandle(Ray ray, out GizmoHandle handle)
        {
            handle = default;
            if (_gizmoMask == 0 || !Physics.Raycast(ray, out var hit, _maxDistance, _gizmoMask))
                return false;

            var part = hit.collider.GetComponent<GizmoHandlePart>();
            if (part == null)
                return false;

            handle = part.Handle;
            return true;
        }

        public bool TryPickWorld(Ray ray, out RaycastHit hit) =>
            Physics.Raycast(ray, out hit, _maxDistance, _worldMask);

        /// <summary>Nearest terrain hit, looking through any placed objects in front of it.</summary>
        public bool TryPickTerrain(Ray ray, out RaycastHit hit)
        {
            hit = default;
            var found = false;
            foreach (var candidate in Physics.RaycastAll(ray, _maxDistance, _worldMask))
            {
                if (candidate.collider is TerrainCollider && (!found || candidate.distance < hit.distance))
                {
                    hit = candidate;
                    found = true;
                }
            }

            return found;
        }

        /// <summary>The placed object under the ray, if the first world hit is one.</summary>
        public bool TryPickPlacedObject(Ray ray, IRecordStore records, out RecordId id)
        {
            id = default;
            if (!TryPickWorld(ray, out var hit))
                return false;

            var link = hit.collider.GetComponentInParent<RecordLink>();
            if (link == null || !records.TryGet(link.RecordId, out var record) || !(record is PlacedObjectRecord))
                return false;

            id = link.RecordId;
            return true;
        }
    }
}
