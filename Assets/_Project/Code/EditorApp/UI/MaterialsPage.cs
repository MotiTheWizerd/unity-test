using System;
using Forge.EditorApp.UI.Controls;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI
{
    /// <summary>
    /// The side dock's Materials page: a preview card of the material in focus (<see cref="MaterialPreview"/>) over the
    /// selected object's parts (<see cref="MaterialSlotList"/>, with Reset look and Make variant, <see cref="VariantForm"/>)
    /// over the library of materials (<see cref="MaterialLibrary"/>). It is only brought up to date while it is on show.
    /// </summary>
    public sealed class MaterialsPage : IPanelSection
    {
        readonly ForgeSideDock _dock;
        readonly MaterialPreview _preview;
        readonly MaterialSlotList _slots;
        readonly VariantForm _variant;
        readonly MaterialLibrary _library;

        public MaterialsPage(VisualElement root, ForgeSideDock dock, EditorContext context, EditorCommands commands, MaterialBalls balls)
        {
            _dock = dock ?? throw new ArgumentNullException(nameof(dock));
            _preview = new MaterialPreview(root, context, commands, balls);
            _slots = new MaterialSlotList(PanelElements.Required<VisualElement>(root, "material-parts"), context, commands, balls);
            _variant = new VariantForm(root, commands);
            _library = new MaterialLibrary(root, context, commands, balls);
            _library.Picked += _preview.Pick;
        }

        public void Refresh()
        {
            if (!_dock.IsOpen || _dock.Current != SideDockSection.MaterialsPage)
                return;
            _preview.Refresh();
            _slots.Refresh();
            _variant.Refresh();
            _library.Refresh();
        }
    }
}
