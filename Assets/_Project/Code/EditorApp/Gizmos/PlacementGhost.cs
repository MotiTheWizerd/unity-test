using System;
using Forge.World;
using Forge.World.Assets;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Forge.EditorApp.Gizmos
{
    /// <summary>
    /// See-through copy of the object the place tool is about to create, built from the same
    /// asset catalog as the world, so the preview looks exactly like the result. It has no
    /// colliders, so picking looks straight through it, and it casts no shadows.
    /// </summary>
    public sealed class PlacementGhost : IDisposable
    {
        readonly IAssetCatalog _catalog;
        readonly Material _material;
        GameObject _instance;
        string _assetKey;

        /// <param name="material">Material for every part (Forge/PlacementGhost).</param>
        public PlacementGhost(IAssetCatalog catalog, Material material)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _material = material != null ? material : throw new ArgumentNullException(nameof(material));
        }

        /// <summary>How far the pivot sits above the shown object's lowest point, at scale 1 and no tilt.</summary>
        public float PivotAboveBottom { get; private set; }

        /// <summary>Makes the ghost show <paramref name="assetKey"/>; rebuilds only when the key changes. Stays hidden until <see cref="Place"/>.</summary>
        public void Use(string assetKey)
        {
            if (_instance != null && assetKey == _assetKey)
                return;

            DestroyInstance();
            _assetKey = assetKey;
            _instance = _catalog.Instantiate(assetKey, null);
            _instance.name = $"PlacementGhost ({assetKey})";
            _instance.SetActive(false);
            MakeGhostly(_instance);
            PivotAboveBottom = MeasurePivotAboveBottom(_instance);
        }

        public void Place(Vector3 position, Quaternion rotation)
        {
            if (_instance == null)
                return;
            _instance.transform.SetPositionAndRotation(position, rotation);
            _instance.SetActive(true);
        }

        public void Hide()
        {
            if (_instance != null)
                _instance.SetActive(false);
        }

        public void Dispose() => DestroyInstance();

        void MakeGhostly(GameObject go)
        {
            // Disabled at once (Destroy is deferred to the end of the frame), so this frame's picks already miss it.
            foreach (var collider in go.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
                Object.Destroy(collider);
            }

            foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
            {
                var materials = new Material[renderer.sharedMaterials.Length];
                for (var i = 0; i < materials.Length; i++)
                    materials[i] = _material;
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        // Bounds are only valid while active; measure at the origin with no rotation.
        static float MeasurePivotAboveBottom(GameObject go)
        {
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            go.SetActive(true);
            var offset = RendererBounds.PivotAboveBottom(go);
            go.SetActive(false);
            return offset;
        }

        void DestroyInstance()
        {
            if (_instance != null)
            {
                // Hidden at once: Destroy only happens at the end of the frame.
                _instance.SetActive(false);
                Object.Destroy(_instance);
            }

            _instance = null;
            _assetKey = null;
            PivotAboveBottom = 0f;
        }
    }
}
