using Forge.Game.Actors;
using Forge.Game.Attributes;
using Forge.Game.Combat;
using Forge.Game.Hud;
using UnityEngine.UIElements;

namespace Forge.Play.Hud
{
    /// <summary>
    /// The health of whoever the player last struck, at the top of the view, as Skyrim's enemy bar: its name over a
    /// health bar, shown when a blow lands (<see cref="Struck"/>) and faded a moment after (<see cref="BarFade"/>),
    /// reading the target's numbers from the game's combatants. Drawn by PlayerHud.uss.
    /// </summary>
    public sealed class TargetHealth : VisualElement
    {
        public const string UssClass = "forge-hud-target";
        public const string NameClass = UssClass + "__name";

        readonly Label _name;
        readonly AttributeBar _bar = new AttributeBar("health");
        readonly BarFade _fade = new BarFade();
        ActorId _target;

        public TargetHealth()
        {
            AddToClassList(UssClass);
            pickingMode = PickingMode.Ignore;
            _name = new Label { pickingMode = PickingMode.Ignore };
            _name.AddToClassList(NameClass);
            Add(_name);
            Add(_bar);
        }

        /// <summary>A blow of the player's landed on <paramref name="target"/>: its bar shows.</summary>
        public void Struck(ActorId target)
        {
            _target = target;
            _fade.Show();
        }

        /// <summary>Draws the target's health from <paramref name="combatants"/>, fading as <paramref name="deltaTime"/> passes; hidden without one.</summary>
        public void Tick(Combatants combatants, float deltaTime)
        {
            if (_target.IsDefault || combatants == null || !combatants.TryGet(_target, out var attributes) ||
                !attributes.Set.Contains(CoreAttributes.Health))
            {
                style.display = DisplayStyle.None;
                return;
            }

            style.display = DisplayStyle.Flex;
            _name.text = combatants.NameOf(_target);
            var fraction = attributes.Fraction(CoreAttributes.Health);
            // A target's bar shows for a while after each blow, hurt or not, then fades: ticked as if full.
            _fade.Tick(1f, deltaTime);
            _bar.Draw(fraction, _fade.Opacity, false);
            _name.style.opacity = _fade.Opacity;
        }
    }
}
