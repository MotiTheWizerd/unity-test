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
    }
}
