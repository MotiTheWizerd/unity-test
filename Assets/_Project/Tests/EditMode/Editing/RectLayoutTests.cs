using System;
using System.Linq;
using System.Numerics;
using Forge.Editing.Geometry;
using Forge.Editing.Placement;
using NUnit.Framework;

namespace Forge.Tests.Editing
{
    public class RectLayoutTests
    {
        const float Tolerance = 1e-4f;

        // A wall section: 4 m long across its front, 0.2 m thick.
        static readonly Footprint Wall = new Footprint(4f, 0.2f);

        // A floor tile: 4 m square.
        static readonly Footprint Tile = new Footprint(4f, 4f);

        static PlacementRoll Exact(int index) => PlacementRoll.Identity;

        static void AssertNear(Vector2 actual, Vector2 expected) =>
            Assert.That(Vector2.Distance(actual, expected), Is.LessThan(Tolerance), $"expected {expected} but was {actual}");

        [Test]
        public void Outline_IsAPinwheel_EachSideFromItsCornerToOneThicknessShortOfTheNext()
        {
            // 12.2 × 8.2 m: each side is that minus the 0.2 m thickness, 12 m (3 sections) and 8 m (2 sections).
            var rect = new GroundRect(Vector2.Zero, 12.2f, 8.2f);

            var items = RectLayout.Lay(rect, RectPattern.Outline, Wall, 0f, alignToRect: true, Exact);

            Assert.That(items.Count, Is.EqualTo(10));
            Assert.That(RectLayout.Count(rect, RectPattern.Outline, Wall, 0f), Is.EqualTo(10));
            // Back (south, 0.1 inside z = -4.1), from the south-west corner (x = -6.1) east, running east.
            AssertNear(items[0].Position, new Vector2(-4.1f, -4f));
            AssertNear(items[1].Position, new Vector2(-0.1f, -4f));
            AssertNear(items[2].Position, new Vector2(3.9f, -4f));
            Assert.That(items[0].YawDegrees, Is.EqualTo(0f).Within(Tolerance));
            // East side (0.1 inside x = 6.1), from the south-east corner (z = -4.1) north, running north.
            AssertNear(items[3].Position, new Vector2(6f, -2.1f));
            AssertNear(items[4].Position, new Vector2(6f, 1.9f));
            Assert.That(items[3].YawDegrees, Is.EqualTo(PlacementMath.YawAlong(Vector2.UnitY)).Within(Tolerance));
        }

        [Test]
        public void Outline_AroundAFloorOfWholeTiles_FromTheSameCorner_MeetsItExactly()
        {
            // Dragged from (0, 0): the floor snaps to 12 × 8 of 4 m tiles, the walls to 12.2 × 8.2.
            var floor = RectLayout.FromDrag(Vector2.Zero, new Vector2(11f, 7f), 0f, false, RectPattern.Fill, Tile, 0f, snapToPieces: true);
            var walls = RectLayout.FromDrag(Vector2.Zero, new Vector2(11f, 7f), 0f, false, RectPattern.Outline, Wall, 0f, snapToPieces: true);

            Assert.That((floor.Width, floor.Depth), Is.EqualTo((12f, 8f)));
            Assert.That(walls.Width, Is.EqualTo(12.2f).Within(Tolerance));
            Assert.That(walls.Depth, Is.EqualTo(8.2f).Within(Tolerance));
            // The far walls' inner faces stand exactly on the floor's far edges (x = 12, z = 8).
            var items = RectLayout.Lay(walls, RectPattern.Outline, Wall, 0f, alignToRect: true, Exact);
            Assert.That(items.Where(i => Math.Abs(i.Position.X - 12.1f) < Tolerance).Count(), Is.EqualTo(2), "east walls");
            Assert.That(items.Where(i => Math.Abs(i.Position.Y - 8.1f) < Tolerance).Count(), Is.EqualTo(3), "north walls");
        }

        [Test]
        public void Outline_CoversEveryCorner_WithNothingOutsideTheRectangle()
        {
            var rect = new GroundRect(new Vector2(30f, 20f), 12f, 8.4f, headingDegrees: 30f);
            var items = RectLayout.Lay(rect, RectPattern.Outline, Wall, 0f, alignToRect: true, Exact);

            // Each section's ends lie inside (or on) the rectangle, and each corner touches a section's outer end.
            foreach (var item in items)
            {
                var along = PlacementMath.LocalXAxis(item.YawDegrees) * 2f;
                Assert.That(rect.DistanceOutside(item.Position + along), Is.LessThan(1e-3f));
                Assert.That(rect.DistanceOutside(item.Position - along), Is.LessThan(1e-3f));
            }

            foreach (var corner in rect.Corners())
            {
                // A section's four footprint corners: half its length along it, half its thickness across.
                var touched = items.Any(item =>
                {
                    var length = PlacementMath.LocalXAxis(item.YawDegrees);
                    var thickness = new Vector2(-length.Y, length.X);
                    return new[] { 1f, -1f }.SelectMany(l => new[] { 1f, -1f }, (l, t) => length * (2f * l) + thickness * (0.1f * t))
                        .Any(end => Vector2.Distance(item.Position + end, corner) < 1e-3f);
                });
                Assert.That(touched, Is.True, $"corner {corner} is open");
            }
        }

