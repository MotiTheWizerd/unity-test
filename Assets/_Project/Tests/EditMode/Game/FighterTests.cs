using System;
using System.Collections.Generic;
using System.Numerics;
using Forge.Game.Actors;
using Forge.Game.Equipment;
using Forge.Game.Minds;
using NUnit.Framework;

namespace Forge.Tests.Game
{
    public class FighterTests
    {
        const float Frame = 0.02f;
        static readonly Vector2 Here = new Vector2(10f, -4f);
        static readonly FighterSettings Settings = new FighterSettings(reach: 1.4f, walkSpeed: 1.5f, runSpeed: 4f, runBeyond: 5f,
            minPause: 0.8f, maxPause: 2f, blockChance: 0f, blockSeconds: 1.2f);

        // Its day: always strolling north at 1 m/s.
        sealed class Stroll : IMind
        {
            public int Asked;

            public MindDecision Decide(MindSight sight, float deltaTime)
            {
                Asked++;
                return MindDecision.Walk(new Vector2(0f, 1f));
            }
        }

        static Fighter Make(FighterSettings? settings = null, Stroll day = null, int seed = 1) =>
            new Fighter(day ?? new Stroll(), settings ?? Settings, new Random(seed));

        static MindSight Fighting(Vector2? opponent, Readiness weapons = Readiness.Drawn, bool rightBusy = false, bool guardUp = false) =>
            new MindSight(Here, Mode.Fight, opponent, weapons, rightBusy, guardUp);

        [Test]
        public void AtEase_ItsDayGoesOn_WeaponsAway()
        {
            var day = new Stroll();
            var fighter = Make(day: day);

            var decision = fighter.Decide(MindSight.AtEase(Here), Frame);

            Assert.That(day.Asked, Is.EqualTo(1));
            Assert.That(decision.Velocity, Is.EqualTo(new Vector2(0f, 1f)));
            Assert.That(decision.HandsBusy, Is.False);
        }

        [Test]
        public void AFightBegins_ItDrawsFirst_StandingAndFacingTheFoe()
        {
            var fighter = Make();
            var east = Here + new Vector2(3f, 0f);

            var sheathed = fighter.Decide(Fighting(east, Readiness.Sheathed), Frame);
            var drawing = fighter.Decide(Fighting(east, Readiness.Drawing), Frame);

            Assert.That(sheathed.ReadyWeapon, Is.True);
            Assert.That(sheathed.Velocity, Is.EqualTo(Vector2.Zero));
            Assert.That(sheathed.Facing, Is.EqualTo(90f).Within(1e-3f));
            Assert.That(drawing.ReadyWeapon, Is.False, "A draw under way is left to finish.");
            Assert.That(drawing.Velocity, Is.EqualTo(Vector2.Zero));
            Assert.That(drawing.Facing, Is.EqualTo(90f).Within(1e-3f));
        }

        [Test]
        public void Drawn_AndOutOfReach_ItClosesIn_WalkingNear_RunningFromAfar()
        {
            var fighter = Make();

            var near = fighter.Decide(Fighting(Here + new Vector2(3f, 0f)), Frame);
            var far = fighter.Decide(Fighting(Here + new Vector2(0f, -20f)), Frame);

            Assert.That(near.Velocity, Is.EqualTo(new Vector2(Settings.WalkSpeed, 0f)));
            Assert.That(near.Facing, Is.EqualTo(90f).Within(1e-3f));
            Assert.That(near.HandsBusy, Is.False);
            Assert.That(far.Velocity, Is.EqualTo(new Vector2(0f, -Settings.RunSpeed)));
            Assert.That(far.Facing, Is.EqualTo(180f).Within(1e-3f));
        }

        [Test]
        public void ClosingIn_WithTheShieldUp_ItLowersIt()
        {
            var fighter = Make();

            var decision = fighter.Decide(Fighting(Here + new Vector2(3f, 0f), guardUp: true), Frame);

            Assert.That(decision.ReleaseLeft, Is.True);
        }

