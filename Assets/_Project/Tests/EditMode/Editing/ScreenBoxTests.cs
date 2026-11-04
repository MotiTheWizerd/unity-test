using System.Numerics;
using Forge.Editing.Geometry;
using NUnit.Framework;

namespace Forge.Tests.Editing
{
    public class ScreenBoxTests
    {
        [Test]
        public void TheCornersMayComeInAnyOrder()
        {
            var box = new ScreenBox(new Vector2(300f, 100f), new Vector2(100f, 250f));

            Assert.That(box.Min, Is.EqualTo(new Vector2(100f, 100f)));
            Assert.That(box.Max, Is.EqualTo(new Vector2(300f, 250f)));
            Assert.That((box.Width, box.Height), Is.EqualTo((200f, 150f)));
        }

        [Test]
        public void Contains_PointsInsideAndOnTheEdge_NotOutside()
        {
            var box = new ScreenBox(new Vector2(100f, 100f), new Vector2(300f, 250f));

            Assert.That(box.Contains(new Vector2(200f, 200f)), Is.True);
            Assert.That(box.Contains(new Vector2(100f, 250f)), Is.True);
            Assert.That(box.Contains(new Vector2(99f, 200f)), Is.False);
            Assert.That(box.Contains(new Vector2(200f, 251f)), Is.False);
        }
    }
}
