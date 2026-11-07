using Forge.World.Assets;
using NUnit.Framework;
using UnityEngine;

namespace Forge.Tests.World
{
    public class PreviewTurnTests
    {
        // The point of the subject nearest the viewer, who looks along +Z.
        static readonly Vector3 Near = new Vector3(0f, 0f, -1f);

        static void AssertNear(Vector3 actual, Vector3 expected, string message = null) =>
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(1e-4f), message ?? $"{actual} is not {expected}");

        static PreviewTurn Facing()
        {
            var turn = new PreviewTurn();
            // Face on, so near and far are easy to read.
            turn.Drag(PreviewTurn.StartSpin, 0f);
            return turn;
        }

        [Test]
        public void ANewSubject_StandsUpright_ThreeQuartersOn_Unzoomed()
        {
            var turn = new PreviewTurn();

            Assert.That((turn.Spin, turn.Zoom), Is.EqualTo((PreviewTurn.StartSpin, 1f)));
            Assert.That(Quaternion.Angle(turn.Tilt, Quaternion.identity), Is.LessThan(1e-3f));
        }

        [Test]
        public void DraggingAcross_SpinsTheNearSideTheSameWay_WithoutTilting()
        {
            var turn = Facing();

            turn.Drag(90f, 0f);

            AssertNear(turn.Rotation * Near, Vector3.right, "Dragged right, the near side goes right.");
            Assert.That(Quaternion.Angle(turn.Tilt, Quaternion.identity), Is.LessThan(1e-3f));
        }

        [Test]
        public void DraggingUp_TiltsTheNearSideUp_HoweverItWasSpun()
        {
            foreach (var spin in new[] { 0f, 90f, 200f })
            {
                var turn = Facing();
                turn.Drag(spin, 0f);
                var before = turn.Rotation;

                turn.Drag(0f, 90f);

                // Relative to the viewer, the whole thing turned 90° about the view's right axis.
                var relative = turn.Rotation * Quaternion.Inverse(before);
                AssertNear(relative * Near, Vector3.up, $"Spun {spin}°: the near side goes up.");
                AssertNear(relative * Vector3.right, Vector3.right, "About the view's right axis.");
            }
        }

        [Test]
        public void ATiltedThing_KeepsItsTilt_WhileItSpinsByItself()
        {
            var turn = Facing();
            turn.Drag(0f, 40f);
            var tilt = turn.Tilt;

            turn.Tick(2f);

            Assert.That(turn.Spin, Is.EqualTo(2f * PreviewTurn.AutoSpeed).Within(1e-3f));
            Assert.That(Quaternion.Angle(turn.Tilt, tilt), Is.LessThan(1e-3f), "Spinning never changes how it leans, so its framing holds still.");
        }

        [Test]
        public void Held_ItStaysPut_AndDoubleClickingPutsItBack()
        {
            var turn = Facing();
            turn.Held = true;
            turn.Tick(5f);
            Assert.That(turn.Spin, Is.Zero.Within(1e-3f));

            turn.Drag(30f, 60f);
            turn.ZoomBy(3f);
            turn.Reset();

            Assert.That((turn.Spin, turn.Zoom), Is.EqualTo((PreviewTurn.StartSpin, 1f)));
            Assert.That(Quaternion.Angle(turn.Tilt, Quaternion.identity), Is.LessThan(1e-3f));
        }

        [Test]
        public void ACharacter_StartsNearlyFaceOn_HoldsStill_AndOnlySpins()
        {
            var turn = PreviewTurn.Character();
            var start = turn.Spin;
            Assert.That(Mathf.DeltaAngle(start, PreviewTurn.FaceOn), Is.InRange(-30f, 30f), "It faces the viewer, near enough.");

            turn.Tick(5f);
            Assert.That(turn.Spin, Is.EqualTo(start), "It doesn't spin by itself.");

            turn.Drag(40f, 60f);
            Assert.That(turn.Spin, Is.EqualTo(Mathf.Repeat(start - 40f, 360f)).Within(1e-3f));
            Assert.That(Quaternion.Angle(turn.Tilt, Quaternion.identity), Is.LessThan(1e-3f), "A drag up never lays a man down.");

            turn.Reset();
            Assert.That(turn.Spin, Is.EqualTo(start), "Double-clicking puts it back at its own start.");
        }

        [Test]
        public void ACharacter_SeenAtEyeLevel_IsFramedNearer_ThanFromAnItemsHeight()
        {
            // A man standing: 0.4 m around his axis, 0.92 m up and down, in a tall narrow panel.
            const float reach = 0.4f, halfHeight = 0.92f, fov = 30f, aspect = 0.45f;

            var fromAbove = TurntableFraming.Distance(reach, halfHeight, fov, aspect, 1f, TurntableFraming.Pitch);
            var eyeLevel = TurntableFraming.Distance(reach, halfHeight, fov, aspect, 1f, PreviewTurn.Character().Pitch);
            var pose = TurntableFraming.CameraPose(Vector3.zero, eyeLevel, PreviewTurn.Character().Pitch);

            Assert.That(eyeLevel, Is.LessThan(fromAbove), "Seen level, his height is all that must fit.");
            Assert.That(pose.position.y / eyeLevel, Is.LessThan(0.1f), "The camera stands about level with him.");
        }

        [Test]
        public void Zoom_StaysWithinTheFramingsBounds()
        {
            var turn = new PreviewTurn();

            turn.ZoomBy(100f);
            Assert.That(turn.Zoom, Is.EqualTo(TurntableFraming.MaxZoom));
            turn.ZoomBy(-100f);
            Assert.That(turn.Zoom, Is.EqualTo(TurntableFraming.MinZoom));
        }

        [Test]
        public void Swept_AFlatSwordStoodUp_ReachesLittle_AndStandsTall()
        {
            // A sword lying along Z: 0.5 m half length, 2 cm thick, 5 cm wide.
            var sword = new Vector3(0.05f, 0.02f, 0.5f);

            TurntableFraming.Swept(sword, Quaternion.identity, out var flatReach, out var flatHeight);
            TurntableFraming.Swept(sword, Quaternion.AngleAxis(90f, Vector3.right), out var upReach, out var upHeight);

            Assert.That(flatReach, Is.EqualTo(new Vector2(0.05f, 0.5f).magnitude).Within(1e-4f));
            Assert.That(flatHeight, Is.EqualTo(0.02f).Within(1e-4f));
            Assert.That(upHeight, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(upReach, Is.EqualTo(new Vector2(0.05f, 0.02f).magnitude).Within(1e-4f));
        }
    }
}
