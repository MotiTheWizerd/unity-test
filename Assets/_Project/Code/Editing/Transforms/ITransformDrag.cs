using System.Numerics;
using Forge.Data.Records;

namespace Forge.Editing.Transforms
{
    /// <summary>
    /// A gizmo drag in progress. It turns pointer rays into a change of placed
    /// objects that a tool previews every frame and commits once, through
    /// <see cref="ObjectEdits.Transform"/>. Preview and commit both call
    /// <see cref="Apply"/>, so what is shown is exactly what gets saved.
    /// </summary>
    public interface ITransformDrag
    {
        /// <summary>Verb for the undo step, e.g. "Rotate".</summary>
        string Verb { get; }

        /// <summary>Where the gizmo belongs now: it follows a move and stays on the pivot otherwise.</summary>
        Vector3 HandlePosition { get; }

        /// <summary>True while the drag would change nothing.</summary>
        bool IsIdentity { get; }

        /// <summary>Short live readout for the status line, e.g. "45°" or "×1.30".</summary>
        string Readout { get; }

        /// <summary>Follows the pointer. Rays that miss the handle's constraint keep the last value.</summary>
        void Update(Vector3 rayOrigin, Vector3 rayDirection, bool snap);

        /// <summary>The object as the drag would leave it.</summary>
        PlacedObjectRecord Apply(PlacedObjectRecord original);
    }
}
