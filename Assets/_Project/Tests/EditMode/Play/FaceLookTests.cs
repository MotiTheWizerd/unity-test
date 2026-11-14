using Forge.Play.Hud;
using NUnit.Framework;
using UnityEngine;

namespace Forge.Tests.Play
{
    public sealed class FaceLookTests
    {
        [Test]
        public void ShapeName_IsThePartAndTheControl_WithoutSpaces()
        {
            Assert.That(FaceLook.ShapeName("Nose", "Width"), Is.EqualTo("NoseWidth"));
            Assert.That(FaceLook.ShapeName("Jaw", "Chin Width"), Is.EqualTo("JawChinWidth"));
        }

        [Test]
        public void Weight_RunsFromTheOtherWay_ThroughNoneAtTheMiddle_ToAllOfIt()
        {
            Assert.That(FaceLook.Weight(0), Is.EqualTo(-100f));
            Assert.That(FaceLook.Weight(FaceEditorScreen.Middle), Is.EqualTo(0f));
            Assert.That(FaceLook.Weight(100), Is.EqualTo(100f));
        }

        [Test]
        public void SkinTint_LeavesTheSkinAsMadeAtTheMiddle_AndDarkensItAbove()
        {
            Assert.That(FaceLook.SkinTint(FaceEditorScreen.Middle), Is.EqualTo(Color.white));
            var deep = FaceLook.SkinTint(100);
            Assert.That(deep.r, Is.LessThan(0.5f));
            Assert.That(deep.b, Is.LessThan(deep.r), "deep skin is warm");
            Assert.That(FaceLook.SkinTint(0).r, Is.GreaterThan(1f), "a touch paler at 0");
        }

        // A head's eyes are a slot of its mesh (a material named "{Head}Eyes"): the tone darkens the skin beside them,
        // never the eyes' whites.
        [Test]
        public void SkinTone_TintsTheSkin_AndLeavesTheEyesAsMade()
        {
            var head = new GameObject("Head");
            var skin = new Material(Shader.Find("Hidden/InternalErrorShader")) { name = "WarriorM" };
            var eyes = new Material(Shader.Find("Hidden/InternalErrorShader")) { name = "WarriorMEyes" };
            try
            {
                var renderer = head.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = new[] { skin, eyes };

                FaceLook.Apply(head, "Skin", "Tone", 100);

                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block, 0);
                Assert.That(((Vector4)(block.GetColor("_BaseColor") - FaceLook.SkinTint(100))).magnitude, Is.LessThan(1e-4f), "the skin darkens");
                renderer.GetPropertyBlock(block, 1);
                Assert.That(((Vector4)(block.GetColor("_BaseColor") - Color.white)).magnitude, Is.LessThan(1e-4f), "the eyes stay as made");
                Assert.That(FaceLook.IsEyes(eyes), Is.True);
                Assert.That(FaceLook.IsEyes(skin), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(head);
                Object.DestroyImmediate(skin);
                Object.DestroyImmediate(eyes);
            }
        }
    }
}
