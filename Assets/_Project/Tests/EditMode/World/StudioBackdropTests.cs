using Forge.World.Assets;
using NUnit.Framework;
using UnityEngine;

namespace Forge.Tests.World
{
    public class StudioBackdropTests
    {
        [Test]
        public void TheGlowIsBrightestBehindTheSubject_AndTheCornersDarkest()
        {
            var centre = Brightness(StudioBackdrop.GlowCentre.x, StudioBackdrop.GlowCentre.y);

            Assert.That(centre, Is.GreaterThan(Brightness(0f, 1f)));
            Assert.That(centre, Is.GreaterThan(Brightness(1f, 1f)));
            Assert.That(Brightness(0.5f, 0.75f), Is.GreaterThan(Brightness(0.5f, 0.98f)));
        }

        [Test]
        public void TheFloorUnderTheSubjectIsInShadow()
        {
            var under = Brightness(0.5f, StudioBackdrop.FloorLine);
            var beside = Brightness(0.2f, StudioBackdrop.FloorLine);

            Assert.That(under, Is.LessThan(beside));
        }

        [Test]
        public void ItIsSymmetricLeftToRight_AndOpaque()
        {
            for (var v = 0f; v <= 1f; v += 0.1f)
            {
                Assert.That(Brightness(0.3f, v), Is.EqualTo(Brightness(0.7f, v)).Within(1e-5f));
                Assert.That(StudioBackdrop.Shade(0.3f, v).a, Is.EqualTo(1f));
            }
        }

        [Test]
        public void CreatePaintsAWideTextureOfTheShade()
        {
            var texture = StudioBackdrop.Create(20);
            try
            {
                Assert.That(texture.height, Is.EqualTo(20));
                Assert.That(texture.width, Is.EqualTo(Mathf.RoundToInt(20 * StudioBackdrop.Aspect)));
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        static float Brightness(float u, float v) => StudioBackdrop.Shade(u, v).grayscale;
    }
}
