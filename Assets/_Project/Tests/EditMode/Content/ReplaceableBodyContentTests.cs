using System.Linq;
using Forge.Play;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Forge.Tests.Content
{
    // Content guard: the player's replaceable body as Build Characters made it (Content/Characters/BodyWarriorM, its heads
    // under Heads/, blender-tests pieces.py): it stands whole in the head it is made with, and every head it can wear sits
    // on its neck where the head was made to, drawn by the body's own bones.
    public class ReplaceableBodyContentTests
    {
        const string BodyPath = "Assets/_Project/Content/Characters/BodyWarriorM/BodyWarriorM.prefab";
        const string HeadsFolder = "Assets/_Project/Content/Characters/BodyWarriorM/Heads";

        // A head is drawn on the body's bones by their names. The body's prefab stands in its bind pose (Build Characters),
        // which is the skeleton's rest both were made on, so a head baked on it lies where its mesh was made, to within a
        // tenth of a millimetre. (Not against its own prefab's skeleton: a Mixamo file's bones stand in its clip's first
        // frame, 2 cm off at the head.)
        const float SamePlace = 1e-4f;

        [Test]
        public void BodyWarriorM_StandsInWarriorM_AndEveryHeadSitsWhereItWasMade_OnTheBodysBones()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BodyPath);
            Assert.That(prefab, Is.Not.Null, "Build Characters makes BodyWarriorM.");
            var made = prefab.GetComponent<CharacterHeads>();
            Assert.That(made, Is.Not.Null, "BodyWarriorM lists its heads.");
            Assert.That(made.Count, Is.GreaterThanOrEqualTo(2), "it has heads to choose from");
            Assert.That(made.NameOf(made.Worn), Is.EqualTo("WarriorM"), "it is made in the face editor's head");

            var body = Object.Instantiate(prefab);
            try
            {
                var heads = body.GetComponent<CharacterHeads>();
                var skin = body.GetComponentsInChildren<SkinnedMeshRenderer>().Single(r => r.name == "Body").sharedMaterial;
                for (var i = 0; i < heads.Count; i++)
                {
                    heads.Wear(i);
                    var name = heads.NameOf(i);
                    var worn = heads.WornRenderers.Single();
                    Assert.That(worn.bones.All(b => b != null && b.IsChildOf(body.transform)), Is.True, $"{name} is drawn by the body's bones");
                    Assert.That(worn.sharedMaterials, Does.Contain(skin), $"{name}'s collar draws the body's skin");
                    if (worn.sharedMaterials.Any(m => m != skin))
                        Assert.That(TrianglesDrawing(worn, skin), Is.LessThan(worn.sharedMesh.triangles.Length / 3 / 10),
                            $"{name}'s own face draws its own maps, the body's skin only its collar");
                    Assert.That(body.GetComponentsInChildren<SkinnedMeshRenderer>().Count(r => r.name == "Head"), Is.EqualTo(1), $"{name} alone is worn");

                    var apart = OffWhereMade(worn);
                    Assert.That(apart, Is.LessThan(SamePlace), $"{name} lies {apart * 1000f:F2} mm off where it was made");
                }

                heads.Wear(made.Worn);
                Assert.That(heads.WornRenderers.Single().sharedMesh.GetBlendShapeIndex("NoseWidth"), Is.GreaterThanOrEqualTo(0),
                    "WarriorM keeps the face editor's sliders");
            }
            finally
            {
                Object.DestroyImmediate(body);
            }
        }

        static int TrianglesDrawing(SkinnedMeshRenderer renderer, Material material) =>
            Enumerable.Range(0, renderer.sharedMesh.subMeshCount)
                .Where(i => renderer.sharedMaterials[i] == material)
                .Sum(i => (int)renderer.sharedMesh.GetSubMesh(i).indexCount / 3);

        // How far a skinned mesh's farthest vertex lies, baked as its bones stand, from where its mesh has it.
        static float OffWhereMade(SkinnedMeshRenderer skinned)
        {
            var baked = new Mesh();
            try
            {
                skinned.BakeMesh(baked, true);
                var posed = baked.vertices;
                var made = skinned.sharedMesh.vertices;
                var far = 0f;
                for (var i = 0; i < made.Length; i++)
                    far = Mathf.Max(far, Vector3.Distance(posed[i], made[i]));
                return far;
            }
            finally
            {
                Object.DestroyImmediate(baked);
            }
        }
    }
}
