using Forge.UnityEditor.Content;
using NUnit.Framework;

namespace Forge.Tests.Content
{
    public class HumanoidMovesTests
    {
        [Test]
        public void AMovesFolder_SaysWhatItIs_AnActionsSettingsWhereItPlays_ItsNameNothing()
        {
            Assert.That(HumanoidMoves.RoleOf("Locomotion/Run.fbx"), Is.EqualTo(MoveRole.Ground));
            Assert.That(HumanoidMoves.RoleOf("Locomotion/DrawnRun.fbx"), Is.EqualTo(MoveRole.Ground));
            Assert.That(HumanoidMoves.RoleOf("Air/Jump.fbx"), Is.EqualTo(MoveRole.Air));
            Assert.That(HumanoidMoves.RoleOf("Styles/Archery/BowShot.fbx"), Is.EqualTo(MoveRole.UpperBodyAction), "An action that doesn't say.");
            Assert.That(HumanoidMoves.RoleOf("Styles/Archery/BowDraw.fbx"), Is.EqualTo(MoveRole.UpperBodyAction), "Its name says nothing...");
            Assert.That(HumanoidMoves.RoleOf("Styles/Archery/BowDraw.fbx", Says("arms")), Is.EqualTo(MoveRole.ArmsAction), "...its settings do.");
            Assert.That(HumanoidMoves.RoleOf(@"Styles\Archery\BowIdle.fbx", Says("stance")), Is.EqualTo(MoveRole.Stance), "Either slash.");
            Assert.That(HumanoidMoves.RoleOf("Styles/OneHanded/SwordDeath.fbx", Says("wholeBody")), Is.EqualTo(MoveRole.FullBodyAction));
            Assert.That(HumanoidMoves.RoleOf("Emotes/Wave.fbx", Says("arms")), Is.EqualTo(MoveRole.ArmsAction), "Any folder of actions.");
            Assert.That(HumanoidMoves.RoleOf("Styles/OneHanded/SwordHit.fbx", Says("reaction")), Is.EqualTo(MoveRole.Reaction));
            Assert.That(HumanoidMoves.Loops(MoveRole.Reaction, "SwordHit"), Is.False, "A blow taken plays once.");
            Assert.That(HumanoidMoves.Loops(MoveRole.FullBodyAction, "SwordDeath"), Is.False);
        }

        [Test]
        public void ALocomotionOrAnAirMove_PlaysAsItsFolderSays_WhateverItsSettingsSay()
        {
            Assert.That(HumanoidMoves.RoleOf("Locomotion/Run.fbx", Says("arms")), Is.EqualTo(MoveRole.Ground));
            Assert.That(HumanoidMoves.RoleOf("Air/Jump.fbx", Says("wholeBody")), Is.EqualTo(MoveRole.Air));
            Assert.That(HumanoidMoves.IgnoresPlays(MoveRole.Ground, Says("arms")), Is.True, "The builder says so.");
            Assert.That(HumanoidMoves.IgnoresPlays(MoveRole.Ground, MoveSettings.None), Is.False);
            Assert.That(HumanoidMoves.IgnoresPlays(MoveRole.ArmsAction, Says("arms")), Is.False);
        }

        static MoveSettings Says(string plays) => MoveSettings.Parse($"{{ \"plays\": \"{plays}\" }}", "test.json");

        [Test]
        public void WhatIsBuilt_OrInNoFolder_IsNoMove()
        {
            Assert.That(HumanoidMoves.RoleOf("_Built/Humanoid.controller"), Is.Null);
            Assert.That(HumanoidMoves.RoleOf("_Reference/TPose.fbx"), Is.Null, "The skeleton the clips are read against is no move.");
            Assert.That(HumanoidMoves.RoleOf("Run.fbx"), Is.Null, "Loose in the set: no folder says what it is.");
            Assert.That(HumanoidMoves.RoleOf(null), Is.Null);
        }

        [Test]
        public void AFolderBesideTheSets_IsASet_UnlessItStartsWithAnUnderscore()
        {
            Assert.That(HumanoidMoves.IsSet("Humanoid"), Is.True);
            Assert.That(HumanoidMoves.IsSet("Guard"), Is.True);
            Assert.That(HumanoidMoves.IsSet(HumanoidMoves.ReferenceFolder), Is.False);
            Assert.That(HumanoidMoves.IsSet("_Built"), Is.False);
            Assert.That(HumanoidMoves.IsSet(""), Is.False);
        }

        [Test]
        public void GroundMovesAndStances_Loop_AsDoesAFall_ButNotAJumpOrAnAction()
        {
            Assert.That(HumanoidMoves.Loops(MoveRole.Ground, "Run"), Is.True);
            Assert.That(HumanoidMoves.Loops(MoveRole.Stance, "BowIdle"), Is.True);
            Assert.That(HumanoidMoves.Loops(MoveRole.Air, "Fall"), Is.True);
            Assert.That(HumanoidMoves.Loops(MoveRole.Air, "Jump"), Is.False);
            Assert.That(HumanoidMoves.Loops(MoveRole.UpperBodyAction, "SwordSlash"), Is.False);
            Assert.That(HumanoidMoves.Lifts(MoveRole.Air, "Jump"), Is.True);
            Assert.That(HumanoidMoves.Lifts(MoveRole.UpperBodyAction, "Jump"), Is.False);
        }

