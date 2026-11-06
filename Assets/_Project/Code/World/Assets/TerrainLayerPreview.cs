using UnityEngine;

namespace Forge.World.Assets
{
    /// <summary>
    /// A material that looks like a terrain layer, for previews (material balls) only: the land itself is drawn by the
    /// terrain shader. It is a copy of a template (URP Lit with its normal, metallic and occlusion maps switched on, a
    /// project asset so those shader variants ship in a build) wearing the layer's maps and tint. The terrain mask
    /// map packs metallic, occlusion, height and smoothness in R, G, B, A, just where Lit reads them (metallic map:
    /// R metallic, A smoothness; occlusion map: G), so one texture serves both slots.
    /// </summary>
    public static class TerrainLayerPreview
    {
        static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int BumpMap = Shader.PropertyToID("_BumpMap");
        static readonly int BumpScale = Shader.PropertyToID("_BumpScale");
        static readonly int MetallicGlossMap = Shader.PropertyToID("_MetallicGlossMap");
        static readonly int OcclusionMap = Shader.PropertyToID("_OcclusionMap");
        static readonly int Smoothness = Shader.PropertyToID("_Smoothness");

        /// <summary>A new material for <paramref name="layer"/> (the caller destroys it); null without a layer or a template.</summary>
        public static Material Create(TerrainLayer layer, Material template)
        {
            if (layer == null || template == null)
                return null;

            var material = new Material(template) { name = $"Preview {layer.name}" };
            material.SetTexture(BaseMap, layer.diffuseTexture);
            // The layer's tint (diffuse remap) colors the ball as it colors the land.
            material.SetColor(BaseColor, (Color)layer.diffuseRemapMax);
            material.SetTexture(BumpMap, layer.normalMapTexture);
            material.SetFloat(BumpScale, layer.normalScale);
            material.SetTexture(MetallicGlossMap, layer.maskMapTexture);
            material.SetTexture(OcclusionMap, layer.maskMapTexture);
            // Lit scales the map's smoothness by this, as the terrain does by the layer's mask remap.
            material.SetFloat(Smoothness, layer.maskMapRemapMax.w);
            return material;
        }
    }
}
