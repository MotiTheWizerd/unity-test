using UnityEngine;

namespace Forge.World
{
    /// <summary>
    /// The ground of an interior: a large grid at height 0 that tools and placing treat like terrain, so the first
    /// floor tile of an empty interior has somewhere to stand. Shown only while an interior is open. Presentation
    /// only: it is not a record and never saved.
    /// </summary>
    public sealed class InteriorFloor : MonoBehaviour
    {
        const float Size = 512f;
        const int CellsPerTile = 8;
        const float CellSize = 1f;
        const int TexturePixels = 64;

        Material _material;
        Texture2D _texture;

        /// <summary>A hidden floor under <paramref name="parent"/>; call <see cref="Show"/> to bring it up.</summary>
        /// <param name="material">An unlit material (Materials/InteriorFloor) the floor copies and paints its grid into.</param>
        public static InteriorFloor Create(Transform parent, Material material)
        {
            if (material == null)
                throw new System.ArgumentNullException(nameof(material));

            var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
            go.name = "InteriorFloor";
            go.transform.SetParent(parent, false);
            // A Unity plane is 10 m across at scale 1.
            go.transform.localScale = new Vector3(Size / 10f, 1f, Size / 10f);
            go.SetActive(false);

            var floor = go.AddComponent<InteriorFloor>();
            floor.Paint(go.GetComponent<MeshRenderer>(), material);
            return floor;
        }

        public void Show(bool visible) => gameObject.SetActive(visible);

        // A grid of 1 m cells with a stronger line every 8 m, tiled across the floor.
        void Paint(MeshRenderer meshRenderer, Material template)
        {
            _texture = new Texture2D(TexturePixels, TexturePixels, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear };
            var fill = new Color(0.30f, 0.31f, 0.33f);
            var minor = new Color(0.40f, 0.41f, 0.44f);
            var major = new Color(0.55f, 0.56f, 0.60f);
            var pixelsPerCell = TexturePixels / CellsPerTile;
            for (var y = 0; y < TexturePixels; y++)
            for (var x = 0; x < TexturePixels; x++)
            {
                var onMajor = x == 0 || y == 0;
                var onMinor = x % pixelsPerCell == 0 || y % pixelsPerCell == 0;
                _texture.SetPixel(x, y, onMajor ? major : onMinor ? minor : fill);
            }

            _texture.Apply();
            // A copy of a material asset, not Shader.Find: a build only includes shaders that assets reference.
            _material = new Material(template) { name = "InteriorFloor (runtime)", mainTexture = _texture };
            _material.mainTextureScale = Vector2.one * (Size / (CellsPerTile * CellSize));
            meshRenderer.sharedMaterial = _material;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void OnDestroy()
        {
            if (_material != null)
                Destroy(_material);
            if (_texture != null)
                Destroy(_texture);
        }
    }
}
