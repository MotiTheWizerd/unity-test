using System.IO;
using System.Linq;
using Forge.Data.Json;
using Forge.Data.Records;
using Forge.Data.Serialization;
using Forge.World;
using Forge.World.Assets;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Forge.Tests.World
{
    // Content guard: every character the base game places in the world (an NPC: a base object whose asset key is
    // "characters/…", listed by Forge → Characters → Build Characters) is a built humanoid of a person's height that
    // idles in a move set, so it can be placed as a model is.
    public class CharacterContentTests
    {
        const string CatalogPath = "Assets/_Project/Content/PrefabCatalog.asset";
        const string CorePluginId = "forge.core";

        [Test]
        public void EveryPlacedCharacter_IsABuiltHumanoid_OfAPersonsHeight_WithItsMoveSet()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<PrefabCatalog>(CatalogPath);
            var store = new PluginFileStore(Path.Combine(Application.streamingAssetsPath, "Plugins"), new JsonPluginSerializer(RecordTypeRegistry.CreateDefault()));
            var npcs = store.Load(CorePluginId).Records.OfType<BaseObjectRecord>()
                .Where(r => r.AssetKey.StartsWith("characters/"))
                .ToArray();
            Assert.That(npcs, Is.Not.Empty, "The base game places no character.");

            foreach (var npc in npcs)
            {
                Assert.That(catalog.TryGet(npc.AssetKey, out var prefab), Is.True, $"{npc.AssetKey} is not in the catalog.");
                var animator = prefab.GetComponentInChildren<Animator>();
                Assert.That(animator, Is.Not.Null, $"{npc.AssetKey} has no animator.");
                Assert.That(animator.avatar != null && animator.avatar.isHuman && animator.avatar.isValid, Is.True, $"{npc.AssetKey} is not a humanoid.");
                Assert.That(animator.runtimeAnimatorController, Is.Not.Null, $"{npc.AssetKey} plays no move set.");

                // Its height, not its feet: a humanoid stands where its move set's idle puts it (its rest pose, the
                // bind pose, can reach below the pivot; the hero's does by 12 cm), and edit mode doesn't play the idle.
                var instance = Object.Instantiate(prefab);
                try
                {
                    Assert.That(RendererBounds.TryGetPosed(instance, out var bounds), Is.True, $"{npc.AssetKey} draws nothing.");
                    Assert.That(bounds.max.y, Is.InRange(1.4f, 2.2f), $"{npc.AssetKey} is a person's height.");
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                }
            }
        }
    }
}
