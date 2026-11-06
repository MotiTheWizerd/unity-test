using Forge.Editing.Inspection;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI.Inspector
{
    /// <summary>
    /// One section of the inspector (<see cref="InspectorPage"/>), like a component's block in Unity's Inspector: a
    /// title over its own content, shown when it has something to say about what is selected. A new kind of thing in the
    /// game (people, items, lights, doors…) gets its own section; the page never changes for it.
    /// </summary>
    public interface IInspectorSection
    {
        /// <summary>The section's heading.</summary>
        string Title { get; }

        /// <summary>The section's content, built once.</summary>
        VisualElement Content { get; }

        /// <summary>Whether the section has anything to show for <paramref name="inspection"/>.</summary>
        bool Shows(Inspection inspection);

        /// <summary>
        /// Shows <paramref name="inspection"/>: called when the selection or the world changes (an edit, an undo), not
        /// every frame, and only while the section <see cref="Shows"/> it.
        /// </summary>
        void Show(Inspection inspection);

        /// <summary>Called every frame while the section is on show, for what changes without an edit; keep it cheap.</summary>
        void Refresh();
    }
}
