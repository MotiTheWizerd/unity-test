using System;
using System.Collections.Generic;
using System.Linq;
using Forge.Data;
using Forge.Data.Records;
using Forge.Editing.Commands;
using Forge.Editing.Placement;

namespace Forge.Editing.Building
{
    /// <summary>
    /// What the world-building commands work with: the session they edit, how new objects are seated on the
    /// ground, and the user's placement options, shared with the place tool so both place alike.
    /// </summary>
    public sealed class BuildContext
    {
        public BuildContext(EditSession session, ObjectSeating seating, PlacementOptions options)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            Seating = seating ?? throw new ArgumentNullException(nameof(seating));
            Options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public EditSession Session { get; }

        public ObjectSeating Seating { get; }

        public PlacementOptions Options { get; }

        /// <summary>The base object with id <paramref name="id"/>; refused, with a hint, when there is none.</summary>
        public BaseObjectRecord BaseObject(string id)
        {
            if (RecordId.TryParse(id, out var recordId)
                && Session.World.Records.TryGet(recordId, out var record)
                && record is BaseObjectRecord baseObject)
                return baseObject;

            throw new CommandException($"There is no object '{id}'. list_objects gives the ids that can be placed.");
        }

        /// <summary>The display name of base object <paramref name="baseId"/>, or its id if it is missing.</summary>
        public string NameOf(RecordId baseId) =>
            Session.World.Records.TryGet(baseId, out var record) && record is BaseObjectRecord baseObject
                ? baseObject.DisplayName
                : baseId.ToString();

        /// <summary>How many of each kind, most first, e.g. "8 Pine Tree, 4 Bush".</summary>
        public string CountByName(IEnumerable<PlacedObjectRecord> placed) =>
            string.Join(", ", placed
                .GroupBy(p => p.BaseId)
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key)
                .Select(g => $"{g.Count()} {NameOf(g.Key)}"));
    }
}
