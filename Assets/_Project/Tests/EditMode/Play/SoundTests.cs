using System;
using System.Linq;
using Forge.Game.Motion;
using Forge.Game.Player;
using Forge.Play.Sound;
using NUnit.Framework;

namespace Forge.Tests.Play
{
    public class SoundTests
    {
        static readonly LocomotionSettings Paces = LocomotionSettings.Default;

        [Test]
        public void ASound_InAVoice_IsItsVersionMadeInIt_AndWithoutOne_ItselfAsItIs()
        {
            Assert.That(SoundDirector.Voiced(SoundDirector.Jump, "Female"), Is.EqualTo("foley/jumpfemale"));
            Assert.That(SoundDirector.Voiced(SoundDirector.Jump, null), Is.EqualTo(SoundDirector.Jump));
            Assert.That(SoundDirector.Voiced(SoundDirector.Jump, " "), Is.EqualTo(SoundDirector.Jump));
        }

        [Test]
        public void ABlock_SoundsAsTheShieldIsMade_AndOfNoKind_ThePlainBlock()
        {
            Assert.That(SoundDirector.Made(SoundDirector.Block, "metal"), Is.EqualTo("combat/blockmetal"));
            Assert.That(SoundDirector.Made(SoundDirector.Block, "wood"), Is.EqualTo("combat/blockwood"));
            Assert.That(SoundDirector.Made(SoundDirector.Block, null), Is.EqualTo(SoundDirector.Block));
        }

        [Test]
        public void ATake_NeverPlaysTwiceRunning()
        {
            var picker = new TakePicker(new Random(7));
            var last = picker.Next(4);
            for (var i = 0; i < 200; i++)
            {
                var next = picker.Next(4);
                Assert.That(next, Is.InRange(0, 3));
                Assert.That(next, Is.Not.EqualTo(last));
                last = next;
            }
        }

        [Test]
        public void EveryTake_IsPlayedInTime()
        {
            var picker = new TakePicker(new Random(3));
            var played = Enumerable.Range(0, 100).Select(_ => picker.Next(5)).Distinct().Count();

            Assert.That(played, Is.EqualTo(5));
        }

        [Test]
        public void OneTake_PlaysEveryTime_AndNoneIsNone()
        {
            var picker = new TakePicker(new Random(1));

            Assert.That(picker.Next(1), Is.EqualTo(0));
            Assert.That(picker.Next(1), Is.EqualTo(0));
            Assert.That(picker.Next(0), Is.EqualTo(-1));
        }

        [Test]
        public void AStep_IsHeard_WhenASwingingFootStops()
        {
            var foot = new StepDetector();

            Assert.That(foot.Update(footSpeed: 0.1f, bodySpeed: 1.8f), Is.False, "Standing on it: no step yet.");
            Assert.That(foot.Update(3.5f, 1.8f), Is.False, "Swinging.");
            Assert.That(foot.Update(1.5f, 1.8f), Is.False, "Slowing...");
            Assert.That(foot.Update(0.2f, 1.8f), Is.True, "...down: a step.");
            Assert.That(foot.Update(0.1f, 1.8f), Is.False, "Still down: once.");
        }

        [Test]
        public void AFoot_ThatNeverSwings_NeverSteps_AndOneStandingStill_ForgetsItsSwing()
        {
            var foot = new StepDetector();
            Assert.That(foot.Update(1f, 1.8f) || foot.Update(0.1f, 1.8f), Is.False, "Shuffling slower than its body: no swing.");

            foot.Update(4f, 1.8f);
            foot.Reset();
            Assert.That(foot.Update(0.1f, 1.8f), Is.False);
            Assert.That(foot.Update(0.1f, 0f), Is.False, "A body that doesn't move takes no step.");
        }

        static BodyMotion Moving(float speed, bool crouched = false, bool grounded = true) =>
            new BodyMotion(speed, 0f, grounded, crouched);

        [Test]
        public void AGait_IsReadOffThePace()
        {
            Assert.That(Gaits.Of(Moving(0f), Paces), Is.EqualTo(Gait.Still));
            Assert.That(Gaits.Of(Moving(Paces.WalkSpeed), Paces), Is.EqualTo(Gait.Walk));
            Assert.That(Gaits.Of(Moving(Paces.RunSpeed), Paces), Is.EqualTo(Gait.Run));
            Assert.That(Gaits.Of(Moving(Paces.SprintSpeed), Paces), Is.EqualTo(Gait.Sprint));
            Assert.That(Gaits.Of(Moving(Paces.CrouchSpeed, crouched: true), Paces), Is.EqualTo(Gait.Sneak));
            Assert.That(Gaits.Of(Moving(Paces.RunSpeed, grounded: false), Paces), Is.EqualTo(Gait.Still), "In the air: no steps.");
        }

        [Test]
        public void AStep_TriesItsSurfaceFirst_ThenItsGait_GentlerOnesStandingIn()
        {
            var keys = FootstepKeys.For("wet", Gait.Sprint).Select(k => k.Key).ToArray();

            Assert.That(keys, Is.EqualTo(new[]
            {
                "footsteps/wet/sprint", "footsteps/wet/run", "footsteps/wet/walk", "footsteps/wet",
                "footsteps/sprint", "footsteps/run", "footsteps/walk",
            }));
            Assert.That(FootstepKeys.For(null, Gait.Sneak).Select(k => k.Key), Is.EqualTo(new[] { "footsteps/sneak", "footsteps/walk" }));
            Assert.That(FootstepKeys.For("grass", Gait.Still), Is.Empty);
        }

        [Test]
        public void AStepsOwnSound_PlaysAsItIs_AndAnothersIsTrodSofterOrHarder()
        {
            var sneak = FootstepKeys.For("wet", Gait.Sneak).ToDictionary(k => k.Key, k => k.Gain);

            Assert.That(sneak["footsteps/sneak"], Is.EqualTo(1f));
            Assert.That(sneak["footsteps/wet"], Is.LessThan(1f), "A walk's wet step, trod softly.");
            Assert.That(FootstepKeys.For(null, Gait.Sprint).First(k => k.Key == "footsteps/run").Gain, Is.GreaterThan(1f));
        }

        [Test]
        public void TheEars_HearFromThePlayersHead_TurnedAsTheCameraLooks_ElseFromTheCamera()
        {
            var camera = new UnityEngine.GameObject("Camera").transform;
            var ears = new SoundListener(camera);
            try
            {
                camera.SetPositionAndRotation(new UnityEngine.Vector3(0f, 3f, -4f), UnityEngine.Quaternion.Euler(20f, 90f, 0f));
                Assert.That(ears.Position, Is.EqualTo(camera.position), "No one plays: at the camera.");

                var head = new UnityEngine.Vector3(0f, 1.7f, 0f);
                ears.HearFrom(head);
                Assert.That(UnityEngine.Vector3.Distance(ears.Position, head), Is.LessThan(1e-4f), "Playing: at the head.");
                Assert.That(UnityEngine.Quaternion.Angle(ears.Rotation, camera.rotation), Is.LessThan(0.01f), "...turned the camera's way.");

                ears.HearFrom(null);
                Assert.That(UnityEngine.Vector3.Distance(ears.Position, camera.position), Is.LessThan(1e-4f), "Done playing: back at the camera.");
            }
            finally
            {
                ears.Dispose();
                UnityEngine.Object.DestroyImmediate(camera.gameObject);
            }
        }
    }
}
