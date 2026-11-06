using System;
using Forge.Data.Records;
using Forge.EditorApp.UI.Controls;
using Forge.Editing.Inspection;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI.Inspector
{
    /// <summary>
    /// The selected objects' material parts (<see cref="MaterialSlotList"/>, the same list as on the Materials page):
    /// clicking a part chooses it, and a button turns the dock to the Materials page to pick what it wears.
    /// </summary>
    public sealed class MaterialsSection : IInspectorSection
    {
        readonly MaterialSlotList _slots;

        public MaterialsSection(EditorContext context, EditorCommands commands, MaterialBalls balls, ForgeSideDock dock)
        {
            if (dock == null)
                throw new ArgumentNullException(nameof(dock));
            Content = new VisualElement();
            _slots = new MaterialSlotList(Content, context, commands, balls);
            Content.Add(new ForgeButton(() => dock.Show(SideDockSection.MaterialsPage))
            {
                text = "Pick a material…",
                tooltip = "Open the Materials page: click a ball to dress the chosen part",
            });
        }

        public string Title => "MATERIALS";

        public VisualElement Content { get; }

        public bool Shows(Inspection inspection) => inspection.All<PlacedObjectRecord>().Count > 0;

        public void Show(Inspection inspection)
        {
        }

        // What the parts wear changes with edits and the chosen part with clicks in the world, so it follows every frame.
        public void Refresh() => _slots.Refresh();
    }
}
