using System;
using System.Collections.Generic;
using Forge.Data;
using Forge.Game.Actors;
using Forge.Game.Attributes;
using Forge.Game.Combat;
using Forge.Game.Hud;
using Forge.Game.Player;
using UnityEngine.UIElements;

namespace Forge.Play.Hud
{
    /// <summary>
    /// The player's HUD while playing, as Skyrim's: magicka, health and stamina bars along the bottom (PlayerHud.uss),
    /// each shown while not full and for a moment after it changes, then faded (<see cref="BarFade"/>), and a dot in the
    /// middle of the view (<see cref="Crosshair"/>, a target cross while a bow is drawn) with the use key's prompt under it (<see cref="InteractionPrompt"/>, from the player's
    /// <see cref="PlayerSession.Focus"/>). The bars read the player's numbers as they change
    /// (<see cref="ActorAttributes.Changed"/>); what happens to the player comes from the game's stream: the stamina bar
    /// blinks when the player is exhausted (<see cref="ActorExhausted"/>) and a bar blinks when it held too little for a
    /// move (<see cref="ActorShortOf"/>). Whoever the player's blows land on shows its health at the top
    /// (<see cref="TargetHealth"/>, on <see cref="HitLanded"/>), and while a bow is out, what it has left to shoot shows at
    /// the bottom right (<see cref="AmmoCounter"/>). It follows whichever player the <see cref="PlayerSession"/> has, so whatever
    /// plays the world (the editor's play, the game later) hosts it the same.
    /// </summary>
    public sealed class PlayerHud : IDisposable
    {
        public const string UssClass = "forge-hud";
        public const string BarsClass = UssClass + "-bars";

        sealed class Bar
        {
            public RecordId Attribute;
            public AttributeBar View;
            public readonly BarFade Fade = new BarFade();
        }

        // Left to right, as Skyrim lays them out; a mod's bar is one more entry and a style.
        static readonly (RecordId Attribute, string Kind)[] Layout =
        {
            (CoreAttributes.Magicka, "magicka"),
            (CoreAttributes.Health, "health"),
            (CoreAttributes.Stamina, "stamina"),
        };

        readonly PlayerSession _session;
        readonly VisualElement _root;
        readonly VisualElement _barRow;
        readonly Crosshair _crosshair;
        readonly InteractionPrompt _prompt;
        readonly TargetHealth _target;
        readonly AmmoCounter _ammo;
        readonly List<Bar> _bars = new List<Bar>();
        readonly IDisposable[] _subscriptions;
        ActorAttributes _attributes;

        /// <param name="parent">Where the HUD goes: a full-screen element of the panel that plays.</param>
        public PlayerHud(PlayerSession session, VisualElement parent)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));

            _root = new VisualElement { name = "player-hud", pickingMode = PickingMode.Ignore };
            _root.AddToClassList(UssClass);
            _barRow = new VisualElement { pickingMode = PickingMode.Ignore };
            _barRow.AddToClassList(BarsClass);
            foreach (var (attribute, kind) in Layout)
            {
                var bar = new Bar { Attribute = attribute, View = new AttributeBar(kind) };
                _bars.Add(bar);
                _barRow.Add(bar.View);
            }

            _crosshair = new Crosshair();
            _prompt = new InteractionPrompt();
            _target = new TargetHealth();
            _ammo = new AmmoCounter();
            _root.Add(_barRow);
            _root.Add(_crosshair);
            _root.Add(_prompt);
            _root.Add(_target);
            _root.Add(_ammo);

            parent.Add(_root);
            _root.style.display = DisplayStyle.None;

            _subscriptions = new[]
            {
                session.Events.Subscribe<ActorExhausted>(e => Flash(e.Actor, CoreAttributes.Stamina)),
                session.Events.Subscribe<ActorShortOf>(e => Flash(e.Actor, e.Attribute)),
                session.Events.Subscribe<HitLanded>(e =>
                {
                    if (e.Attacker == ActorId.Player)
                        _target.Struck(e.Target);
                }),
            };
        }

        /// <summary>Follows the player for <paramref name="deltaTime"/> seconds; hidden while there is none, or its inventory is open.</summary>
        public void Tick(float deltaTime)
        {
            var player = _session.Player;
            _root.style.display = player != null && !_session.InventoryOpen ? DisplayStyle.Flex : DisplayStyle.None;
            var offer = _session.Focus?.Offer;
            _prompt.Show(offer, _session.InteractKey);
            var combat = _session.Combat;
            _crosshair.Draw(offer != null, combat != null && combat.Aiming, combat?.DrawPull ?? 0f);
            _target.Tick(_session.Combatants, deltaTime);
            _ammo.Show(_session.Readiness != null
                ? AmmoCount.Of(_session.Equipment, _session.Inventory, _session.Readiness, _session.World)
                : AmmoCount.Hidden);

            var attributes = player != null ? player.Attributes : null;
            Bind(attributes);
            _barRow.style.display = attributes != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (attributes == null)
                return;

            foreach (var bar in _bars)
            {
                if (!attributes.Set.Contains(bar.Attribute))
                {
                    bar.View.Draw(0f, 0f, false);
                    continue;
                }

                var fraction = attributes.Fraction(bar.Attribute);
                bar.Fade.Tick(fraction, deltaTime);
                bar.View.Draw(fraction, bar.Fade.Opacity, bar.Fade.FlashOn);
            }
        }

        public void Dispose()
        {
            Bind(null);
            foreach (var subscription in _subscriptions)
                subscription.Dispose();
            _root.RemoveFromHierarchy();
        }

        // Reads the player's numbers as they change; a new world's attributes replace them.
        void Bind(ActorAttributes attributes)
        {
            if (attributes == _attributes)
                return;
            if (_attributes != null)
                _attributes.Changed -= OnChanged;
            _attributes = attributes;
            if (_attributes != null)
                _attributes.Changed += OnChanged;
        }

        void OnChanged(AttributeChange change) => Find(change.Attribute)?.Fade.Show();

        void Flash(ActorId actor, RecordId attribute)
        {
            if (actor == ActorId.Player)
                Find(attribute)?.Fade.Flash();
        }

        Bar Find(RecordId attribute) => _bars.Find(bar => bar.Attribute == attribute);
    }
}
