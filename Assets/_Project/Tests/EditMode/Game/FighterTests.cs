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
        static readonly ArcherySettings Bow = new ArcherySettings(range: 25f, standOff: 5f, spacedAt: 8f, minPause: 0.6f, maxPause: 1.8f,
            minAim: 0.3f, maxAim: 1f, spread: 1.5f);

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
            new Fighter(day ?? new Stroll(), settings ?? Settings, Bow, new Random(seed));

        static MindSight Fighting(Vector2? opponent, Readiness weapons = Readiness.Drawn, bool rightBusy = false, bool guardUp = false) =>
            new MindSight(Here, Mode.Fight, opponent, weapons, rightBusy, guardUp);

        // In a fight with a bow drawn (held in both hands): its right hand drawing or loosing while busy, the arrow so far drawn.
        static MindSight Shooting(Vector2? opponent, bool rightBusy = false, float drawPull = 0f) =>
            new MindSight(Here, Mode.Fight, opponent, Readiness.Drawn, rightBusy, false, shoots: true, drawPull: drawPull);

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
        public void Crowded_ItStepsBack_FacingItsFoe_ToFightingDistance_ThenHoldsItsGround()
        {
            var spaced = new FighterSettings(1.4f, 1.5f, 4f, 5f, 0.8f, 2f, 0f, 1.2f, standOff: 1f);
            Assert.That(spaced.SpacedAt, Is.EqualTo(1.2f).Within(1e-5f));
            var fighter = Make(spaced);
            var north = new Vector2(0f, 1f);

            // Its foe 0.8 m north: it walks south, still facing north.
            var crowded = fighter.Decide(Fighting(Here + north * 0.8f), Frame);
            Assert.That(crowded.Velocity, Is.EqualTo(-north * spaced.WalkSpeed));
            Assert.That(crowded.Facing, Is.EqualTo(0f).Within(1e-3f));

            // Past its stand-off but short of fighting distance, it keeps stepping back...
            Assert.That(fighter.Decide(Fighting(Here + north * 1.1f), Frame).Velocity, Is.EqualTo(-north * spaced.WalkSpeed));

            // ...and at fighting distance it stands; nearer again but past its stand-off, it holds its ground.
            Assert.That(fighter.Decide(Fighting(Here + north * 1.2f), Frame).Velocity, Is.EqualTo(Vector2.Zero));
            Assert.That(fighter.Decide(Fighting(Here + north * 1.1f), Frame).Velocity, Is.EqualTo(Vector2.Zero));
        }

        [Test]
        public void Crowded_MidSwing_TheBlowPlaysOutWhereItBegan_AndAGuardUpBacksAway()
        {
            var spaced = new FighterSettings(1.4f, 1.5f, 4f, 5f, 0.8f, 2f, 0f, 1.2f, standOff: 1f);
            var fighter = Make(spaced);
            var foe = Here + new Vector2(0.7f, 0f);

            Assert.That(fighter.Decide(Fighting(foe, rightBusy: true), Frame).Velocity, Is.EqualTo(Vector2.Zero));
            var guarded = fighter.Decide(Fighting(foe, guardUp: true), Frame);
            Assert.That(guarded.Velocity, Is.EqualTo(new Vector2(-spaced.WalkSpeed, 0f)), "Its shield up, it backs away behind it.");
            Assert.That(guarded.ReleaseLeft, Is.False);
        }

        [Test]
        public void WithNoStandOff_ItNeverGivesGround()
        {
            var fighter = Make();

            Assert.That(fighter.Decide(Fighting(Here + new Vector2(0f, 0.3f)), Frame).Velocity, Is.EqualTo(Vector2.Zero));
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
        public void WithABow_OutOfRange_ItClosesIn_AndInRange_ItStandsFacingTheFoe()
        {
            var fighter = Make();

            var far = fighter.Decide(Shooting(Here + new Vector2(0f, -40f)), Frame);
            var inRange = fighter.Decide(Shooting(Here + new Vector2(15f, 0f)), Frame);

            Assert.That(far.Velocity, Is.EqualTo(new Vector2(0f, -Settings.RunSpeed)));
            Assert.That(far.Facing, Is.EqualTo(180f).Within(1e-3f));
            Assert.That(far.HandsBusy, Is.False);
            Assert.That(inRange.Velocity, Is.EqualTo(Vector2.Zero));
            Assert.That(inRange.Facing, Is.EqualTo(90f).Within(1e-3f));
        }

        [Test]
        public void WithABow_ItDraws_AimsAWhileAtFullDraw_AndLooses_EachShotItsOwnWayOff()
        {
            const float nock = 0.2f, pullSeconds = 0.6f, letGo = 0.5f;
            var fighter = Make();
            var foe = Here + new Vector2(0f, 15f);
            var aims = new List<Vector2>();
            var drawing = false;
            var drawnAt = -1f;
            var fullSeen = -1f;
            var freeAt = -1f;
            var busyUntil = -1f;
            Vector2? aim = null;
            for (var t = 0f; t < 30f; t += Frame)
            {
                // Its body: the arrow on the string after the nock, drawn to full over the pull; let go, the bow comes down.
                var pull = drawing ? Math.Max(0f, Math.Min(1f, (t - drawnAt - nock) / pullSeconds)) : 0f;
                var busy = drawing || t < busyUntil;
                var decision = fighter.Decide(Shooting(foe, busy, pull), Frame);
                Assert.That(decision.Velocity, Is.EqualTo(Vector2.Zero));
                Assert.That(decision.Facing, Is.EqualTo(0f).Within(1e-3f));
                if (drawing && pull >= 1f && fullSeen < 0f)
                    fullSeen = t;

                if (decision.UseRight)
                {
                    Assert.That(busy, Is.False, "No draw while the last shot plays out.");
                    Assert.That(decision.Aim, Is.Not.Null, "It aims from the draw's start.");
                    Assert.That(decision.Aim.Value.Length(), Is.LessThanOrEqualTo(Bow.Spread + 1e-4f));
                    if (freeAt >= 0f)
                        Assert.That(t - freeAt, Is.InRange(Bow.MinPause - Frame, Bow.MaxPause + 2 * Frame), "A pause between shots.");
                    drawing = true;
                    drawnAt = t;
                    fullSeen = -1f;
                    aim = decision.Aim;
                    aims.Add(aim.Value);
                    continue;
                }

                if (!drawing)
                {
                    Assert.That(decision.Aim, Is.Null, "Not drawing, it doesn't aim.");
                    continue;
                }

                Assert.That(decision.Aim, Is.EqualTo(aim), "One shot, one way off.");
                if (!decision.ReleaseRight)
                    continue;
                Assert.That(pull, Is.EqualTo(1f), "Loosed only at full draw.");
                Assert.That(t - fullSeen, Is.InRange(Bow.MinAim - Frame, Bow.MaxAim + Frame), "Aimed a while first.");
                drawing = false;
                busyUntil = freeAt = t + letGo;
            }

            Assert.That(aims.Count, Is.InRange(6, 20), "Many shots in 30 s, each a draw, an aim, a loose and a pause.");
            Assert.That(new HashSet<Vector2>(aims).Count, Is.EqualTo(aims.Count), "Each shot strays its own way.");
        }

        [Test]
        public void WithABow_Crowded_ItBacksAway_FacingItsFoe_StillShooting_ThenHoldsItsGround()
        {
            var north = new Vector2(0f, 1f);
            var fighter = Make();

            var crowded = fighter.Decide(Shooting(Here + north * 3f), Frame);
            Assert.That(crowded.Velocity, Is.EqualTo(-north * Settings.WalkSpeed));
            Assert.That(crowded.Facing, Is.EqualTo(0f).Within(1e-3f));
            Assert.That(fighter.Decide(Shooting(Here + north * 6f), Frame).Velocity, Is.EqualTo(-north * Settings.WalkSpeed),
                "Past its stand-off it backs on...");
            Assert.That(fighter.Decide(Shooting(Here + north * 8f), Frame).Velocity, Is.EqualTo(Vector2.Zero), "...to where it is spaced,");
            Assert.That(fighter.Decide(Shooting(Here + north * 6f), Frame).Velocity, Is.EqualTo(Vector2.Zero), "then holds its ground.");

            var backing = Make();
            var drewBacking = false;
            for (var t = 0f; t < 5f && !drewBacking; t += Frame)
            {
                var decision = backing.Decide(Shooting(Here + north * 3f), Frame);
                drewBacking = decision.UseRight && decision.Velocity == -north * Settings.WalkSpeed;
            }

            Assert.That(drewBacking, Is.True, "It draws as it backs away.");
        }

        [Test]
        public void WithABow_ADrawUnderWay_IsSeenThrough_WhereverItsFoeGoes()
        {
            var fighter = Make();
            var near = Here + new Vector2(0f, 15f);
            var t = 0f;
            for (; t < 5f && !fighter.Decide(Shooting(near), Frame).UseRight; t += Frame)
            {
            }

            Assert.That(t, Is.LessThan(5f), "It drew.");
            var gone = Here + new Vector2(0f, 40f);
            var drawing = fighter.Decide(Shooting(gone, rightBusy: true, drawPull: 0.5f), Frame);
            Assert.That(drawing.Velocity, Is.EqualTo(Vector2.Zero), "It doesn't chase mid-draw.");
            Assert.That(drawing.Aim, Is.Not.Null);
            Assert.That(drawing.ReleaseRight, Is.False);

            var loosed = false;
            for (var s = 0f; s < 2f && !loosed; s += Frame)
                loosed = fighter.Decide(Shooting(gone, rightBusy: true, drawPull: 1f), Frame).ReleaseRight;
            Assert.That(loosed, Is.True, "It looses at its foe even out of range.");
            Assert.That(fighter.Decide(Shooting(gone), Frame).Velocity, Is.EqualTo(new Vector2(0f, Settings.RunSpeed)), "Then closes in.");
        }

        [Test]
        public void WithABow_ADrawThatNeverCame_ItWaitsAndTriesAgain()
        {
            var fighter = Make();
            var foe = Here + new Vector2(0f, 15f);
            var tries = new List<float>();
            for (var t = 0f; t < 10f; t += Frame)
            {
                // No arrows: the draw never starts, the hand stays free.
                var decision = fighter.Decide(Shooting(foe), Frame);
                Assert.That(decision.ReleaseRight, Is.False);
                if (decision.UseRight)
                    tries.Add(t);
                else
                    Assert.That(decision.Aim, Is.Null);
            }

            Assert.That(tries.Count, Is.InRange(4, 17));
            for (var i = 1; i < tries.Count; i++)
                Assert.That(tries[i] - tries[i - 1], Is.InRange(Bow.MinPause - Frame, Bow.MaxPause + 2 * Frame));
        }

        [Test]
        public void ArcherySettings_RefuseNonsense()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ArcherySettings(0f, 0f, 1f, 1f, 2f, 0.3f, 1f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ArcherySettings(25f, 9f, 8f, 1f, 2f, 0.3f, 1f, 1f), "Spaced beyond its stand-off.");
            Assert.Throws<ArgumentOutOfRangeException>(() => new ArcherySettings(25f, 5f, 30f, 1f, 2f, 0.3f, 1f, 1f), "Spaced within its range.");
            Assert.Throws<ArgumentOutOfRangeException>(() => new ArcherySettings(25f, 5f, 8f, 3f, 2f, 0.3f, 1f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ArcherySettings(25f, 5f, 8f, 1f, 2f, 1.3f, 1f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ArcherySettings(25f, 5f, 8f, 1f, 2f, 0.3f, 1f, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ArcherySettings(25f, 5f, 8f, 1f, 2f, 0.3f, 1f, 45f));
            Assert.DoesNotThrow(() => new ArcherySettings(25f, 0f, 1f, 0f, 0f, 0f, 0f, 0f), "It may never give ground, and shoot true.");
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
            Assert.Throws<ArgumentOutOfRangeException>(() => new FighterSettings(1f, 1f, 2f, 5f, 1f, 2f, 0.5f, 1f, standOff: 1f), "Short of its reach.");
            Assert.Throws<ArgumentOutOfRangeException>(() => new FighterSettings(1f, 1f, 2f, 5f, 1f, 2f, 0.5f, 1f, standOff: -0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => Make().Decide(MindSight.AtEase(Here), 0f));
        }
    }
}
