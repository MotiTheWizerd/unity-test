using System;
using System.Collections.Generic;
using System.Linq;
using Forge.Data;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Editing;
using NUnit.Framework;
using static Forge.Tests.Data.TestRecords;

namespace Forge.Tests.Editing
{
    public class EditSessionTests
    {
        static readonly RecordId Tree = RecordId.Parse("core:tree");
        static readonly RecordId Rock = RecordId.Parse("core:rock");

        static EditSession NewSession(Plugin active = null) =>
            new EditSession(
                new[] { Plugin("core", Base("core:tree", "Oak"), Base("core:rock", "Rock")) },
                active ?? Plugin("user", new[] { "core" }));

        [Test]
        public void NewSession_ShowsMasterDataAndIsClean()
        {
            var session = NewSession();

            Assert.That(session.World.Records.Count, Is.EqualTo(2));
            Assert.That(session.IsDirty, Is.False);
            Assert.That(session.ActivePluginId, Is.EqualTo("user"));
        }

        [Test]
        public void Put_NewRecord_AppearsInWorldAndRaisesEvent()
        {
            var session = NewSession();
            ResolvedWorld raised = null;
            session.WorldChanged += w => raised = w;

            var id = session.NewId("ref");
            session.Put(Placed(id.ToString(), "core:tree"));

            Assert.That(session.World.Records.Contains(id), Is.True);
            Assert.That(session.World.Records.GetSource(id), Is.EqualTo("user"));
            Assert.That(raised, Is.SameAs(session.World));
            Assert.That(session.IsDirty, Is.True);
        }

        [Test]
        public void Put_MasterRecord_BecomesOverrideInActivePlugin()
        {
            var session = NewSession();

            session.Put(Base("core:tree", "Giant Oak"));

            Assert.That(session.World.Records.Get<BaseObjectRecord>(Tree).DisplayName, Is.EqualTo("Giant Oak"));
            Assert.That(session.BuildActivePlugin().Records.Single().Id, Is.EqualTo(Tree));
        }

        [Test]
        public void Put_InvalidOwnership_IsRejectedAndRolledBack()
        {
            var session = NewSession();

            Assert.Throws<DataValidationException>(() => session.Put(Base("other:thing")));

            Assert.That(session.BuildActivePlugin().Records, Is.Empty);
            Assert.That(session.IsDirty, Is.False);
        }

        [Test]
        public void Delete_MasterRecord_AddsDeletion()
        {
            var session = NewSession();

            session.Delete(Rock);

            Assert.That(session.World.Records.Contains(Rock), Is.False);
            Assert.That(session.BuildActivePlugin().Deletions, Is.EqualTo(new[] { Rock }));
        }

        [Test]
        public void Delete_OwnRecord_RemovesItWithoutDeletionEntry()
        {
            var session = NewSession();
            var id = session.NewId("ref");
            session.Put(Placed(id.ToString(), "core:tree"));

            session.Delete(id);

            Assert.That(session.World.Records.Contains(id), Is.False);
            Assert.That(session.BuildActivePlugin().Records, Is.Empty);
            Assert.That(session.BuildActivePlugin().Deletions, Is.Empty);
        }

        [Test]
        public void Delete_OverriddenMasterRecord_DropsOverrideAndDeletes()
        {
            var session = NewSession();
            session.Put(Base("core:tree", "Giant Oak"));

            session.Delete(Tree);

            var active = session.BuildActivePlugin();
            Assert.That(active.Records, Is.Empty);
            Assert.That(active.Deletions, Is.EqualTo(new[] { Tree }));
        }

        [Test]
        public void Put_AfterDelete_RestoresAsOverride()
        {
            var session = NewSession();
            session.Delete(Rock);

            session.Put(Base("core:rock", "Boulder"));

            Assert.That(session.World.Records.Get<BaseObjectRecord>(Rock).DisplayName, Is.EqualTo("Boulder"));
            Assert.That(session.BuildActivePlugin().Deletions, Is.Empty);
        }

        [Test]
        public void Delete_MissingRecord_Throws()
        {
            Assert.Throws<KeyNotFoundException>(() => NewSession().Delete(RecordId.Parse("core:none")));
        }

        [Test]
        public void NewId_IsSequentialAndContinuesFromLoadedData()
        {
            var session = NewSession(Plugin("user", new[] { "core" }, Placed("user:ref/000007", "core:tree")));

            Assert.That(session.NewId("ref").ToString(), Is.EqualTo("user:ref/000008"));
            Assert.That(session.NewId("marker").ToString(), Is.EqualTo("user:marker/000001"));
        }

        [Test]
        public void NewIds_ReservesDistinctConsecutiveIds_ForOneBatch()
        {
            var session = NewSession(Plugin("user", new[] { "core" }, Placed("user:ref/000007", "core:tree")));

            var ids = session.NewIds("ref", 3);
            session.Batch("Place 3", () =>
            {
                foreach (var id in ids)
                    session.Put(Placed(id.ToString(), "core:tree"));
            });

            Assert.That(ids.Select(id => id.ToString()), Is.EqualTo(new[] { "user:ref/000008", "user:ref/000009", "user:ref/000010" }));
            Assert.That(ids.All(session.World.Records.Contains), Is.True);
            Assert.That(session.NewId("ref").ToString(), Is.EqualTo("user:ref/000011"));
            Assert.Throws<ArgumentOutOfRangeException>(() => session.NewIds("ref", 0));
        }

        [Test]
        public void MarkSaved_ClearsDirtyFlag()
        {
            var session = NewSession();
            session.Put(Base("core:tree", "Giant Oak"));

            session.MarkSaved();

            Assert.That(session.IsDirty, Is.False);
        }

        [Test]
        public void ActivePluginWithMissingMaster_IsRejected()
        {
            Assert.Throws<DataValidationException>(() =>
                new EditSession(new Plugin[0], Plugin("user", new[] { "core" })));
        }
    }
}
