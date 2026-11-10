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
        public void NoJson_EveryCurveIsZero_RightHandFirst()
        {
            var curves = MoveSettings.Parse(null, "a.json");

            Assert.That(curves.Select(c => c.Name), Is.EqualTo(Names));
            Assert.That(curves.Select(c => c.Value), Is.All.EqualTo(0f));
            Assert.That(Names[0], Is.EqualTo(HumanoidAnimation.HandPoints(true, 0)));
            Assert.That(Names[5], Is.EqualTo(HumanoidAnimation.HandPoints(false, 2)));
        }

        [Test]
        public void ARightHandThatPoints_FillsItsCurves_TheLeftStaysZero()
        {
            var curves = MoveSettings.Parse("{ \"rightHand\": { \"points\": [0, 0.5, 1] } }", "a.json");

            Assert.That(curves.Select(c => c.Value), Is.EqualTo(new[] { 0f, 0.5f, 1f, 0f, 0f, 0f }));
        }

        [Test]
        public void AnEmptyJson_SaysNothing()
        {
            Assert.That(MoveSettings.Parse("{}", "a.json").Select(c => c.Value), Is.All.EqualTo(0f));
            Assert.That(MoveSettings.Parse("{ \"leftHand\": {} }", "a.json").Select(c => c.Value), Is.All.EqualTo(0f));
        }

        [Test]
        public void PointsThatAreNotThreeNumbers_AreRefused_NamingTheFile()
        {
            var refusal = Assert.Throws<InvalidOperationException>(() => MoveSettings.Parse("{ \"leftHand\": { \"points\": [1, 2] } }", "Locomotion/Idle.json"));

            Assert.That(refusal.Message, Does.Contain("Locomotion/Idle.json").And.Contain("leftHand"));
        }

        [Test]
        public void WhatIsWritten_ReadsBack()
        {
            var json = MoveSettings.Write(new UnityEngine.Vector3(0f, 1f, -0.3f), UnityEngine.Vector3.zero);

            Assert.That(json, Is.EqualTo("{ \"rightHand\": { \"points\": [0, 1, -0.3] } }\n"));
            Assert.That(MoveSettings.Parse(json, "a.json").Select(c => c.Value), Is.EqualTo(new[] { 0f, 1f, -0.3f, 0f, 0f, 0f }));
            Assert.That(MoveSettings.Write(UnityEngine.Vector3.zero, UnityEngine.Vector3.zero), Is.EqualTo("{  }\n"));
        }

        [Test]
        public void ReadingAClipWithoutAJson_IsAllZero()
        {
            var clip = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "Idle.fbx");

            Assert.That(MoveSettings.Read(clip).Select(c => c.Value), Is.All.EqualTo(0f));
        }
    }
}
