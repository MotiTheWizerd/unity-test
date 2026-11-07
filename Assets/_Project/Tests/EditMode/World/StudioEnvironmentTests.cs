using Forge.World.Assets;
using NUnit.Framework;
using UnityEngine;

namespace Forge.Tests.World
{
    public class StudioEnvironmentTests
    {
        const int Size = 8;

        [Test]
        public void EachFace_LooksOutAlongItsAxis_FromItsMiddle()
        {
            Assert.That(Middle(CubemapFace.PositiveX), Is.EqualTo(Vector3.right));
            Assert.That(Middle(CubemapFace.NegativeX), Is.EqualTo(Vector3.left));
            Assert.That(Middle(CubemapFace.PositiveY), Is.EqualTo(Vector3.up));
            Assert.That(Middle(CubemapFace.NegativeY), Is.EqualTo(Vector3.down));
            Assert.That(Middle(CubemapFace.PositiveZ), Is.EqualTo(Vector3.forward));
            Assert.That(Middle(CubemapFace.NegativeZ), Is.EqualTo(Vector3.back));
        }

        [Test]
        public void ASideFace_LooksUpAtItsTopRow_AndDownAtItsBottom()
        {
            foreach (var face in new[] { CubemapFace.PositiveX, CubemapFace.NegativeX, CubemapFace.PositiveZ, CubemapFace.NegativeZ })
            {
                Assert.That(StudioEnvironment.Up(face, Size / 2, 0, Size), Is.GreaterThan(0.5f), $"{face}: top row");
                Assert.That(StudioEnvironment.Up(face, Size / 2, Size - 1, Size), Is.LessThan(-0.5f), $"{face}: bottom row");
            }
        }

        [Test]
        public void EveryDirection_IsAUnitOne()
        {
            var direction = StudioEnvironment.Direction(CubemapFace.PositiveZ, 0, 0, Size);

            Assert.That(direction.magnitude, Is.EqualTo(1f).Within(1e-5f));
        }

        // Between the two middle texels of an even face: straight out.
        static Vector3 Middle(CubemapFace face)
        {
            var a = StudioEnvironment.Direction(face, Size / 2 - 1, Size / 2 - 1, Size);
            var b = StudioEnvironment.Direction(face, Size / 2, Size / 2, Size);
            var middle = (a + b).normalized;
            return new Vector3(Mathf.Round(middle.x), Mathf.Round(middle.y), Mathf.Round(middle.z));
        }
    }
}
