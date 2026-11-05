using System;
using Forge.Editing.Geometry;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI
{
    /// <summary>
    /// The camera's fly speed in the top bar's global row: the presets as a segmented row (the one in use lit), the
    /// speed itself when the wheel picked one between the presets, and whether it follows the camera's height.
    /// </summary>
    public sealed class FlySpeedSection : IPanelSection
    {
        readonly FlySpeed _speed;
        readonly ChoiceRow<float> _presets;
        readonly Label _other;
        readonly Toggle _height;

        public FlySpeedSection(VisualElement root, EditorContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            _speed = context.FlySpeed;
            _presets = new ChoiceRow<float>(PanelElements.Required<VisualElement>(root, "fly-speed-presets"), FlySpeed.Presets, FlySpeed.Label, _speed.Set);
            _other = PanelElements.Required<Label>(root, "fly-speed-other");
            _height = PanelElements.Required<Toggle>(root, "fly-speed-height");
            _height.RegisterValueChangedCallback(change => _speed.FollowsHeight = change.newValue);
        }

        public void Refresh()
        {
            var multiplier = _speed.Multiplier;
            var preset = IsPreset(multiplier);
            _presets.Refresh(preset ? multiplier : (float?)null);
            PanelElements.SetVisible(_other, !preset);
            if (!preset)
                PanelElements.SetText(_other, FlySpeed.Label(multiplier));
            if (_height.value != _speed.FollowsHeight)
                _height.SetValueWithoutNotify(_speed.FollowsHeight);
        }

        static bool IsPreset(float multiplier)
        {
            foreach (var preset in FlySpeed.Presets)
            {
                if (preset == multiplier)
                    return true;
            }

            return false;
        }
    }
}
