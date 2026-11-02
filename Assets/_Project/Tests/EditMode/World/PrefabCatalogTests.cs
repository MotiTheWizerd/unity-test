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
    public class PrefabCatalogTests
    {
        const string CatalogPath = "Assets/_Project/Content/PrefabCatalog.asset";
        const string CorePluginId = "forge.core";

        PrefabCatalog _catalog;
        GameObject _prefabA;
        GameObject _prefabB;

        [SetUp]
        public void SetUp()
        {
            _catalog = ScriptableObject.CreateInstance<PrefabCatalog>();
            _prefabA = new GameObject("a");
            _prefabB = new GameObject("b");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_catalog);
            Object.DestroyImmediate(_prefabA);
            Object.DestroyImmediate(_prefabB);
        }

        [Test]
        public void Set_AddsAndReplacesByKey()
        {
            _catalog.Set("props/barrel", _prefabA);
            _catalog.Set("props/barrel", _prefabB);

            Assert.That(_catalog.Count, Is.EqualTo(1));
            Assert.That(_catalog.TryGet("props/barrel", out var prefab), Is.True);
            Assert.That(prefab, Is.SameAs(_prefabB));
        }

        [Test]
        public void TryGet_UnknownOrNullKey_IsFalse()
        {
            _catalog.Set("props/barrel", _prefabA);

            Assert.That(_catalog.TryGet("props/crate", out _), Is.False);
            Assert.That(_catalog.TryGet(null, out _), Is.False);
        }

        [Test]
        public void TryGet_EntryWithoutPrefab_IsFalse()
        {
            _catalog.Set("props/barrel", null);

            Assert.That(_catalog.TryGet("props/barrel", out _), Is.False);
        }

        // Content drift guard: every base object the shipped base game defines must be presentable,
        // either by a primitive or by a prefab in the project's catalog, and the catalog lists nothing unused.
        [Test]
        public void ShippedBaseObjects_AndProjectCatalog_Match()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<PrefabCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null, $"Missing {CatalogPath}.");

            var store = new PluginFileStore(Path.Combine(Application.streamingAssetsPath, "Plugins"), new JsonPluginSerializer(RecordTypeRegistry.CreateDefault()));
            var keys = store.Load(CorePluginId).Records.OfType<BaseObjectRecord>()
                .Select(r => r.AssetKey)
                .Where(key => !key.StartsWith(PrimitiveAssetCatalog.Prefix))
                .ToArray();

            var missing = keys.Where(key => !catalog.TryGet(key, out _)).ToArray();
            Assert.That(missing, Is.Empty, "Base objects whose asset key has no prefab in the catalog.");
            Assert.That(catalog.Count, Is.EqualTo(keys.Length), "The catalog lists keys no shipped base object uses.");
        }
    }
}
