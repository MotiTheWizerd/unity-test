using System;
using Forge.Data;
using Forge.Data.Records;
using Forge.Editing.Placement;
using NUnit.Framework;

namespace Forge.Tests.Editing
{
    public class PlacementOptionsTests
    {
        static readonly BaseObjectRecord Tree = new BaseObjectRecord(RecordId.Parse("core:tree"), "Tree", "tree",
            placement: new PlacementRules(true, 0.8f, 1.2f, alignToSlope: false));

        static readonly BaseObjectRecord Fence = new BaseObjectRecord(RecordId.Parse("core:fence"), "Fence", "fence",
            placement: new PlacementRules(false, 1f, 1f, alignToSlope: true));

        static readonly BaseObjectRecord Crate = new BaseObjectRecord(RecordId.Parse("core:crate"), "Crate", "crate");

        static PlacementOptions NewOptions() => new PlacementOptions(new Random(7));

        [Test]
        public void DrawRoll_FollowsTheRules_OnlyWhileRandomizing()
        {
            var options = NewOptions();

            for (var i = 0; i < 20; i++)
            {
                var roll = options.DrawRoll(Tree);
                Assert.That(roll.Scale, Is.InRange(0.8f, 1.2f));
            }

            Assert.That(options.DrawRoll(Crate), Is.EqualTo(PlacementRoll.Identity), "No rules, no variation.");

            options.Randomize = false;
            Assert.That(options.DrawRoll(Tree), Is.EqualTo(PlacementRoll.Identity));
        }

        [Test]
        public void KeepsOwnHeading_OnlyForRandomHeadings_WhileRandomizing()
        {
            var options = NewOptions();

            Assert.That(options.KeepsOwnHeading(Tree), Is.True);
            Assert.That(options.KeepsOwnHeading(Fence), Is.False);

            options.Randomize = false;
            Assert.That(options.KeepsOwnHeading(Tree), Is.False);
        }

        [Test]
        public void TiltsToSlope_NeedsTheRuleAndTheToggle()
        {
            var options = NewOptions();

            Assert.That(options.TiltsToSlope(Fence), Is.True);
            Assert.That(options.TiltsToSlope(Tree), Is.False);
            Assert.That(options.TiltsToSlope(Crate), Is.False);

            options.AlignToSlope = false;
            Assert.That(options.TiltsToSlope(Fence), Is.False);
        }

        [Test]
        public void Gaps_DefaultByKind_AndAreRememberedPerObject()
        {
            var options = NewOptions();

            Assert.That(options.GapFor(Fence), Is.EqualTo(new RowGap(0f, 0f)), "Built things meet end to end.");
            Assert.That(options.GapFor(Tree), Is.EqualTo(new RowGap(1f, 2f)), "Natural things get room.");

            options.SetGap(Fence, new RowGap(0.5f, 0f));

            Assert.That(options.GapFor(Fence), Is.EqualTo(new RowGap(0.5f, 0f)));
            Assert.That(options.GapFor(Tree), Is.EqualTo(new RowGap(1f, 2f)));
        }

        [Test]
        public void DrawGap_StaysWithinTheGapAndItsRandomExtra()
        {
            var options = NewOptions();

            for (var i = 0; i < 20; i++)
                Assert.That(options.DrawGap(new RowGap(1f, 2f)), Is.InRange(1f, 3f));
        }

        [Test]
        public void RowGap_RejectsOutOfRange_AndClampsFreeInput()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RowGap(-1f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RowGap(0f, RowGap.Max + 1f));

            Assert.That(RowGap.Clamped(-3f, 99f), Is.EqualTo(new RowGap(0f, RowGap.Max)));
        }
    }
}
