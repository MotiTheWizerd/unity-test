using System;
using System.Linq;
using Forge.Data;
using Forge.EditorApp.UI.Controls;
using Forge.Editing;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI
{
    /// <summary>
    /// The Materials page's preview card (<see cref="PreviewCard"/>): a large ball of the material in focus on a studio
    /// backdrop. In focus is the ball last picked in the library (<see cref="Pick"/>), or else what the selection's
    /// chosen part wears; a new selection or another chosen part puts that part back in focus. The caption names the
    /// part when it wears the material shown.
    /// </summary>
    public sealed class MaterialPreview
    {
        const string NothingTitle = "No material";
        const string NothingCaption = "Pick a ball to preview it";
        const string OwnLookTitle = "Own look";

        readonly EditorContext _context;
        readonly EditorCommands _commands;
        readonly MaterialBalls _balls;
        readonly PreviewCard _card;
        RecordId? _picked;
        string _chosen;
        string _shown;

        public MaterialPreview(VisualElement root, EditorContext context, EditorCommands commands, MaterialBalls balls)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
            _balls = balls ?? throw new ArgumentNullException(nameof(balls));
            _card = PanelElements.Required<PreviewCard>(root, "material-preview");
            _context.Selection.Changed += () => _picked = null;
        }

        /// <summary>Puts <paramref name="material"/> in focus (a library ball was picked).</summary>
        public void Pick(RecordId material) => _picked = material;

        public void Refresh()
        {
            var chosen = _commands.DressedSlot(_commands.SelectionSlots());
            if (chosen != _chosen)
            {
                _chosen = chosen;
                _picked = null;
            }

            var worn = _commands.SelectionMaterial(chosen);
            var shown = _picked ?? worn;
            var record = shown is RecordId id ? _context.Materials.FirstOrDefault(m => m.Id == id) : null;
            var onPart = chosen != null && shown == worn ? $"On {Materials.SlotLabel(chosen)}" : null;

            // Draw only when what the card says changes.
            var key = $"{record?.Id}|{onPart}|{chosen}";
            if (key == _shown)
                return;
            _shown = key;

            if (record != null)
                _card.Show(_balls.PreviewOf(record), record.DisplayName, onPart);
            else if (chosen != null)
                _card.Show(null, OwnLookTitle, onPart);
            else
                _card.Show(null, NothingTitle, NothingCaption);
        }
    }
}
