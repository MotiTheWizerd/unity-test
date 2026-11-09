using Forge.Game.Hud;
using UnityEngine.UIElements;

namespace Forge.Play.Hud
{
    /// <summary>
    /// What the drawn bow has left to shoot, at the bottom right of the view: the arrows' name and how many
    /// (<see cref="AmmoCount"/>), the number marked when there are none; drawn by PlayerHud.uss and redrawn only when the
    /// count changes.
    /// </summary>
    public sealed class AmmoCounter : VisualElement
    {
        public const string UssClass = "forge-hud-ammo";
        public const string NameClass = UssClass + "__name";
        public const string CountClass = UssClass + "__count";
        public const string EmptyClass = UssClass + "--empty";

        readonly Label _name;
        readonly Label _count;
        AmmoCount _shown;
        bool _drawn;

        public AmmoCounter()
        {
            AddToClassList(UssClass);
            pickingMode = PickingMode.Ignore;
            _name = new Label { pickingMode = PickingMode.Ignore };
            _name.AddToClassList(NameClass);
            _count = new Label { pickingMode = PickingMode.Ignore };
            _count.AddToClassList(CountClass);
            Add(_name);
            Add(_count);
            style.display = DisplayStyle.None;
        }

        /// <summary>Shows <paramref name="count"/>; <see cref="AmmoCount.Hidden"/> hides it.</summary>
        public void Show(AmmoCount count)
        {
            if (_drawn && count == _shown)
                return;
            _drawn = true;
            _shown = count;
            style.display = count.Shown ? DisplayStyle.Flex : DisplayStyle.None;
            if (!count.Shown)
                return;

            _name.text = count.Name ?? string.Empty;
            _count.text = count.Count.ToString();
            EnableInClassList(EmptyClass, count.Count == 0);
        }
    }
}
