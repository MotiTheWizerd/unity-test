using System.Linq;
using System.Numerics;
using Forge.Editing.Geometry;
using NUnit.Framework;

namespace Forge.Tests.Editing
{
    public class GroundMarkTests
    {
        [Test]
        public void AClick_MarksASpot()
        {
            var mark = GroundMark.FromDrag(new Vector2(10f, 20f), new Vector2(10.4f, 19.7f));

            Assert.That(mark.IsArea, Is.False);
            Assert.That(mark.Center, Is.EqualTo(new Vector2(10f, 20f)), "The spot is where the pointer went down.");
            Assert.That(mark.Describe(), Is.EqualTo("a spot at x=10, z=20"));
        }

        [Test]
        public void ADrag_MarksTheAreaBetweenItsCorners_WhicheverWayItGoes()
        {
            var mark = GroundMark.FromDrag(new Vector2(110f, 75f), new Vector2(70f, 50f));

            Assert.That(mark.IsArea, Is.True);
            Assert.That(mark.Center, Is.EqualTo(new Vector2(90f, 62.5f)));
            Assert.That((mark.Area.Value.Width, mark.Area.Value.Depth), Is.EqualTo((40f, 25f)));
            Assert.That(mark.Describe(), Is.EqualTo("a 40 × 25 m area from x=70 to 110, z=50 to 75"));
        }

        [Test]
        public void AThinDrag_StillMarksAnAreaAtLeastAMeterWide()
        {
            var mark = GroundMark.FromDrag(new Vector2(0f, 0f), new Vector2(30f, 0.2f));

            Assert.That((mark.Area.Value.Width, mark.Area.Value.Depth), Is.EqualTo((30f, GroundMark.MinAreaSize)));
        }

        [Test]
        public void RectCorners_GoAroundTheTurnedRectangle()
        {
            // Facing east, 20 m across the front (north-south) and 10 m deep (east-west).
            var corners = new GroundRect(new Vector2(50f, 50f), 20f, 10f, 90f).Corners();

            var expected = new[] { new Vector2(45f, 60f), new Vector2(45f, 40f), new Vector2(55f, 40f), new Vector2(55f, 60f) };
            Assert.That(corners.Zip(expected, Vector2.Distance).All(d => d < 1e-4f), Is.True, string.Join(" ", corners));
        }
    }
}
