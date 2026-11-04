using System;
using Forge.EditorApp.Tools;
using UnityEngine;

namespace Forge.EditorApp.Gizmos
{
    /// <summary>Drops ground positions onto the terrain, so gizmo lines drawn on the ground hug the hills.</summary>
    public sealed class TerrainSurface
    {
        /// <summary>How far above the surface ground lines float, so the terrain doesn't hide them.</summary>
        public const float Lift = 0.2f;

        readonly ScenePicker _picker;

        public TerrainSurface(ScenePicker picker)
        {
            _picker = picker ?? throw new ArgumentNullException(nameof(picker));
        }

        /// <summary>The terrain under <paramref name="flat"/> (its y is ignored), lifted by <see cref="Lift"/>; <paramref name="flat"/> itself off the terrain.</summary>
        public Vector3 Drop(Vector3 flat)
        {
            var height = _picker.TerrainHeightAt(flat.x, flat.z);
            return new Vector3(flat.x, (height ?? flat.y) + Lift, flat.z);
        }
    }
}
