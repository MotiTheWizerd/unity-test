using System;
using System.IO;
using System.Linq;
using Forge.Play;
using Forge.UnityEditor.Content;
using NUnit.Framework;

namespace Forge.Tests.Content
{
    public class MoveSettingsTests
    {
        static readonly string[] Names =
        {
            "Right Hand Points X", "Right Hand Points Y", "Right Hand Points Z",
            "Left Hand Points X", "Left Hand Points Y", "Left Hand Points Z",
        };

        [Test]
        public void TheJson_SitsBesideTheClip()
        {
            Assert.That(MoveSettings.PathFor("Assets/_Project/Content/Animations/Humanoid/Locomotion/DrawnIdle.fbx"),
                Is.EqualTo("Assets/_Project/Content/Animations/Humanoid/Locomotion/DrawnIdle.json"));
        }

        [Test]
        public void NoJson_SaysNothing_EveryCurveZero_RightHandFirst()
        {
            var settings = MoveSettings.Parse(null, "a.json");

            Assert.That(settings, Is.SameAs(MoveSettings.None));
            Assert.That((settings.Plays, settings.StaysAtEnd), Is.EqualTo(((MoveRole?)null, false)), "Its folder's way.");
            Assert.That(settings.Curves.Select(c => c.Name), Is.EqualTo(Names));
            Assert.That(settings.Curves.Select(c => c.Value), Is.All.EqualTo(0f));
            Assert.That(Names[0], Is.EqualTo(HumanoidAnimation.HandPoints(true, 0)));
            Assert.That(Names[5], Is.EqualTo(HumanoidAnimation.HandPoints(false, 2)));
        }

        [Test]
        public void ARightHandThatPoints_FillsItsCurves_TheLeftStaysZero()
        {
            var settings = MoveSettings.Parse("{ \"rightHand\": { \"points\": [0, 0.5, 1] } }", "a.json");

            Assert.That(settings.Curves.Select(c => c.Value), Is.EqualTo(new[] { 0f, 0.5f, 1f, 0f, 0f, 0f }));
            Assert.That(settings.RightHand, Is.EqualTo(new UnityEngine.Vector3(0f, 0.5f, 1f)));
        }

        [Test]
        public void AnEmptyJson_SaysNothing()
        {
            Assert.That(MoveSettings.Parse("{}", "a.json").Curves.Select(c => c.Value), Is.All.EqualTo(0f));
            Assert.That(MoveSettings.Parse("{ \"leftHand\": {} }", "a.json").Curves.Select(c => c.Value), Is.All.EqualTo(0f));
            Assert.That(MoveSettings.Parse("{}", "a.json").Plays, Is.Null);
        }

        [Test]
        public void WhereAnActionPlays_IsSaid_ByName()
        {
            Assert.That(MoveSettings.Parse("{ \"plays\": \"arms\" }", "a.json").Plays, Is.EqualTo(MoveRole.ArmsAction));
            Assert.That(MoveSettings.Parse("{ \"plays\": \"upperBody\" }", "a.json").Plays, Is.EqualTo(MoveRole.UpperBodyAction));
            Assert.That(MoveSettings.Parse("{ \"plays\": \"wholeBody\" }", "a.json").Plays, Is.EqualTo(MoveRole.FullBodyAction));
            Assert.That(MoveSettings.Parse("{ \"plays\": \"stance\" }", "a.json").Plays, Is.EqualTo(MoveRole.Stance));
            var death = MoveSettings.Parse("{ \"plays\": \"wholeBody\", \"staysAtEnd\": true }", "a.json");
            Assert.That((death.Plays, death.StaysAtEnd), Is.EqualTo(((MoveRole?)MoveRole.FullBodyAction, true)));
        }

        [Test]
        public void WhatCantBeMeant_IsRefused_NamingTheFile()
        {
            var points = Assert.Throws<InvalidOperationException>(() => MoveSettings.Parse("{ \"leftHand\": { \"points\": [1, 2] } }", "Locomotion/Idle.json"));
            Assert.That(points.Message, Does.Contain("Locomotion/Idle.json").And.Contain("leftHand"));

            var plays = Assert.Throws<InvalidOperationException>(() => MoveSettings.Parse("{ \"plays\": \"legs\" }", "Styles/A/Kick.json"));
            Assert.That(plays.Message, Does.Contain("Styles/A/Kick.json").And.Contain("legs").And.Contain("wholeBody"));

            var stays = Assert.Throws<InvalidOperationException>(() => MoveSettings.Parse("{ \"plays\": \"upperBody\", \"staysAtEnd\": true }", "Styles/A/Bow.json"));
            Assert.That(stays.Message, Does.Contain("Styles/A/Bow.json").And.Contain("staysAtEnd"), "Only the whole body's layer stays.");
            Assert.Throws<InvalidOperationException>(() => MoveSettings.Parse("{ \"staysAtEnd\": true }", "a.json"), "Nor without saying where it plays.");
        }

        [Test]
        public void WhatIsWritten_ReadsBack_AndSaysOnlyWhatIsSaid()
        {
            var json = MoveSettings.None.WithHands(new UnityEngine.Vector3(0f, 1f, -0.3f), UnityEngine.Vector3.zero).Write();

            Assert.That(json, Is.EqualTo("{ \"rightHand\": { \"points\": [0, 1, -0.3] } }\n"));
            Assert.That(MoveSettings.Parse(json, "a.json").Curves.Select(c => c.Value), Is.EqualTo(new[] { 0f, 1f, -0.3f, 0f, 0f, 0f }));
            Assert.That(MoveSettings.None.Write(), Is.EqualTo("{  }\n"));

            var death = MoveSettings.Parse("{ \"plays\": \"wholeBody\", \"staysAtEnd\": true }", "a.json").Write();
            Assert.That(death, Is.EqualTo("{ \"plays\": \"wholeBody\", \"staysAtEnd\": true }\n"));
        }

        [Test]
        public void TurningAHand_KeepsTheRestOfWhatTheMoveSays()
        {
            var stance = MoveSettings.Parse("{ \"plays\": \"stance\", \"leftHand\": { \"points\": [1, 0, 0] } }", "BowIdle.json");

            var turned = stance.WithHands(new UnityEngine.Vector3(0f, 0f, 1f), stance.LeftHand);

            Assert.That((turned.Plays, turned.RightHand, turned.LeftHand),
                Is.EqualTo(((MoveRole?)MoveRole.Stance, new UnityEngine.Vector3(0f, 0f, 1f), new UnityEngine.Vector3(1f, 0f, 0f))));
            Assert.That(MoveSettings.Parse(turned.Write(), "BowIdle.json").Plays, Is.EqualTo(MoveRole.Stance), "The hold stage's save keeps it a stance.");
        }

        [Test]
        public void ReadingAClipWithoutAJson_SaysNothing()
        {
            var clip = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "Idle.fbx");

            Assert.That(MoveSettings.Read(clip), Is.SameAs(MoveSettings.None));
        }
    }
}
