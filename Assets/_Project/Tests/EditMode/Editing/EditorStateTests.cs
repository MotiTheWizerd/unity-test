using System;
using System.Numerics;
using Forge.Data.Sky;
using Forge.Editing.Placement;
using Forge.Editing.State;
using NUnit.Framework;

namespace Forge.Tests.Editing
{
    public class EditorStateTests
    {
        [Test]
        public void SavedPose_RoundTripsPositionAndRotation()
        {
            var rotation = Quaternion.CreateFromYawPitchRoll(0.5f, 0.25f, 0f);

            var pose = SavedPose.Of(new Vector3(10, 50, -2000), rotation);

            Assert.That(pose.IsValid, Is.True);
            Assert.That(pose.Position, Is.EqualTo(new Vector3(10, 50, -2000)));
            Assert.That(Quaternion.Dot(pose.Rotation, rotation), Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void SavedPose_WithoutAUsableRotation_IsUpright()
        {
            var pose = new SavedPose { RotationX = 0, RotationY = 0, RotationZ = 0, RotationW = 0 };

            Assert.That(pose.Rotation, Is.EqualTo(Quaternion.Identity));
        }

        [Test]
        public void SavedPose_WithANonFiniteNumber_IsNotValid()
        {
            Assert.That(new SavedPose { Y = float.NaN }.IsValid, Is.False);
            Assert.That(new SavedPose { RotationW = float.PositiveInfinity }.IsValid, Is.False);
        }

        [Test]
        public void SavedClock_RoundTripsTheClock()
        {
            var clock = new WorldClock(6.5f) { Day = 12, Running = true };

            var restored = new WorldClock();
            SavedClock.Of(clock).ApplyTo(restored);

            Assert.That(restored.Day, Is.EqualTo(12));
            Assert.That(restored.Hour, Is.EqualTo(6.5f));
            Assert.That(restored.Running, Is.True);
        }

        [Test]
        public void SavedClock_SkipsValuesAClockCannotHold()
        {
            var clock = new WorldClock(9f) { Day = 4 };

            new SavedClock { Day = -1, Hour = float.NaN }.ApplyTo(clock);

            Assert.That(clock.Day, Is.EqualTo(4));
            Assert.That(clock.Hour, Is.EqualTo(9f));
        }

        [Test]
        public void SavedGap_IsClampedToWhatARowGapCanBe()
        {
            var gap = new SavedGap { Gap = -5, RandomExtra = 1000 }.ToRowGap();

            Assert.That(gap.Gap, Is.EqualTo(0f));
            Assert.That(gap.RandomExtra, Is.EqualTo(RowGap.Max));
            Assert.That(new SavedGap { Gap = float.NaN }.ToRowGap().Gap, Is.EqualTo(0f));
        }

        [Test]
        public void PlacementOptions_GapsSetById_AreListedAndUsed()
        {
            var options = new PlacementOptions(new Random(1));
            var fence = Forge.Tests.Data.TestRecords.Base("core:fence");

            options.SetGap(fence.Id, new RowGap(1.5f, 0.5f));

            Assert.That(options.Gaps[fence.Id].Gap, Is.EqualTo(1.5f));
            Assert.That(options.GapFor(fence).RandomExtra, Is.EqualTo(0.5f));
        }
    }
}
