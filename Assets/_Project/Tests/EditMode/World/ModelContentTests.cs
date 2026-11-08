using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Forge.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Forge.Tests.World
{
    // Content guard: every model under Content/Models is built (Forge → Models → Build Models) and keeps the asset
    // contract: its lowest point on the pivot, its footprint centred on it, colliding only through undrawn convex hulls.
    public class ModelContentTests
    {
        const string ModelsFolder = "Assets/_Project/Content/Models";

        [Test]
        public void EveryModel_IsBuilt_RestsOnItsPivot_AndCollidesThroughConvexHullsOnly()
        {
            var folders = AssetDatabase.IsValidFolder(ModelsFolder) ? AssetDatabase.GetSubFolders(ModelsFolder) : new string[0];
            Assert.That(folders, Is.Not.Empty);

            foreach (var folder in folders)
            {
                var name = Path.GetFileName(folder);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{folder}/{name}.prefab");
                Assert.That(prefab, Is.Not.Null, $"{folder} is not built.");

                var instance = Object.Instantiate(prefab);
                try
                {
                    Assert.That(RendererBounds.TryGet(instance, out var bounds), Is.True, $"{name} draws nothing.");
                    Assert.That(bounds.min.y, Is.EqualTo(0f).Within(1e-3f), $"{name}'s lowest point is on its pivot.");
                    Assert.That(new Vector2(bounds.center.x, bounds.center.z).magnitude, Is.LessThan(1e-3f), $"{name}'s footprint is centred on its pivot.");

                    var colliders = instance.GetComponentsInChildren<Collider>();
                    Assert.That(colliders, Is.Not.Empty, $"{name} has no colliders (meshes named …_COL).");
                    Assert.That(colliders.All(c => c is MeshCollider mesh && mesh.convex), Is.True, $"{name}'s colliders are convex hulls.");
                    Assert.That(colliders.All(c => c.GetComponent<Renderer>() == null), Is.True, $"{name}'s colliders are never drawn.");
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                }
            }
        }

        [Test]
        public void EachPart_DrawsWithItsOwnMaterial_ClippingOrBlendingItsAlpha_OnlyAsItsJsonSays()
        {
            foreach (var folder in AssetDatabase.GetSubFolders(ModelsFolder))
            {
                var name = Path.GetFileName(folder);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{folder}/{name}.prefab");
                Assert.That(prefab, Is.Not.Null, $"{folder} is not built.");
                var path = $"{folder}/{name}.json";
                var settings = File.Exists(path) ? JsonUtility.FromJson<Settings>(File.ReadAllText(path)) : new Settings();

                foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
                {
                    var part = PartOf(renderer.name, name);
                    var material = renderer.sharedMaterial;
                    Assert.That(material, Is.Not.Null, $"{renderer.name} has a material.");
                    Assert.That(material.name, Is.EqualTo(part == null ? name : $"{name}_{part}"), $"{renderer.name} draws with its part's material.");

                    var transparent = part != null && (settings.transparentParts ?? new string[0]).Contains(part);
                    var cutout = !transparent && (settings.cutout || (part != null && (settings.cutoutParts ?? new string[0]).Contains(part)));
                    Assert.That(material.IsKeywordEnabled("_ALPHATEST_ON"), Is.EqualTo(cutout), $"{material.name} clips its alpha only if cut out.");
                    Assert.That(material.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"), Is.EqualTo(transparent), $"{material.name} blends only if transparent.");
                    if (cutout)
                        Assert.That(material.GetFloat("_Cull"), Is.EqualTo((float)UnityEngine.Rendering.CullMode.Off), $"{material.name} draws both faces.");
                    if (transparent)
                        Assert.That(material.renderQueue, Is.GreaterThanOrEqualTo((int)UnityEngine.Rendering.RenderQueue.Transparent), $"{material.name} draws after the opaque world.");
                    if (!cutout && !transparent)
                        continue;
                    var color = material.GetTexture("_BaseMap");
                    Assert.That(color, Is.Not.Null, $"{material.name} has a base colour.");
                    Assert.That(GraphicsFormatUtility.HasAlphaChannel(color.graphicsFormat), Is.True, $"{material.name}'s base colour carries an alpha.");
                }
            }
        }

        // The part a model's renderer draws, as Build Models names them: "Ship_Hull_LOD1" → "Hull"; null for the model's own mesh.
        static string PartOf(string renderer, string model)
        {
            var stem = Regex.Replace(renderer, "_LOD[0-9]+$", "");
            return stem.StartsWith(model + "_") && stem.Length > model.Length + 1 ? stem.Substring(model.Length + 1) : null;
        }

        [System.Serializable]
        sealed class Settings
        {
            public bool cutout;
            public string[] cutoutParts;
            public string[] transparentParts;
        }
    }
}
