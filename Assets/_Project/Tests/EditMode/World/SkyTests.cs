using Forge.World.Sky;
using NUnit.Framework;
using UnityEngine;

namespace Forge.Tests.World
{
    public class SkyTests
    {
        [TestCase(0f, 0f, 0f, 0f, 1f)]
        [TestCase(0f, 90f, 1f, 0f, 0f)]
        [TestCase(0f, 180f, 0f, 0f, -1f)]
        [TestCase(90f, 123f, 0f, 1f, 0f)]
        public void SunDirection_IsHeightAboveTheHorizon_TowardACompassBearing(float height, float bearing, float x, float y, float z)
        {
            var direction = SkyView.SunDirection(height, bearing);

            Assert.That(direction.x, Is.EqualTo(x).Within(1e-5f));
            Assert.That(direction.y, Is.EqualTo(y).Within(1e-5f));
            Assert.That(direction.z, Is.EqualTo(z).Within(1e-5f));
        }

        [Test]
        public void Profile_ColorsFollowTheSun_WarmAtSunrise_DarkAndUnlitBelowTheHorizon()
        {
            var profile = ScriptableObject.CreateInstance<SkyProfile>();
            try
            {
                var sunrise = profile.ColorsAt(0f);
                var noon = profile.ColorsAt(60f);
                var night = profile.ColorsAt(SkyProfile.LowestSun);

                Assert.That(sunrise.Horizon.r, Is.GreaterThan(sunrise.Horizon.b), "A rising sun reddens the horizon.");
                Assert.That(noon.Horizon.b, Is.GreaterThan(noon.Horizon.r), "By day the horizon is pale blue.");
                Assert.That(noon.SunIntensity, Is.GreaterThan(sunrise.SunIntensity));
                Assert.That(night.SunIntensity, Is.EqualTo(0f));
                Assert.That(night.Zenith.grayscale, Is.LessThan(noon.Zenith.grayscale));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }
    }
}
