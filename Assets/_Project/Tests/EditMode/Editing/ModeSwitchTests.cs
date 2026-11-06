using System;
using System.Collections.Generic;
using Forge.Data.Records;
using Forge.Editing;
using Forge.Editing.Modes;
using NUnit.Framework;
using static Forge.Tests.Data.TestRecords;

namespace Forge.Tests.Editing
{
    public class ModeSwitchTests
    {
        static EditSession NewSession() =>
            new EditSession(
                new[] { Plugin("core", Base("core:tree", "Oak")) },
                Plugin("user", new[] { "core" }));

        static PlacedObjectRecord NewTree(EditSession session, float x = 0f) =>
            Placed(session.NewId("ref").ToString(), "core:tree", x);

        [Test]
        public void Starts_Editing()
        {
            var modes = new ModeSwitch(NewSession());

            Assert.That(modes.Mode, Is.EqualTo(AppMode.Editing));
            Assert.That(modes.IsPlaying, Is.False);
        }

        [Test]
        public void Playing_FreezesTheWorld_AndEditingThawsIt()
        {
            var session = NewSession();
            var modes = new ModeSwitch(session);

            Assert.That(modes.TryPlay(out var refusal), Is.True);
            Assert.That(refusal, Is.Null);
            Assert.That(modes.Mode, Is.EqualTo(AppMode.Playing));
            Assert.That(session.IsFrozen, Is.True);
            var edit = Assert.Throws<InvalidOperationException>(() => session.Put(NewTree(session)));
            Assert.That(edit.Message, Does.Contain("playing"), "The refusal says why.");

            modes.Edit();

            Assert.That(modes.Mode, Is.EqualTo(AppMode.Editing));
            Assert.That(session.IsFrozen, Is.False);
            Assert.DoesNotThrow(() => session.Put(NewTree(session)));
        }

        [Test]
        public void Playing_NeverChangesTheWorld()
        {
            var session = NewSession();
            session.Put(NewTree(session));
            var before = session.World;
            var modes = new ModeSwitch(session);

            modes.TryPlay(out _);
            Assert.Throws<InvalidOperationException>(() => session.Put(NewTree(session, 5f)));
            Assert.Throws<InvalidOperationException>(() => session.Batch("Two trees", () => session.Put(NewTree(session, 9f))));
            Assert.Throws<InvalidOperationException>(() => session.Undo());
            Assert.Throws<InvalidOperationException>(() => session.BeginDraft());
            modes.Edit();

            Assert.That(session.World, Is.SameAs(before), "Nothing tried while playing reached the world.");
            Assert.That(session.UndoDescription, Is.Not.Null, "The history is as it was.");
        }

        [Test]
        public void Redo_WaitsWhilePlaying()
        {
            var session = NewSession();
            session.Put(NewTree(session));
            session.Undo();
            var modes = new ModeSwitch(session);

            modes.TryPlay(out _);

            Assert.Throws<InvalidOperationException>(() => session.Redo());
            modes.Edit();
            Assert.That(session.Redo(), Is.True, "The redo is still there afterwards.");
        }

        [Test]
        public void Play_WaitsWhileTheAgentsDraftIsOpen()
        {
            var session = NewSession();
            var modes = new ModeSwitch(session);
            var draft = session.BeginDraft();

            Assert.That(modes.TryPlay(out var refusal), Is.False);
            Assert.That(refusal, Does.Contain("agent"));
            Assert.That(modes.IsPlaying, Is.False);
            Assert.That(session.IsFrozen, Is.False);

            draft.Discard();
            Assert.That(modes.TryPlay(out _), Is.True);
        }

        [Test]
        public void Changed_IsRaisedOncePerSwitch()
        {
            var modes = new ModeSwitch(NewSession());
            var seen = new List<AppMode>();
            modes.Changed += seen.Add;

            modes.TryPlay(out _);
            modes.TryPlay(out _);
            modes.Edit();
            modes.Edit();

            Assert.That(seen, Is.EqualTo(new[] { AppMode.Playing, AppMode.Editing }));
        }

        [Test]
        public void Freeze_IsOneAtATime_AndItsHandleThawsOnlyOnce()
        {
            var session = NewSession();
            var first = session.Freeze("playing");

            Assert.Throws<InvalidOperationException>(() => session.Freeze("another"));

            first.Dispose();
            var second = session.Freeze("again");
            first.Dispose();

            Assert.That(session.IsFrozen, Is.True, "A spent handle doesn't thaw a later freeze.");
            second.Dispose();
            Assert.That(session.IsFrozen, Is.False);
        }

        [Test]
        public void Freeze_IsRefusedInsideABatch()
        {
            var session = NewSession();

            Assert.Throws<InvalidOperationException>(() => session.Batch("Edit", () => session.Freeze("playing")));
            Assert.That(session.IsFrozen, Is.False);
        }

        [Test]
        public void Freeze_NeedsAReason()
        {
            Assert.Throws<ArgumentException>(() => NewSession().Freeze(" "));
        }
    }
}
