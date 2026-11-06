using System.Linq;
using Forge.Data.Records;
using Forge.EditorApp.UI.Controls;
using Forge.Editing.Inspection;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI.Inspector
{
    /// <summary>
    /// What is selected. One placed object: its kind's name, where the palette files it, its id and its kind's id, and
    /// the plugin its version comes from. Several: how many, and of which kinds. Any other record shows its type and id,
    /// so a new kind of record is never invisible before it has a section of its own.
    /// </summary>
    public sealed class IdentitySection : IInspectorSection
    {
        const int KindsListed = 3;

        readonly ForgePropertyRow _name = new ForgePropertyRow("Name");
        readonly ForgePropertyRow _kind = new ForgePropertyRow("Kind");
        readonly ForgePropertyRow _category = new ForgePropertyRow("Category");
        readonly ForgePropertyRow _id = new ForgePropertyRow("Id");
        readonly ForgePropertyRow _source = new ForgePropertyRow("From");

        public IdentitySection()
        {
            Content = new VisualElement();
            Content.Add(_name);
            Content.Add(_kind);
            Content.Add(_category);
            Content.Add(_id);
            Content.Add(_source);
        }

        public string Title => "OBJECT";

        public VisualElement Content { get; }

        public bool Shows(Inspection inspection) => !inspection.IsEmpty;

        public void Show(Inspection inspection)
        {
            if (inspection.Count == 1)
                ShowOne(inspection, inspection.Records[0]);
            else
                ShowMany(inspection);
        }

        public void Refresh()
        {
        }

        void ShowOne(Inspection inspection, Record record)
        {
            var placed = record as PlacedObjectRecord;
            var kind = placed != null ? inspection.Find<BaseObjectRecord>(placed.BaseId) : null;

            Set(_name, kind?.DisplayName ?? record.GetType().Name.Replace("Record", string.Empty));
            Set(_kind, placed != null ? placed.BaseId.ToString() : null);
            Set(_category, kind != null && kind.Category.Length > 0 ? kind.Category.Replace("/", " / ") : null);
            Set(_id, record.Id.ToString());
            Set(_source, inspection.World?.GetSource(record.Id));
        }

        void ShowMany(Inspection inspection)
        {
            var kinds = inspection.Records
                .GroupBy(r => r is PlacedObjectRecord p ? inspection.Find<BaseObjectRecord>(p.BaseId)?.DisplayName ?? p.BaseId.ToString() : r.GetType().Name)
                .OrderByDescending(g => g.Count())
                .ToList();
            var listed = string.Join(", ", kinds.Take(KindsListed).Select(g => $"{g.Key} ×{g.Count()}"));
            if (kinds.Count > KindsListed)
                listed += $", and {kinds.Count - KindsListed} more";

            Set(_name, $"{inspection.Count} objects");
            Set(_kind, listed);
            Set(_category, null);
            Set(_id, null);
            Set(_source, null);
        }

        // A row shows only when it has a value.
        static void Set(ForgePropertyRow row, string value)
        {
            PanelElements.SetVisible(row, !string.IsNullOrEmpty(value));
            if (!string.IsNullOrEmpty(value))
                row.SetText(value);
        }
    }
}
