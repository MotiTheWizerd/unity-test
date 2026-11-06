using Forge.Game.Cameras;
using NUnit.Framework;

namespace Forge.Tests.Game
{
    public class ViewSwitchTests
    {
        static readonly ThirdPersonSettings Settings = new ThirdPersonSettings(distance: 4f, minDistance: 1f, maxDistance: 8f, zoomStep: 1f);

        static ViewSwitch NewSwitch(bool thirdPerson = false) =>
            new ViewSwitch(new FirstPersonCamera(), new ThirdPersonCamera(Settings), thirdPerson);

        static ViewCommands Toggle => new ViewCommands(true, 0f);

        static ViewCommands Wheel(float notches) => new ViewCommands(false, notches);

        [Test]
        public void StartsAsTheProfileSays()
        {
            Assert.That(NewSwitch().Current, Is.InstanceOf<FirstPersonCamera>());
            Assert.That(NewSwitch(thirdPerson: true).Current, Is.InstanceOf<ThirdPersonCamera>());
        }

        [Test]
        public void Toggle_SwapsViews_KeepingTheThirdPersonDistance()
        {
            var views = NewSwitch(thirdPerson: true);
            views.Apply(Wheel(-2f));

            views.Apply(Toggle);
            Assert.That(views.IsThirdPerson, Is.False);

            views.Apply(Toggle);
            Assert.That(views.IsThirdPerson, Is.True);
            Assert.That(views.ThirdPerson.Distance, Is.EqualTo(6f));
        }

        [Test]
        public void WheelingIn_PastTheClosest_GoesToFirstPerson()
        {
            var views = NewSwitch(thirdPerson: true);

            views.Apply(Wheel(3f));
            Assert.That(views.IsThirdPerson, Is.True, "First as close as it comes...");
            Assert.That(views.ThirdPerson.IsClosest, Is.True);

            views.Apply(Wheel(1f));
            Assert.That(views.IsThirdPerson, Is.False, "...then one notch more is first person.");
        }

        [Test]
        public void WheelingOut_InFirstPerson_ComesOutToThirdAtItsClosest()
        {
            var views = NewSwitch();

            views.Apply(Wheel(-1f));

            Assert.That(views.IsThirdPerson, Is.True);
            Assert.That(views.ThirdPerson.Distance, Is.EqualTo(Settings.MinDistance));
        }

        [Test]
        public void WheelingIn_InFirstPerson_StaysFirstPerson()
        {
            var views = NewSwitch();

            views.Apply(Wheel(1f));

            Assert.That(views.IsThirdPerson, Is.False);
        }
    }
}
