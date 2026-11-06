using System;
using System.Collections.Generic;
using System.Linq;
using Forge.Data;
using Forge.Data.Records;
using Forge.Editing.Commands;

namespace Forge.Editing.Building
{
    /// <summary>Dresses one material slot of placed objects in a material, or gives it back their kind's own look.</summary>
    public sealed class SetMaterialsCommand : EditorCommand<SetMaterialsCommand.Arguments, SetMaterialsCommand.Result>
    {
        public const int MaxObjects = 1000;

        readonly BuildContext _context;

        public SetMaterialsCommand(BuildContext context)
            : base("set_materials",
                "Dresses one material slot of placed objects in a material, e.g. the \"Roof\" of houses in thatch. " +
                "Slots come from list_objects (each kind of object has its own), materials from list_materials. " +
                "Leave out the material to give the slot back the object's own look. Objects without that slot are " +
                "skipped and reported with the slots they have. Ids come from inspect_area or from the results of placing.")
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public sealed class Arguments
        {
            [Param("Ids of the placed objects to dress.", MinItems = 1, MaxItems = MaxObjects)]
            public List<string> Ids { get; set; }

            [Param("The slot to dress, exactly as list_objects names it, e.g. \"Roof\".")]
            public string Slot { get; set; }

            [Param("Id of the material to wear there. Leave out to give the slot back the object's own look.", Optional = true)]
            public string Material { get; set; }
        }

        public sealed class Result : ICommandResult
        {
            internal Result(string summary, int changed, IReadOnlyList<Skip> skipped)
            {
                Summary = summary;
                Changed = changed;
                Skipped = skipped;
            }

            public string Summary { get; }

            /// <summary>How many objects now wear something else in the slot.</summary>
            public int Changed { get; }

            /// <summary>Objects left as they were, and why.</summary>
            public IReadOnlyList<Skip> Skipped { get; }
        }

        public sealed class Skip
        {
            internal Skip(string id, string reason)
            {
                Id = id;
                Reason = reason;
            }

            public string Id { get; }

            public string Reason { get; }
        }

        protected override Result Run(Arguments arguments)
        {
            var material = arguments.Material == null ? null : _context.Material(arguments.Material);
            var slot = arguments.Slot;
            var targets = new List<RecordId>();
            var skipped = new List<Skip>();
            foreach (var id in arguments.Ids.Distinct())
            {
                var placed = _context.PlacedInSpace(id);
                if (placed == null)
                {
                    skipped.Add(new Skip(id, "no placed object here has that id; inspect_area lists the objects in an area"));
                    continue;
                }

                var slots = _context.Session.World.Records.TryGet(placed.BaseId, out var record) && record is BaseObjectRecord baseObject
                    ? _context.SlotsOf(baseObject)
                    : Array.Empty<string>();
                if (slots.Contains(slot, StringComparer.Ordinal))
                    targets.Add(placed.Id);
                else
                    skipped.Add(new Skip(id, slots.Count == 0
                        ? "it has no material slots"
                        : $"it has no slot \"{slot}\"; its slots are {string.Join(", ", slots.Select(s => $"\"{s}\""))}"));
            }

            if (targets.Count == 0)
                throw new CommandException($"Nothing was changed: {skipped[0].Id}: {skipped[0].Reason}.");

            var changed = ObjectEdits.SetMaterial(_context.Session, targets, slot, material?.Id);
            var wearing = material != null ? material.DisplayName : "its own look";
            var summary = $"\"{slot}\" of {targets.Count} object(s) now wears {wearing}.";
            if (changed < targets.Count)
                summary += $" {targets.Count - changed} of them already did.";
            if (skipped.Count > 0)
                summary += $" Skipped {skipped.Count}, e.g. {skipped[0].Id}: {skipped[0].Reason}.";
            return new Result(summary, changed, skipped);
        }
    }
}
