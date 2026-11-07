using Forge.World.Assets;
using NUnit.Framework;
using UnityEngine;

namespace Forge.Tests.World
{
    public class IconFramingTests
    {
        // Half sizes, as the items are: a sword lying flat along Z, a pile of coins, a flask, an apple.
        static readonly Vector3 Sword = new Vector3(0.05f, 0.012f, 0.45f);
        static readonly Vector3 Coins = new Vector3(0.042f, 0.006f, 0.038f);
        static readonly Vector3 Flask = new Vector3(0.04f, 0.065f, 0.04f);
        static readonly Vector3 Apple = new Vector3(0.04f, 0.045f, 0.04f);

        [Test]
        public void WhatLiesFlat_IsLookedDownOn_WhatStands_IsSeenFromTheFront()
        {
            Assert.That((IconFraming.IsFlat(Sword), IconFraming.IsFlat(Coins)), Is.EqualTo((true, true)));
            Assert.That((IconFraming.IsFlat(Flask), IconFraming.IsFlat(Apple)), Is.EqualTo((false, false)));

            Assert.That(PitchOf(Coins), Is.EqualTo(IconFraming.FlatPitch).Within(0.1f));
            Assert.That(PitchOf(Flask), Is.EqualTo(IconFraming.StandingPitch).Within(0.1f));
        }

        [Test]
        public void ALongThing_LiesAlongTheDiagonal()
        {
            Assert.That(IconFraming.IsLong(Sword), Is.True);
            Assert.That(IconFraming.IsLong(Coins) || IconFraming.IsLong(Flask) || IconFraming.IsLong(Apple), Is.False);

            // The sword's length, as the camera sees it, runs as far across as up: corner to corner.
            var inverse = Quaternion.Inverse(IconFraming.ViewRotation(Sword));
            var length = inverse * Vector3.forward;
            Assert.That(Mathf.Abs(length.x), Is.EqualTo(Mathf.Abs(length.y)).Within(1e-3f));
            Assert.That(new Vector2(length.x, length.y).magnitude, Is.GreaterThan(0.99f), "Its full length shows, not foreshortened.");
        }

        [Test]
        public void SomethingStanding_StaysUpright()
        {
            var up = Quaternion.Inverse(IconFraming.ViewRotation(Flask)) * Vector3.up;

            Assert.That(up.x, Is.EqualTo(0f).Within(1e-3f));
            Assert.That(up.y, Is.GreaterThan(0.9f));
        }

        [Test]
        public void EveryCornerFallsInside_AndTheThingFillsItsSquare()
        {
            foreach (var extents in new[] { Sword, Coins, Flask, Apple })
            {
                var bounds = new Bounds(new Vector3(3f, 1f, -2f), extents * 2f);
                var pose = IconFraming.CameraPose(bounds, out var size, out var near, out var far);

                var inverse = Quaternion.Inverse(pose.rotation);
                var widest = 0f;
                for (var corner = 0; corner < 8; corner++)
                {
                    var point = bounds.center + Vector3.Scale(extents, new Vector3(
                        (corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
                    var local = inverse * (point - pose.position);
                    Assert.That(Mathf.Abs(local.x), Is.LessThanOrEqualTo(size), $"{extents}: across");
                    Assert.That(Mathf.Abs(local.y), Is.LessThanOrEqualTo(size), $"{extents}: up");
                    Assert.That(local.z, Is.GreaterThan(near).And.LessThan(far), $"{extents}: depth");
                    widest = Mathf.Max(widest, Mathf.Abs(local.x), Mathf.Abs(local.y));
                }

                Assert.That(widest, Is.GreaterThan(size / IconFraming.Margin - 1e-4f), $"{extents}: it reaches the margin, centred.");
            }
        }

        static float PitchOf(Vector3 extents)
        {
            var forward = IconFraming.ViewRotation(extents) * Vector3.forward;
            return Mathf.Asin(-forward.y) * Mathf.Rad2Deg;
        }
    }
}