        [Test]
        public void Outline_ASideThatIsNotWholeSections_SlidesItsLastOneBackToTheCorner()
        {
            // The back runs 10 - 0.2 = 9.8 m from x = 0: sections at 2, 6, and the last one ends at 9.8 (centered at 7.8).
            var rect = new GroundRect(new Vector2(5f, 0f), 10f, 4.2f);

            var back = RectLayout.Lay(rect, RectPattern.Outline, Wall, 0f, alignToRect: true, Exact).Take(3).ToList();

            Assert.That(back.Select(i => i.Position.X), Is.EqualTo(new[] { 2f, 6f, 7.8f }).Within(Tolerance));
        }

        [Test]
        public void SnapSize_GivesWholeSectionsOnEverySide_PlusOneThickness()
        {
            var (width, depth) = RectLayout.SnapSize(11f, 9f, RectPattern.Outline, Wall, 0f);

            // 11 - 0.2 = 10.8 → 3 sections = 12 m; 9 - 0.2 = 8.8 → 2 sections = 8 m; each plus the thickness.
            Assert.That(width, Is.EqualTo(12.2f).Within(Tolerance));
            Assert.That(depth, Is.EqualTo(8.2f).Within(Tolerance));
            Assert.That(RectLayout.SnapSize(11f, 9f, RectPattern.Fill, Tile, 0f), Is.EqualTo((12f, 8f)));
        }

        [Test]
        public void SnapSize_CountsTheGapBetweenPieces_AndNeverGoesBelowOne()
        {
            Assert.That(RectLayout.SnapSize(9f, 1f, RectPattern.Fill, Tile, 1f), Is.EqualTo((9f, 4f)), "2 tiles + 1 gap = 9 m; at least one tile.");
        }

        [Test]
        public void FromDrag_KeepsTheStartCorner_AndGrowsTowardsThePointer()
        {
            var rect = RectLayout.FromDrag(new Vector2(10f, 10f), new Vector2(-1f, 1.5f), 0f, square: false,
                RectPattern.Fill, Tile, 0f, snapToPieces: true);

            // Dragged 11 west and 8.5 south: snapped to 12 × 8, from the start corner westwards and southwards.
            Assert.That((rect.Width, rect.Depth), Is.EqualTo((12f, 8f)));
            AssertNear(rect.Center, new Vector2(4f, 6f));
        }

        [Test]
        public void FromDrag_Square_UsesTheLongerSide_AndTurnsWithTheHeading()
        {
            var rect = RectLayout.FromDrag(Vector2.Zero, new Vector2(0f, -5f), 90f, square: true,
                RectPattern.Fill, Tile, 0f, snapToPieces: false);

            Assert.That((rect.Width, rect.Depth), Is.EqualTo((5f, 5f)));
            Assert.That(rect.HeadingDegrees, Is.EqualTo(90f));
            Assert.That(rect.DistanceOutside(new Vector2(0f, -5f)), Is.LessThan(Tolerance), "The pointer lies on the rectangle's edge.");
        }

        [Test]
        public void Fill_LaysRowsOverTheWholeRectangle_TurnedWithIt()
        {
            var rect = new GroundRect(Vector2.Zero, 8f, 8f, headingDegrees: 90f);

            var items = RectLayout.Lay(rect, RectPattern.Fill, Tile, 0f, alignToRect: true, Exact);

            Assert.That(items.Count, Is.EqualTo(4));
            Assert.That(items.Select(i => Math.Abs(i.Position.X)), Is.All.EqualTo(2f).Within(Tolerance));
            Assert.That(items.Select(i => Math.Abs(i.Position.Y)), Is.All.EqualTo(2f).Within(Tolerance));
            Assert.That(items.Select(i => i.YawDegrees), Is.All.EqualTo(90f).Within(Tolerance));
        }

        [Test]
        public void PiecesThatKeepTheirOwnHeading_AddTheirDrawnTurnToTheRectangles_AndTakeTheirSize()
        {
            var rect = new GroundRect(Vector2.Zero, 8f, 4f, headingDegrees: 10f);

            var items = RectLayout.Lay(rect, RectPattern.Fill, Tile, 0f, alignToRect: false, i => new PlacementRoll(20f * i, 1f + 0.1f * i));

            Assert.That(items.Select(i => i.YawDegrees), Is.EqualTo(new[] { 10f, 30f }).Within(Tolerance));
            Assert.That(items.Select(i => i.Scale), Is.EqualTo(new[] { 1f, 1.1f }).Within(Tolerance));
        }

        [Test]
        public void MoreThanTheCap_IsRefused()
        {
            var rect = new GroundRect(Vector2.Zero, 200f, 200f);

            Assert.That(RectLayout.Count(rect, RectPattern.Fill, Tile, 0f), Is.EqualTo(2500));
            Assert.Throws<ArgumentException>(() => RectLayout.Lay(rect, RectPattern.Fill, Tile, 0f, true, Exact));
        }
    }
}
