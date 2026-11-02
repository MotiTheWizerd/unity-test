using System;
using System.Collections.Generic;
using UnityEngine;

namespace Forge.UnityEditor.Content
{
    /// <summary>Assembles a placeholder prefab from primitive parts, each with its own material and collider.</summary>
    sealed class PrefabBuilder
    {
        readonly IReadOnlyDictionary<string, Material> _materials;

        public PrefabBuilder(GameObject root, IReadOnlyDictionary<string, Material> materials)
        {
            Root = root != null ? root : throw new ArgumentNullException(nameof(root));
            _materials = materials ?? throw new ArgumentNullException(nameof(materials));
        }

        public GameObject Root { get; }

        /// <param name="material">A name from <see cref="PlaceholderMaterials"/>.</param>
        /// <param name="euler">Rotation in degrees (Unity order: Z, then X, then Y).</param>
        public PrefabBuilder Part(PrimitiveType shape, string material, Vector3 position, Vector3 scale, Vector3 euler = default)
        {
            if (!_materials.TryGetValue(material, out var shared))
                throw new ArgumentException($"Unknown placeholder material '{material}'.", nameof(material));

            var part = GameObject.CreatePrimitive(shape);
            part.name = $"{shape} ({material})";
            part.transform.SetParent(Root.transform, false);
            part.transform.localPosition = position;
            part.transform.localRotation = Quaternion.Euler(euler);
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = shared;
            return this;
        }

        public PrefabBuilder Cube(string material, Vector3 position, Vector3 scale, Vector3 euler = default) =>
            Part(PrimitiveType.Cube, material, position, scale, euler);

        public PrefabBuilder Sphere(string material, Vector3 position, Vector3 scale, Vector3 euler = default) =>
            Part(PrimitiveType.Sphere, material, position, scale, euler);

        /// <summary>A cylinder <paramref name="height"/> meters tall standing on <paramref name="basePosition"/>.</summary>
        public PrefabBuilder Post(string material, Vector3 basePosition, float diameter, float height) =>
            Part(PrimitiveType.Cylinder, material, basePosition + Vector3.up * (height / 2f), new Vector3(diameter, height / 2f, diameter));
    }
}
