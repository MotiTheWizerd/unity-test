using System;
using System.Collections.Generic;
using Forge.Editing.Placement;
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
        readonly AssetShapes _shapes;
        readonly Material _material;
        readonly Transform _worldRoot;
        readonly List<GameObject> _instances = new List<GameObject>();
        Transform _root;
        string _assetKey;

        /// <param name="material">Material for every part (Forge/PlacementGhost).</param>
        /// <param name="worldRoot">What the ghosts sit under, so the world coordinates they are shown at hold.</param>
        public PlacementGhost(IAssetCatalog catalog, AssetShapes shapes, Material material, Transform worldRoot)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _shapes = shapes ?? throw new ArgumentNullException(nameof(shapes));
            _material = material != null ? material : throw new ArgumentNullException(nameof(material));
            _worldRoot = worldRoot;
        }

        /// <summary>The shown object's footprint and pivot height, at scale 1 and no rotation.</summary>
        public PlacementShape Shape { get; private set; }

        /// <summary>Makes the ghosts show <paramref name="assetKey"/>; rebuilds only when the key changes. All stay hidden until <see cref="Show"/>.</summary>
        public void Use(string assetKey)
        {
            if (_instances.Count > 0 && assetKey == _assetKey)
                return;

            DestroyInstances();
            _assetKey = assetKey;
            Shape = _shapes.ShapeOf(assetKey);
            Instance(0);
        }

        /// <summary>Shows copy <paramref name="index"/> (created on demand) at the given pose, in world coordinates.</summary>
        public void Show(int index, Vector3 position, Quaternion rotation, float scale)
        {
            if (_assetKey == null)
                return;
            var instance = Instance(index);
            instance.transform.SetLocalPositionAndRotation(position, rotation);
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

        public void Dispose()
        {
            DestroyInstances();
            if (_root != null)
                Object.Destroy(_root.gameObject);
        }

        GameObject Instance(int index)
        {
            // One parent keeps a painted row's copies together instead of cluttering the world root.
            if (_root == null)
            {
                _root = new GameObject("PlacementGhosts").transform;
                _root.SetParent(_worldRoot, false);
            }

            while (_instances.Count <= index)
            {
                var instance = _catalog.Instantiate(_assetKey, _root);
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
            Shape = default;
        }
    }
}
