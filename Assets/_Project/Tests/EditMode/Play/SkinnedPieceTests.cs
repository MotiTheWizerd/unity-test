using System;
using System.Collections.Generic;
using System.Linq;
using Forge.Play;
using Forge.Play.Hud;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Forge.Tests.Play
{
    public sealed class SkinnedPieceTests
    {
        readonly List<Object> _made = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var made in _made)
            {
                if (made != null)
                    Object.DestroyImmediate(made);
            }

            _made.Clear();
        }

        [Test]
        public void Stitch_DrawsThePieceOnTheBodysBones_UnderTheBody_OnItsLayer()
        {
            var body = Model("Body", "Body", out var bodySkin);
            body.layer = 5;
            var piece = Model("Piece", "Head", out var pieceSkin);
            var material = new Material(Shader.Find("Hidden/InternalErrorShader"));
            _made.Add(material);
            pieceSkin.sharedMaterials = new[] { material };

            var worn = SkinnedPiece.Stitch(body.transform, pieceSkin);

            Assert.That(worn.transform.parent, Is.SameAs(body.transform));
            Assert.That(worn.name, Is.EqualTo("Head"));
            Assert.That(worn.gameObject.layer, Is.EqualTo(5));
            Assert.That(worn.bones, Is.EqualTo(bodySkin.bones));
            Assert.That(worn.rootBone, Is.SameAs(bodySkin.rootBone));
            Assert.That(worn.sharedMesh, Is.SameAs(pieceSkin.sharedMesh));
            Assert.That(worn.sharedMaterials, Is.EqualTo(new[] { material }));
            Assert.That(pieceSkin.bones.All(b => b.IsChildOf(piece.transform)), Is.True, "the piece is left as it is");
        }

        [Test]
        public void Stitch_RefusesAPiece_OnABoneTheBodyLacks()
        {
            var body = Model("Body", "Body", out var bodySkin);
            bodySkin.bones[1].name = "mixamorig:Neck";
            Model("Piece", "Head", out var pieceSkin);

            Assert.Throws<ArgumentException>(() => SkinnedPiece.Stitch(body.transform, pieceSkin));
        }

        [Test]
        public void Wear_PutsOnTheHead_InPlaceOfTheOneWorn_AndTheSameAgainChangesNothing()
        {
            var body = Model("Body", "Body", out _);
            var heads = body.AddComponent<CharacterHeads>();
            Model("Plain", "Head", out var plain);
            Model("Warrior", "Head", out var warrior);
            heads.SetHeads(new[] { plain.transform.root.gameObject, warrior.transform.root.gameObject });
            Assert.That(heads.Count, Is.EqualTo(2));
            Assert.That(heads.NameOf(1), Is.EqualTo("Warrior"));
            Assert.That(heads.Worn, Is.EqualTo(-1));

            heads.Wear(0);
            Assert.That(HeadsDrawn(body).Select(h => h.sharedMesh), Is.EqualTo(new[] { plain.sharedMesh }));

            heads.Wear(1);
            var worn = HeadsDrawn(body);
            Assert.That(worn.Select(h => h.sharedMesh), Is.EqualTo(new[] { warrior.sharedMesh }), "the plain head is taken off");
            Assert.That(heads.Worn, Is.EqualTo(1));
            Assert.That(heads.WornRenderers, Is.EqualTo(worn));

            heads.Wear(1);
            Assert.That(HeadsDrawn(body), Is.EqualTo(worn));
            Assert.Throws<ArgumentOutOfRangeException>(() => heads.Wear(2));
        }

        [Test]
        public void PartsFor_ABodyWithHeads_LeadsTheHeadWithAChoiceOfThem_StartingAtTheOneItWears()
        {
            var body = Model("Body", "Body", out _);
            var heads = body.AddComponent<CharacterHeads>();
            Model("Plain", "Head", out var plain);
            Model("Warrior", "Head", out var warrior);
            heads.SetHeads(new[] { plain.transform.root.gameObject, warrior.transform.root.gameObject });
            heads.Wear(1);

            var parts = FaceEditorScreen.PartsFor(body);

            var head = parts.Single(p => p.Name == FaceLook.HeadPart);
            var face = head.Controls[0];
            Assert.That(face.Name, Is.EqualTo(FaceLook.FaceControl));
            Assert.That(face.Choices, Is.EqualTo(new[] { "Plain", "Warrior" }));
            Assert.That(face.Start, Is.EqualTo(1));
            Assert.That(head.Controls.Skip(1), Is.EqualTo(FaceEditorScreen.Parts.Single(p => p.Name == FaceLook.HeadPart).Controls));
            Assert.That(parts.Select(p => p.Name), Is.EqualTo(FaceEditorScreen.Parts.Select(p => p.Name)));
        }

        [Test]
        public void PartsFor_ACharacterWithoutHeadsToChoose_IsTheFacesParts()
        {
            Assert.That(FaceEditorScreen.PartsFor(null), Is.SameAs(FaceEditorScreen.Parts));
            Assert.That(FaceEditorScreen.PartsFor(Model("Body", "Body", out _)), Is.SameAs(FaceEditorScreen.Parts));
        }

        [Test]
        public void FaceLook_HeadsFace_PutsOnThatHead()
        {
            var body = Model("Body", "Body", out _);
            var heads = body.AddComponent<CharacterHeads>();
            Model("Plain", "Head", out var plain);
            Model("Warrior", "Head", out var warrior);
            heads.SetHeads(new[] { plain.transform.root.gameObject, warrior.transform.root.gameObject });
            heads.Wear(0);

            FaceLook.Apply(body, FaceLook.HeadPart, FaceLook.FaceControl, 1);

            Assert.That(heads.Worn, Is.EqualTo(1));
            Assert.That(HeadsDrawn(body).Select(h => h.sharedMesh), Is.EqualTo(new[] { warrior.sharedMesh }));
        }

        static SkinnedMeshRenderer[] HeadsDrawn(GameObject body) =>
            body.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.name == "Head").ToArray();

        // A model as an FBX imports one: a root, its skeleton (the hips, the head on them) and a skinned mesh of its own
        // on the head.
        GameObject Model(string name, string meshName, out SkinnedMeshRenderer skinned)
        {
            var root = new GameObject(name);
            _made.Add(root);
            var hips = new GameObject("mixamorig:Hips").transform;
            hips.SetParent(root.transform, false);
            hips.localPosition = new Vector3(0f, 1f, 0f);
            var head = new GameObject("mixamorig:Head").transform;
            head.SetParent(hips, false);
            head.localPosition = new Vector3(0f, 0.6f, 0f);

            var drawn = new GameObject(meshName);
            drawn.transform.SetParent(root.transform, false);
            var mesh = new Mesh
            {
                name = name + meshName,
                vertices = new[] { new Vector3(0f, 1.6f, 0f), new Vector3(0.1f, 1.7f, 0f), new Vector3(-0.1f, 1.7f, 0f) },
                triangles = new[] { 0, 1, 2 },
                boneWeights = Enumerable.Repeat(new BoneWeight { boneIndex0 = 1, weight0 = 1f }, 3).ToArray(),
                bindposes = new[] { hips.worldToLocalMatrix, head.worldToLocalMatrix },
            };
            _made.Add(mesh);
            skinned = drawn.AddComponent<SkinnedMeshRenderer>();
            skinned.sharedMesh = mesh;
            skinned.bones = new[] { hips, head };
            skinned.rootBone = hips;
            return root;
        }
    }
}
