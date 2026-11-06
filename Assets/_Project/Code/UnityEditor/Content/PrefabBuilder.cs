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
        /// <param name="solid">
        /// Whether the part keeps its primitive's collider. A primitive's collider follows the primitive, which is wrong for
        /// some shapes (a squashed cylinder gets a ball): leave those hollow and cover them with a <see cref="Box"/>.
        /// </param>
        public PrefabBuilder Part(PrimitiveType shape, string material, Vector3 position, Vector3 scale, Vector3 euler = default, bool solid = true)
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
            if (!solid)
                UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
            return this;
        }

        public PrefabBuilder Cube(string material, Vector3 position, Vector3 scale, Vector3 euler = default, bool solid = true) =>
            Part(PrimitiveType.Cube, material, position, scale, euler, solid);

        public PrefabBuilder Sphere(string material, Vector3 position, Vector3 scale, Vector3 euler = default, bool solid = true) =>
            Part(PrimitiveType.Sphere, material, position, scale, euler, solid);

        /// <summary>A cylinder <paramref name="height"/> meters tall standing on <paramref name="basePosition"/>.</summary>
        public PrefabBuilder Post(string material, Vector3 basePosition, float diameter, float height, bool solid = true) =>
            Part(PrimitiveType.Cylinder, material, basePosition + Vector3.up * (height / 2f), new Vector3(diameter, height / 2f, diameter), solid: solid);

        /// <summary>A solid box around <paramref name="center"/>, for parts left hollow (see <see cref="Part"/>).</summary>
        public PrefabBuilder Box(Vector3 center, Vector3 size)
        {
            var box = Root.AddComponent<BoxCollider>();
            box.center = center;
            box.size = size;
            return this;
        }
    }
}
