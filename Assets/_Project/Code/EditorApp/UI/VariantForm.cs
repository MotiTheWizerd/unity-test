using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI
{
    /// <summary>
    /// Part of the selection options: "Make variant" for the one selected object opens a name field (suggested from what
    /// it wears) with Save and Cancel; Enter saves, Esc cancels. Saving is <see cref="EditorCommands.MakeVariantOfSelection"/>.
    /// The form closes when the selection changes, so a name is never saved onto another object.
    /// </summary>
    public sealed class VariantForm
    {
        readonly EditorCommands _commands;
        readonly Button _open;
        readonly TextField _name;
        readonly Button _save;
        readonly Button _cancel;
        Forge.Data.Records.PlacedObjectRecord _naming;

        public VariantForm(VisualElement root, EditorCommands commands)
        {
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
            _open = PanelElements.Required<Button>(root, "make-variant");
            _name = PanelElements.Required<TextField>(root, "variant-name");
            _save = PanelElements.Required<Button>(root, "variant-save");
            _cancel = PanelElements.Required<Button>(root, "variant-cancel");

            _name.style.minWidth = 180f;
            _name.style.maxWidth = 260f;
            _open.clicked += Open;
            _save.clicked += Save;
            _cancel.clicked += Close;
            _name.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                    Save();
                else if (e.keyCode == KeyCode.Escape)
                    Close();
                else
                    return;
                e.StopPropagation();
            }, TrickleDown.TrickleDown);
        }

        public void Refresh()
        {
            var single = _commands.SingleSelected();
            // Another object selected (or none): the form was for the one before.
            if (_naming != null && (single == null || single.Id != _naming.Id))
                _naming = null;

            var naming = _naming != null;
            PanelElements.SetVisible(_open, single != null && !naming);
            PanelElements.SetVisible(_name, naming);
            PanelElements.SetVisible(_save, naming);
            PanelElements.SetVisible(_cancel, naming);
        }

        void Open()
        {
            _naming = _commands.SingleSelected();
            if (_naming == null)
                return;
            _name.SetValueWithoutNotify(_commands.SuggestedVariantName());
            Refresh();
            _name.schedule.Execute(() =>
            {
                _name.Focus();
                _name.SelectAll();
            });
        }

        void Save()
        {
            if (_naming == null)
                return;
            _commands.MakeVariantOfSelection(_name.value);
            if (!string.IsNullOrWhiteSpace(_name.value))
                Close();
        }

        void Close()
        {
            _naming = null;
            _name.Blur();
            Refresh();
        }
    }
}
