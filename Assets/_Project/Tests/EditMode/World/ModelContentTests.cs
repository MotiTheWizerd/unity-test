using System.IO;
using System.Linq;
using Forge.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

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
    }
}
