using System;
using System.Linq;
using System.Numerics;
using Forge.Editing.Placement;
using NUnit.Framework;

namespace Forge.Tests.Editing
{
    public class PlacementStrokeTests
    {
        // A fence: 3 m long along its local X, 0.2 m deep.
        static readonly Footprint Fence = new Footprint(3f, 0.2f);

        // A tree: 2 m round (square footprint).
        static readonly Footprint Tree = new Footprint(2f, 2f);

        static PlacementStroke FenceStroke(float gap = 0f) =>
            new PlacementStroke(Vector2.Zero, Fence, alignToPath: true, yawOffset: 0f,
                PlacementRoll.Identity, () => PlacementRoll.Identity, () => gap);

        static PlacementStroke TreeStroke(float gap) =>
            new PlacementStroke(Vector2.Zero, Tree, alignToPath: false, yawOffset: 0f,
                PlacementRoll.Identity, () => PlacementRoll.Identity, () => gap);

        [Test]
        public void Starts_WithOneObjectAtTheStart()
        {
            var stroke = FenceStroke();

            Assert.That(stroke.Items.Count, Is.EqualTo(1));
            Assert.That(stroke.Items[0].Position, Is.EqualTo(Vector2.Zero));
        }

        [Test]
        public void NextObject_WaitsUntilTheCursorIsClearOfTheLast()
        {
            var stroke = FenceStroke();

            stroke.MoveTo(new Vector2(2.9f, 0f), line: false);
            Assert.That(stroke.Items.Count, Is.EqualTo(1), "Not yet a full fence length away.");

            stroke.MoveTo(new Vector2(3.1f, 0f), line: false);
            Assert.That(stroke.Items.Count, Is.EqualTo(2));
        }

        [Test]
        public void Fences_WithZeroGap_MeetEndToEnd_AndFollowTheDrag()
        {
            var stroke = FenceStroke();

            stroke.MoveTo(new Vector2(0f, 10f), line: false); // drag along +Z, far in one jump

            Assert.That(stroke.Items.Count, Is.EqualTo(4), "Centers at 0, 3, 6, 9.");
            for (var i = 0; i < stroke.Items.Count; i++)
            {
                Assert.That(stroke.Items[i].Position.X, Is.EqualTo(0f).Within(1e-4));
                Assert.That(stroke.Items[i].Position.Y, Is.EqualTo(i * 3f).Within(1e-4));
                // Length (local +X) along world +Z: yaw 270 in the engine's convention.
                var axis = PlacementMath.LocalXAxis(stroke.Items[i].YawDegrees);
                Assert.That(axis.Y, Is.EqualTo(1f).Within(1e-4), $"Fence {i} points along the drag.");
            }
        }

        [Test]
        public void Gap_IsAddedBetweenFootprints()
        {
            var stroke = TreeStroke(gap: 1.5f);

            stroke.MoveTo(new Vector2(20f, 0f), line: false);

            var xs = stroke.Items.Select(i => i.Position.X).ToArray();
            Assert.That(xs, Is.EqualTo(new[] { 0f, 3.5f, 7f, 10.5f, 14f, 17.5f }).Within(1e-4));
        }

        [Test]
        public void RandomGap_IsDrawnOncePerPosition()
        {
            var random = new Random(9);
            var draws = 0;
            var stroke = new PlacementStroke(Vector2.Zero, Tree, false, 0f, PlacementRoll.Identity,
                () => PlacementRoll.Identity, () => { draws++; return (float)random.NextDouble() * 4f; });

            stroke.MoveTo(new Vector2(30f, 0f), line: true);
            var first = stroke.Items.Select(i => i.Position.X).ToArray();
            var drawsAfterFirst = draws;
            stroke.MoveTo(new Vector2(30f, 0f), line: true);

            Assert.That(stroke.Items.Select(i => i.Position.X), Is.EqualTo(first), "Re-laying keeps the drawn gaps.");
            Assert.That(draws, Is.EqualTo(drawsAfterFirst));
            var spacings = first.Zip(first.Skip(1), (a, b) => b - a).ToArray();
            Assert.That(spacings, Has.All.InRange(2f, 6f));
            Assert.That(spacings.Distinct().Count(), Is.GreaterThan(1), "Gaps vary.");
        }

        [Test]
        public void TurnedTrees_UseTheirFootprintAlongTheDrag()
        {
            // A 4 × 1 footprint turned 90°: only 1 m long along world X.
            var stroke = new PlacementStroke(Vector2.Zero, new Footprint(4f, 1f), false, 90f,
                PlacementRoll.Identity, () => PlacementRoll.Identity, () => 0f);

            stroke.MoveTo(new Vector2(3.5f, 0f), line: false);

            Assert.That(stroke.Items.Select(i => i.Position.X), Is.EqualTo(new[] { 0f, 1f, 2f, 3f }).Within(1e-4));
        }

