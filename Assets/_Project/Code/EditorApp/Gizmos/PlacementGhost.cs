using System;
using System.Collections.Generic;
using Forge.Editing.Placement;
using Forge.World;
using Forge.World.Assets;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Forge.EditorApp.Gizmos
{
    /// <summary>
    /// See-through copies of the object the place tool is about to create (one, or a whole row while
    /// painting), built from the same asset catalog as the world, so the preview looks exactly like the
    /// result. They have no colliders, so picking looks straight through them, and cast no shadows.
    /// </summary>
    public sealed class PlacementGhost : IDisposable
    {
        readonly IAssetCatalog _catalog;
        readonly Material _material;
        readonly List<GameObject> _instances = new List<GameObject>();
        string _assetKey;

        /// <param name="material">Material for every part (Forge/PlacementGhost).</param>
        public PlacementGhost(IAssetCatalog catalog, Material material)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _material = material != null ? material : throw new ArgumentNullException(nameof(material));
        }

        /// <summary>How far the pivot sits above the shown object's lowest point, at scale 1 and no tilt.</summary>
        public float PivotAboveBottom { get; private set; }

        /// <summary>The shown object's ground footprint at scale 1 and no rotation.</summary>
        public Footprint Footprint { get; private set; }

        /// <summary>Makes the ghosts show <paramref name="assetKey"/>; rebuilds only when the key changes. All stay hidden until <see cref="Show"/>.</summary>
        public void Use(string assetKey)
        {
            if (_instances.Count > 0 && assetKey == _assetKey)
                return;

            DestroyInstances();
            _assetKey = assetKey;
            Measure(Instance(0));
        }

        /// <summary>Shows copy <paramref name="index"/> (created on demand) at the given pose.</summary>
        public void Show(int index, Vector3 position, Quaternion rotation, float scale)
        {
            if (_assetKey == null)
                return;
            var instance = Instance(index);
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.transform.localScale = Vector3.one * scale;
            instance.SetActive(true);
        }

        /// <summary>Hides copies from <paramref name="index"/> on (they are kept for reuse).</summary>
        public void HideFrom(int index)
        {
            for (var i = index; i < _instances.Count; i++)
                _instances[i].SetActive(false);
        }

        public void Hide() => HideFrom(0);

        public void Dispose() => DestroyInstances();

        GameObject Instance(int index)
        {
            while (_instances.Count <= index)
            {
                var instance = _catalog.Instantiate(_assetKey, null);
                instance.name = $"PlacementGhost ({_assetKey})";
                instance.SetActive(false);
                MakeGhostly(instance);
                _instances.Add(instance);
            }

            return _instances[index];
        }

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

        // Bounds are only valid while active; measure at the origin with no rotation, at scale 1.
        void Measure(GameObject go)
        {
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            go.transform.localScale = Vector3.one;
            go.SetActive(true);
            PivotAboveBottom = RendererBounds.PivotAboveBottom(go);
            Footprint = RendererBounds.TryGet(go, out var bounds) ? new Footprint(bounds.size.x, bounds.size.z) : default;
            go.SetActive(false);
        }

        void DestroyInstances()
        {
            foreach (var instance in _instances)
            {
                // Hidden at once: Destroy only happens at the end of the frame.
                instance.SetActive(false);
                Object.Destroy(instance);
            }

            _instances.Clear();
            _assetKey = null;
            PivotAboveBottom = 0f;
            Footprint = default;
        }
    }
}