        [Test]
        public void InReach_ItStandsFacingTheFoe_AndStrikes_PausingBetweenBlows_NeverMidSwing()
        {
            var fighter = Make();
            var foe = Here + new Vector2(0f, 1f);
            var blows = new List<float>();
            var busyUntil = -1f;
            for (var t = 0f; t < 20f; t += Frame)
            {
                var busy = t < busyUntil;
                var decision = fighter.Decide(Fighting(foe, rightBusy: busy), Frame);
                Assert.That(decision.Velocity, Is.EqualTo(Vector2.Zero));
                Assert.That(decision.Facing, Is.EqualTo(0f).Within(1e-3f));
                if (!decision.UseRight)
                    continue;
                Assert.That(busy, Is.False, "No blow while the hand is still swinging.");
                blows.Add(t);
                busyUntil = t + 0.6f;
            }

            Assert.That(blows.Count, Is.InRange(6, 16), "Several blows in 20 s, at 0.8 to 2 s apart plus the swing.");
            for (var i = 1; i < blows.Count; i++)
                Assert.That(blows[i] - blows[i - 1], Is.InRange(0.6f + Settings.MinPause - Frame, 0.6f + Settings.MaxPause + Frame));
        }

        [Test]
        public void NowAndThen_ItRaisesTheShieldInstead_HoldsIt_ThenLowersIt()
        {
            var always = new FighterSettings(1.4f, 1.5f, 4f, 5f, 0.5f, 0.5f, blockChance: 1f, blockSeconds: 1f);
            var fighter = Make(always);
            var foe = Here + new Vector2(1f, 0f);
            var raised = -1f;
            var lowered = -1f;
            var guardUp = false;
            for (var t = 0f; t < 5f && lowered < 0f; t += Frame)
            {
                var decision = fighter.Decide(Fighting(foe, guardUp: guardUp), Frame);
                Assert.That(decision.UseRight, Is.False, "It blocks every time here.");
                if (decision.UseLeft)
                {
                    raised = t;
                    guardUp = true;
                }

                if (decision.ReleaseLeft)
                    lowered = t;
            }

            Assert.That(raised, Is.EqualTo(0.5f).Within(Frame * 2));
            Assert.That(lowered - raised, Is.EqualTo(1f).Within(Frame * 2));
        }

        [Test]
        public void ItsFoeGone_ItStandsReady_TheShieldDown()
        {
            var fighter = Make();

            var decision = fighter.Decide(Fighting(null, guardUp: true), Frame);

            Assert.That(decision.Velocity, Is.EqualTo(Vector2.Zero));
            Assert.That(decision.Facing, Is.Null);
            Assert.That(decision.ReleaseLeft, Is.True);
            Assert.That(decision.UseRight | decision.UseLeft | decision.ReadyWeapon, Is.False);
        }

        [Test]
        public void TheFightOver_ItSheathes_ThenGoesBackToItsDay()
        {
            var day = new Stroll();
            var fighter = Make(day: day);

            var drawn = fighter.Decide(new MindSight(Here, Mode.Normal, null, Readiness.Drawn, false, true), Frame);
            var sheathing = fighter.Decide(new MindSight(Here, Mode.Normal, null, Readiness.Sheathing, false, false), Frame);
            var sheathed = fighter.Decide(MindSight.AtEase(Here), Frame);

            Assert.That(drawn.ReadyWeapon, Is.True);
            Assert.That(drawn.ReleaseLeft, Is.True);
            Assert.That(drawn.Velocity, Is.EqualTo(Vector2.Zero));
            Assert.That(sheathing.HandsBusy, Is.False);
            Assert.That(sheathing.Velocity, Is.EqualTo(Vector2.Zero));
            Assert.That(day.Asked, Is.EqualTo(1), "Its day went on only once the weapons were away.");
            Assert.That(sheathed.Velocity, Is.EqualTo(new Vector2(0f, 1f)));
        }

        [Test]
        public void Settings_RefuseNonsense()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new FighterSettings(0f, 1f, 2f, 5f, 1f, 2f, 0.5f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FighterSettings(1f, 3f, 2f, 5f, 1f, 2f, 0.5f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FighterSettings(1f, 1f, 2f, 0.5f, 1f, 2f, 0.5f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FighterSettings(1f, 1f, 2f, 5f, 3f, 2f, 0.5f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FighterSettings(1f, 1f, 2f, 5f, 1f, 2f, 1.5f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FighterSettings(1f, 1f, 2f, 5f, 1f, 2f, 0.5f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => Make().Decide(MindSight.AtEase(Here), 0f));
        }
    }
}