        [Test]
        public void Scale_StretchesTheSpacing()
        {
            var stroke = new PlacementStroke(Vector2.Zero, Tree, false, 0f, new PlacementRoll(0f, 2f),
                () => new PlacementRoll(0f, 0.5f), () => 0f);

            stroke.MoveTo(new Vector2(3f, 0f), line: false);

            // 2 m tree at scale 2 (half 2) then scale 0.5 (half 0.5): 2.5 m apart.
            Assert.That(stroke.Items[1].Position.X, Is.EqualTo(2.5f).Within(1e-4));
            Assert.That(stroke.Items[1].Scale, Is.EqualTo(0.5f));
        }

        [Test]
        public void LineMode_LaysAStraightEvenRow_AndFreePathResumesAfter()
        {
            var stroke = FenceStroke();
            stroke.MoveTo(new Vector2(3.5f, 0f), line: false);
            Assert.That(stroke.Items.Count, Is.EqualTo(2));

            stroke.MoveTo(new Vector2(6.5f, 6.5f), line: true); // ~9.19 m diagonal
            Assert.That(stroke.Items.Count, Is.EqualTo(4), "Centers at 0, 3, 6, 9 along the diagonal.");
            var direction = Vector2.Normalize(new Vector2(1f, 1f));
            for (var i = 0; i < stroke.Items.Count; i++)
                Assert.That(Vector2.Distance(stroke.Items[i].Position, direction * (3f * i)), Is.LessThan(1e-3));

            stroke.MoveTo(new Vector2(3.5f, 0f), line: false);
            Assert.That(stroke.Items.Count, Is.EqualTo(2), "Releasing Shift shows the free path again.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void JitteryClick_KeepsTheAngleThePreviewShowed(bool line)
        {
            // A fence the user turned to 90°; the hand moves 3 cm while clicking.
            var stroke = new PlacementStroke(Vector2.Zero, Fence, alignToPath: true, yawOffset: 90f,
                PlacementRoll.Identity, () => PlacementRoll.Identity, () => 0f);

            stroke.MoveTo(new Vector2(0.02f, -0.02f), line);
            stroke.MoveTo(new Vector2(-0.03f, 0.005f), line);

            Assert.That(stroke.IsDragging, Is.False);
            Assert.That(stroke.Items.Count, Is.EqualTo(1));
            Assert.That(stroke.Items[0].YawDegrees, Is.EqualTo(90f));
        }

        [Test]
        public void DragStarts_OnlyBeyondHalfTheFootprint_AndStays()
        {
            var stroke = FenceStroke(); // half a fence: 1.5 m

            stroke.MoveTo(new Vector2(1.4f, 0f), line: false);
            Assert.That(stroke.IsDragging, Is.False);

            stroke.MoveTo(new Vector2(1.6f, 0f), line: false);
            stroke.MoveTo(new Vector2(0.1f, 0f), line: false);
            Assert.That(stroke.IsDragging, Is.True, "Coming back near the start is still a drag.");
        }

        [Test]
        public void PathFollowingRow_TakesItsHeadingFromTheDrag_NotTheUserAngle()
        {
            var stroke = new PlacementStroke(Vector2.Zero, Fence, alignToPath: true, yawOffset: 90f,
                PlacementRoll.Identity, () => PlacementRoll.Identity, () => 0f);

            stroke.MoveTo(new Vector2(10f, 0f), line: false);

            Assert.That(stroke.Items.Count, Is.EqualTo(4), "End to end along the drag, not packed side by side.");
            foreach (var item in stroke.Items)
                Assert.That(item.YawDegrees, Is.EqualTo(0f).Within(1e-3));
        }

        [Test]
        public void OwnHeadingObjects_KeepTheUserAngleWhileDragging()
        {
            var stroke = new PlacementStroke(Vector2.Zero, Tree, alignToPath: false, yawOffset: 30f,
                new PlacementRoll(10f, 1f), () => new PlacementRoll(200f, 1f), () => 0f);

            stroke.MoveTo(new Vector2(5f, 0f), line: false);

            Assert.That(stroke.Items[0].YawDegrees, Is.EqualTo(40f));
            Assert.That(stroke.Items[1].YawDegrees, Is.EqualTo(230f));
        }

        [Test]
        public void ZeroFootprint_WithZeroGap_StillStops()
        {
            var stroke = new PlacementStroke(Vector2.Zero, new Footprint(0f, 0f), false, 0f,
                PlacementRoll.Identity, () => PlacementRoll.Identity, () => 0f);

            stroke.MoveTo(new Vector2(1000f, 0f), line: false);

            Assert.That(stroke.Items.Count, Is.EqualTo(PlacementStroke.MaxItems));
        }

        [Test]
        public void YawAlong_PointsLocalXAlongTheDirection()
        {
            foreach (var direction in new[] { new Vector2(1, 0), new Vector2(0, 1), new Vector2(-1, 0), Vector2.Normalize(new Vector2(1, -1)) })
            {
                var axis = PlacementMath.LocalXAxis(PlacementMath.YawAlong(direction));
                Assert.That(Vector2.Distance(axis, direction), Is.LessThan(1e-4), direction.ToString());
            }
        }
    }
}
