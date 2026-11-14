using Forge.Play.Hud;
using NUnit.Framework;
using UnityEngine;

namespace Forge.Tests.Play
{
    public sealed class TurnManipulatorTests
    {
        // Moved as if held: dragged down (panel y grows down) he goes down, so the aim goes up toward his face; dragged
        // right he goes right, so the aim goes left; and nearer (zoomed) the same drag moves the aim less, so he
        // follows the hand.
        [Test]
        public void PanFor_MovesItWithTheHand_LessWhenNearer()
        {
            var down = TurnManipulator.PanFor(new Vector2(0f, 100f), 500f, 1f);
            Assert.That(down.y, Is.GreaterThan(0f));
            Assert.That(down.x, Is.EqualTo(0f));
            Assert.That(TurnManipulator.PanFor(new Vector2(100f, 0f), 500f, 1f).x, Is.LessThan(0f));

            var near = TurnManipulator.PanFor(new Vector2(0f, 100f), 500f, 3f);
            Assert.That(near.y, Is.EqualTo(down.y / 3f).Within(1e-5f));
            // Half the view's height at no zoom moves the aim by about the subject's half height.
            Assert.That(TurnManipulator.PanFor(new Vector2(0f, 250f), 500f, 1f).y, Is.EqualTo(1f).Within(1e-5f));
        }
    }
}