        [Test]
        public void TheDrawnCrouchedMoves_AreGroundMoves_NamedDrawnCrouch()
        {
            Assert.That(HumanoidMoves.DrawnCrouchedGround[0], Is.EqualTo("DrawnCrouchIdle"));
            Assert.That(HumanoidMoves.RoleOf("Locomotion/DrawnCrouchIdle.fbx"), Is.EqualTo(MoveRole.Ground));
            Assert.That(HumanoidMoves.Loops(MoveRole.Ground, "DrawnCrouchIdle"), Is.True);
            Assert.That(HumanoidMoves.Misplaced("DrawnCrouchIdle", MoveRole.Air), Is.EqualTo(HumanoidMoves.LocomotionFolder));
        }

        [Test]
        public void TheSidestep_IsAGroundMove_WalkedOnLoop_DrawnOrNot()
        {
            Assert.That(HumanoidMoves.DrawnStrafe, Is.EqualTo("DrawnStrafe"));
            Assert.That(HumanoidMoves.RoleOf("Locomotion/DrawnStrafe.fbx"), Is.EqualTo(MoveRole.Ground));
            Assert.That(HumanoidMoves.Loops(MoveRole.Ground, HumanoidMoves.Strafe), Is.True);
            Assert.That(HumanoidMoves.Misplaced(HumanoidMoves.Strafe, MoveRole.UpperBodyAction), Is.EqualTo(HumanoidMoves.LocomotionFolder));
            Assert.That(HumanoidMoves.Misplaced(HumanoidMoves.DrawnStrafe, MoveRole.Air), Is.EqualTo(HumanoidMoves.LocomotionFolder));
        }

        [Test]
        public void TheDrawnJump_IsAnAirMove_ThatLiftsAndPlaysOnce()
        {
            Assert.That(HumanoidMoves.DrawnJump, Is.EqualTo("DrawnJump"));
            Assert.That(HumanoidMoves.RoleOf("Air/DrawnJump.fbx"), Is.EqualTo(MoveRole.Air));
            Assert.That(HumanoidMoves.Lifts(MoveRole.Air, HumanoidMoves.DrawnJump), Is.True);
            Assert.That(HumanoidMoves.Loops(MoveRole.Air, HumanoidMoves.DrawnJump), Is.False);
            Assert.That(HumanoidMoves.Misplaced(HumanoidMoves.DrawnJump, MoveRole.Ground), Is.EqualTo(HumanoidMoves.AirFolder));
        }

        [Test]
        public void AKnownGroundOrAirMove_OutOfItsFolder_IsSaidToBelongThere()
        {
            Assert.That(HumanoidMoves.Misplaced("Run", MoveRole.UpperBodyAction), Is.EqualTo(HumanoidMoves.LocomotionFolder));
            Assert.That(HumanoidMoves.Misplaced("DrawnIdle", MoveRole.Stance), Is.EqualTo(HumanoidMoves.LocomotionFolder));
            Assert.That(HumanoidMoves.Misplaced("Land", MoveRole.Ground), Is.EqualTo(HumanoidMoves.AirFolder));
            Assert.That(HumanoidMoves.Misplaced("Run", MoveRole.Ground), Is.Null);
            Assert.That(HumanoidMoves.Misplaced("BowShot", MoveRole.UpperBodyAction), Is.Null);
        }

        [Test]
        public void CrouchedMoves_AreGroundMoves_WalkedOnLoop_ButGoingDownOrUpPlaysOnce()
        {
            Assert.That(HumanoidMoves.Crouched("Walk"), Is.EqualTo("CrouchWalk"));
            Assert.That(HumanoidMoves.RoleOf("Locomotion/CrouchIdle.fbx"), Is.EqualTo(MoveRole.Ground));
            Assert.That(HumanoidMoves.Loops(MoveRole.Ground, "CrouchWalk"), Is.True);
            Assert.That(HumanoidMoves.Loops(MoveRole.Ground, HumanoidMoves.CrouchDown), Is.False);
            Assert.That(HumanoidMoves.Loops(MoveRole.Ground, HumanoidMoves.StandUp), Is.False);
            Assert.That(HumanoidMoves.Misplaced("CrouchIdle", MoveRole.UpperBodyAction), Is.EqualTo(HumanoidMoves.LocomotionFolder));
            Assert.That(HumanoidMoves.Misplaced(HumanoidMoves.StandUp, MoveRole.UpperBodyAction), Is.EqualTo(HumanoidMoves.LocomotionFolder));
        }

        [Test]
        public void AClipsPlaceInItsSet_AndItsMove()
        {
            Assert.That(AnimationImport.InSet(AnimationImport.AnimationsFolder + "/Humanoid/Styles/Archery/BowShot.fbx"), Is.EqualTo("Styles/Archery/BowShot.fbx"));
            Assert.That(AnimationImport.InSet("Assets/Elsewhere/Run.fbx"), Is.Null);
            Assert.That(HumanoidMoves.MoveName("Styles/Archery/BowShot.fbx"), Is.EqualTo("BowShot"));
        }
    }
}
