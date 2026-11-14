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

        [Test]
        public void WarriorM_BringsEyesOfItsOwn_ThatTurnToWhatItLooksAt_AsFarAsEyesTurn()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BodyPath);
            var holder = new GameObject("Body");
            try
            {
                var model = new CharacterModel(holder.transform, prefab);
                var heads = model.Root.GetComponentInChildren<CharacterHeads>();
                var head = model.Bone(HumanBodyBones.Head);
                Assert.That(heads.WornBones.Select(b => (b.name, b.parent)),
                    Is.EquivalentTo(new[] { ("mixamorig:LeftEye", head), ("mixamorig:RightEye", head) }), "WarriorM's eyes, on the body's head");
                var eyeballs = heads.WornRenderers.Single();
                foreach (var eye in heads.WornBones)
                    Assert.That(Carried(eyeballs, eye), Is.GreaterThan(500), $"{eye.name} carries its eyeball");

                Assert.That(model.TryGetEyeHeight(out var height), Is.True);
                var face = model.Eyes(height);
                Vector3? target = null;
                var look = new EyeLook(model, height, () => target);
                Assert.That(look.EyeCount, Is.EqualTo(2));
                var rest = heads.WornBones.Select(b => b.rotation).ToArray();

                // Near, 15 degrees aside and 10 up: each eye on it, from where it is.
                target = face.position + Quaternion.AngleAxis(-10f, face.right) * Quaternion.AngleAxis(15f, face.up) * face.forward * 0.8f;
                for (var i = 0; i < 20; i++)
                    look.Look(0.05f);
                for (var e = 0; e < 2; e++)
                {
                    var eye = heads.WornBones[e];
                    var looks = eye.rotation * Quaternion.Inverse(rest[e]) * face.forward;
                    Assert.That(Vector3.Angle(looks, target.Value - eye.position), Is.LessThan(0.5f), $"{eye.name} looks at it");
                }

                // Far aside: as far as an eye turns.
                target = face.position + face.right * 5f;
                for (var i = 0; i < 20; i++)
                    look.Look(0.05f);
                var turned = heads.WornBones[0].rotation * Quaternion.Inverse(rest[0]) * face.forward;
                Assert.That(Vector3.Angle(turned, face.forward), Is.EqualTo(EyeLook.MostTurn).Within(0.5f));

                // Nothing: ahead again.
                target = null;
                for (var i = 0; i < 20; i++)
                    look.Look(0.05f);
                Assert.That(Quaternion.Angle(heads.WornBones[0].rotation, rest[0]), Is.LessThan(0.01f));

                // A head without eyes of its own: nothing to turn; WarriorM again: its new eyes.
                heads.Wear(Enumerable.Range(0, heads.Count).Single(i => heads.NameOf(i) == "BodyWarriorM"));
                Assert.That(look.EyeCount, Is.EqualTo(0));
                heads.Wear(Enumerable.Range(0, heads.Count).Single(i => heads.NameOf(i) == "WarriorM"));
                Assert.That(look.EyeCount, Is.EqualTo(2));
                look.Dispose();
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }

        [Test]
        public void WarriorM_Blinks_NowAndThen_ShuttingHisLids_ButNotOnceFallen()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BodyPath);
            var holder = new GameObject("Body");
            try
            {
                var model = new CharacterModel(holder.transform, prefab);
                var heads = model.Root.GetComponentInChildren<CharacterHeads>();
                var awake = true;
                var blink = new EyeBlink(model, 7, () => awake);
                Assert.That(blink.LidCount, Is.EqualTo(1), "WarriorM's head shuts its eyes");
                var head = heads.WornRenderers.Single();
                var shape = head.sharedMesh.GetBlendShapeIndex(EyeBlink.Shape);

                // A minute, a frame at a time: blinks start LeastGap to MostGap apart (or TwiceGap, a double), each shut.
                const float frame = 0.01f;
                var starts = new System.Collections.Generic.List<float>();
                var wasOpen = true;
                var mostShut = 0f;
                for (var t = 0f; t < 60f; t += frame)
                {
                    blink.Blink(frame);
                    Assert.That(head.GetBlendShapeWeight(shape), Is.EqualTo(blink.Shut * 100f).Within(1e-3f));
                    if (wasOpen && blink.Shut > 0f)
                        starts.Add(t);
                    wasOpen = blink.Shut <= 0f;
                    mostShut = Mathf.Max(mostShut, blink.Shut);
                }

                Assert.That(mostShut, Is.EqualTo(1f).Within(0.01f), "shut all the way");
                Assert.That(starts.Count, Is.InRange(60 / (int)(EyeBlink.MostGap + 1), 60 / (int)EyeBlink.LeastGap + 4));
                for (var i = 1; i < starts.Count; i++)
                {
                    var gap = starts[i] - starts[i - 1] - EyeBlink.Length;
                    Assert.That(gap, Is.InRange(EyeBlink.TwiceGap - 2 * frame, EyeBlink.MostGap + 2 * frame), $"gap {i}");
                }

                awake = false;
                blink.Blink(frame);
                Assert.That(head.GetBlendShapeWeight(shape), Is.EqualTo(0f), "a body's eyes stay as they are");

                heads.Wear(Enumerable.Range(0, heads.Count).Single(i => heads.NameOf(i) == "BodyWarriorM"));
                Assert.That(blink.LidCount, Is.EqualTo(0), "the body's own head has no lids of its own");
                blink.Dispose();
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }

        // How many of a skinned mesh's vertices a bone carries wholly.
        static int Carried(SkinnedMeshRenderer skinned, Transform bone)
        {
            var index = System.Array.IndexOf(skinned.bones, bone);
            var counts = skinned.sharedMesh.GetBonesPerVertex();
            var weights = skinned.sharedMesh.GetAllBoneWeights();
            var carried = 0;
            for (int vertex = 0, at = 0; vertex < counts.Length; at += counts[vertex], vertex++)
            {
                for (var k = at; k < at + counts[vertex]; k++)
                {
                    if (weights[k].boneIndex == index && weights[k].weight > 0.999f)
                        carried++;
                }
            }

            return carried;
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
