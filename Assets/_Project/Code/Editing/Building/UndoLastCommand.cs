using System;
using System.Collections.Generic;
using System.Linq;
using Forge.Editing.Commands;

namespace Forge.Editing.Building
{
    /// <summary>Takes back the latest changes of the build waiting for review, one command's change at a time.</summary>
    public sealed class UndoLastCommand : EditorCommand<UndoLastCommand.Arguments, UndoLastCommand.Result>
    {
        public const int MaxSteps = 20;

        readonly BuildContext _context;

        public UndoLastCommand(BuildContext context)
            : base("undo_last",
                "Takes back your latest change in this request (a sculpt, a paint, a placement, a scatter…) as if it " +
                "hadn't been made; your earlier changes stay. steps takes back that many, newest first. Use it to try " +
                "something bolder and step back if it doesn't look right, instead of repairing by hand. Only changes " +
                "made in this request can be taken back; the user's own edits and accepted builds can't.")
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public sealed class Arguments
        {
            [Param("How many of your latest changes to take back. Default 1.", Optional = true, Minimum = 1, Maximum = MaxSteps)]
            public int? Steps { get; set; }
        }

        public sealed class Result : ICommandResult
        {
            internal Result(string summary, IReadOnlyList<string> undone, int remaining)
            {
                Summary = summary;
                Undone = undone;
                Remaining = remaining;
            }

            public string Summary { get; }

            /// <summary>What was taken back, newest first.</summary>
            public IReadOnlyList<string> Undone { get; }

            /// <summary>How many of this request's changes remain.</summary>
            public int Remaining { get; }
        }

        protected override Result Run(Arguments arguments)
        {
            var draft = _context.Session.Draft;
            if (draft == null || draft.Steps.Count == 0)
                throw new CommandException("There is nothing of yours to take back: only changes made in this request can be undone.");

            var undone = new List<string>();
            for (var i = 0; i < (arguments.Steps ?? 1) && draft.Steps.Count > 0; i++)
                undone.Add(draft.UndoLast().Description);

            var remaining = draft.Steps.Count;
            var summary = $"Took back {string.Join(", ", undone.Select(step => $"\"{step}\""))}; {remaining} earlier change(s) of this request remain.";
            return new Result(summary, undone, remaining);
        }
    }
}
