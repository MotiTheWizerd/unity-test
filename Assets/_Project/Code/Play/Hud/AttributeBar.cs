using UnityEngine.UIElements;

namespace Forge.Play.Hud
{
    /// <summary>
    /// One HUD bar: a track and a fill as wide as the share it holds, drawn by PlayerHud.uss. Its kind
    /// ("health", "stamina", a mod's "thirst") is a style class (forge-hud-bar--{kind}), so a new bar is a style, not code.
    /// </summary>
    public sealed class AttributeBar : VisualElement
    {
        public const string UssClass = "forge-hud-bar";
        public const string FillClass = UssClass + "__fill";
        public const string FlashClass = UssClass + "--flash";

        readonly VisualElement _fill;
        float _fraction = -1f;
        float _opacity = -1f;
        bool? _flash;

        public AttributeBar(string kind)
        {
            AddToClassList(UssClass);
            AddToClassList($"{UssClass}--{kind}");
            pickingMode = PickingMode.Ignore;
            _fill = new VisualElement { pickingMode = PickingMode.Ignore };
            _fill.AddToClassList(FillClass);
            Add(_fill);
        }

        /// <summary>Draws the bar holding <paramref name="fraction"/> (0 to 1), at <paramref name="opacity"/>, lit by a flash or not.</summary>
        public void Draw(float fraction, float opacity, bool flash)
        {
            if (fraction != _fraction)
            {
                _fraction = fraction;
                _fill.style.width = Length.Percent(fraction * 100f);
            }

            if (opacity != _opacity)
            {
                _opacity = opacity;
                style.opacity = opacity;
            }

            if (flash != _flash)
            {
                _flash = flash;
                EnableInClassList(FlashClass, flash);
            }
        }
    }
}
