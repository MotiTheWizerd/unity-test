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
    public class MaterialCatalogTests
    {
        const string CatalogPath = "Assets/_Project/Content/MaterialCatalog.asset";
        const string CorePluginId = "forge.core";

        static string[] ShippedKeys()
        {
            var store = new PluginFileStore(Path.Combine(Application.streamingAssetsPath, "Plugins"), new JsonPluginSerializer(RecordTypeRegistry.CreateDefault()));
            return store.Load(CorePluginId).Records.OfType<MaterialRecord>().Select(r => r.AssetKey).ToArray();
        }

        // Content drift guard: every material the shipped base game defines has a look in the project's catalog, and the
        // catalog lists nothing unused.
        [Test]
        public void ShippedMaterials_AndProjectCatalog_Match()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MaterialCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null, $"Missing {CatalogPath}. Run Forge → Materials → Build Materials.");

            var keys = ShippedKeys();

            Assert.That(keys, Is.Not.Empty);
            Assert.That(keys.Where(key => !catalog.TryGet(key, out _)), Is.Empty, "Materials whose asset key has no material in the catalog.");
            Assert.That(catalog.Count, Is.EqualTo(keys.Length), "The catalog lists keys no shipped material uses.");
        }

        // The maps must be on as keywords, or a build would lack the shader variants and the scans would show flat.
        [Test]
        public void ShippedMaterials_HaveTheirMaps_On()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MaterialCatalog>(CatalogPath);
            foreach (var key in ShippedKeys())
            {
                Assert.That(catalog.TryGet(key, out var material), Is.True, key);
                Assert.That((material.GetTexture("_BaseMap") != null, material.GetTexture("_BumpMap") != null, material.GetTexture("_MetallicGlossMap") != null),
                    Is.EqualTo((true, true, true)), key);
                Assert.That(material.shaderKeywords, Is.SupersetOf(new[] { "_NORMALMAP", "_METALLICSPECGLOSSMAP", "_OCCLUSIONMAP" }), key);
            }
        }
    }
}
