using System.Linq;
using Forge.Play.Hud;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Forge.Tests.Play
{
    public class CrosshairTests
    {
        static VisualElement Tick(Crosshair crosshair, string way) =>
            crosshair.Children().Single(part => part.ClassListContains(Crosshair.TickClass + "--" + way));

        [Test]
        public void ADot_LitWhileSomethingCanBeUsed_AndNoCross()
        {
            var crosshair = new Crosshair();

            crosshair.Draw(on: true, aiming: false, pull: 0f);

            Assert.That(crosshair.ClassListContains(Crosshair.OnClass), Is.True);
            Assert.That(crosshair.ClassListContains(Crosshair.AimClass), Is.False);
        }

        [Test]
        public void ABowDrawn_MakesACross_ThatClosesInWithThePull_AndLightsAtFullDraw()
        {
            var crosshair = new Crosshair();

            crosshair.Draw(on: false, aiming: true, pull: 0f);
            Assert.That(crosshair.ClassListContains(Crosshair.AimClass), Is.True);
            Assert.That(Tick(crosshair, "up").style.top.value.value, Is.EqualTo(-Crosshair.LooseSpread));
            Assert.That(Tick(crosshair, "right").style.left.value.value, Is.EqualTo(Crosshair.LooseSpread));
            Assert.That(crosshair.ClassListContains(Crosshair.FullDrawClass), Is.False);

            crosshair.Draw(on: false, aiming: true, pull: 1f);
            Assert.That(Tick(crosshair, "down").style.top.value.value, Is.EqualTo(Crosshair.DrawnSpread));
            Assert.That(Tick(crosshair, "left").style.left.value.value, Is.EqualTo(-Crosshair.DrawnSpread));
            Assert.That(crosshair.ClassListContains(Crosshair.FullDrawClass), Is.True);

            crosshair.Draw(on: false, aiming: false, pull: 0f);
            Assert.That(crosshair.ClassListContains(Crosshair.AimClass) || crosshair.ClassListContains(Crosshair.FullDrawClass), Is.False);
        }
    }
}
