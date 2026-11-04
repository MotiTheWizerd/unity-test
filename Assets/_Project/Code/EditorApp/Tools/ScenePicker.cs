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

        /// <summary>
        /// Nearest ground hit, looking through any placed objects in front of it. The ground is the terrain, or in an
        /// interior its floor grid.
        /// </summary>
        public bool TryPickTerrain(Ray ray, out RaycastHit hit)
        {
            hit = default;
            var found = false;
            foreach (var candidate in Physics.RaycastAll(ray, _maxDistance, _worldMask))
            {
                if (IsGround(candidate.collider) && (!found || candidate.distance < hit.distance))
                {
                    hit = candidate;
                    found = true;
                }
            }

            return found;
        }

        static bool IsGround(Collider collider) => collider is TerrainCollider || collider.GetComponent<InteriorFloor>() != null;

        /// <summary>Ground height straight below (x, z), looking through objects; null where there is no ground.</summary>
        public float? TerrainHeightAt(float x, float z) =>
            TryPickTerrain(new Ray(new Vector3(x, ProbeTop, z), Vector3.down), out var hit) ? hit.point.y : (float?)null;

        /// <summary>Same as <see cref="TerrainHeightAt(float, float)"/> for a ground position (x, z), the form placement math uses.</summary>
        public float? TerrainHeightAt(System.Numerics.Vector2 ground) => TerrainHeightAt(ground.X, ground.Y);

        // Height vertical terrain probes start from; well above any terrain (heights are capped far lower).
        const float ProbeTop = 1000f;

        /// <summary>The placed object under the ray, if the first world hit is one.</summary>
        public bool TryPickPlacedObject(Ray ray, IRecordStore records, out RecordId id) =>
            TryPickPlacedObject(ray, records, out id, out _);

        /// <summary>The placed object under the ray, if the first world hit is one, and the point on it that was hit.</summary>
        public bool TryPickPlacedObject(Ray ray, IRecordStore records, out RecordId id, out Vector3 point)
        {
            id = default;
            point = default;
            if (!TryPickWorld(ray, out var hit))
                return false;

            var link = hit.collider.GetComponentInParent<RecordLink>();
            if (link == null || !records.TryGet(link.RecordId, out var record) || !(record is PlacedObjectRecord))
                return false;

            id = link.RecordId;
            point = hit.point;
            return true;
        }
    }
}
