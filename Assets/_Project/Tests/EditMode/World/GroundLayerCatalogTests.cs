using System.IO;
using System.Linq;
using Forge.Data.Json;
using Forge.Data.Records;
using Forge.Data.Serialization;
using Forge.World.Assets;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Forge.Tests.World
{
    public class GroundLayerCatalogTests
    {
        const string CatalogPath = "Assets/_Project/Content/GroundLayerCatalog.asset";
        const string CorePluginId = "forge.core";

        // Content drift guard: every ground layer the shipped base game defines has a look in the project's catalog,
        // the catalog lists nothing unused, and its base layer (unpainted ground) is one of them.
        [Test]
        public void ShippedGroundLayers_AndProjectCatalog_Match()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<GroundLayerCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null, $"Missing {CatalogPath}.");

            var keys = ShippedKeys();

            Assert.That(keys, Is.Not.Empty);
            Assert.That(keys.Where(key => !catalog.TryGet(key, out _)), Is.Empty, "Ground layers whose asset key has no terrain layer in the catalog.");
            Assert.That(catalog.Count, Is.EqualTo(keys.Length), "The catalog lists keys no shipped ground layer uses.");
            Assert.That(keys, Has.Member(catalog.BaseKey));
            Assert.That(catalog.Base, Is.Not.Null);
        }

        static string[] ShippedKeys()
        {
            var store = new PluginFileStore(Path.Combine(Application.streamingAssetsPath, "Plugins"), new JsonPluginSerializer(RecordTypeRegistry.CreateDefault()));
            return store.Load(CorePluginId).Records.OfType<GroundLayerRecord>().Select(r => r.AssetKey).ToArray();
        }

        [Test]
        public void ShippedLayers_HaveTheirTextures()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<GroundLayerCatalog>(CatalogPath);
            foreach (var key in ShippedKeys())
            {
                Assert.That(catalog.TryGet(key, out var layer), Is.True, key);
                Assert.That((layer.diffuseTexture != null, layer.normalMapTexture != null, layer.maskMapTexture != null), Is.EqualTo((true, true, true)), key);
            }
        }

        // The material balls copy this template; its maps must be on, or a build would lack the shader variants.
        [Test]
        public void Catalog_HasAPreviewTemplate_WithTheLayerMapsOn()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<GroundLayerCatalog>(CatalogPath);
            var template = catalog.PreviewMaterial;

            Assert.That(template, Is.Not.Null, "Run Forge → Ground → Build Ground Layers.");
            Assert.That(template.shaderKeywords, Is.SupersetOf(new[] { "_NORMALMAP", "_METALLICSPECGLOSSMAP", "_OCCLUSIONMAP" }));

            catalog.TryGet("ground/rock", out var rock);
            var preview = TerrainLayerPreview.Create(rock, template);
            try
            {
                Assert.That(preview.GetTexture("_BaseMap"), Is.SameAs(rock.diffuseTexture));
                Assert.That(preview.GetTexture("_OcclusionMap"), Is.SameAs(rock.maskMapTexture));
            }
            finally
            {
                Object.DestroyImmediate(preview);
            }
        }
    }
}
