using System;
using System.Collections.Generic;
using Forge.Data;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Game.Events;

namespace Forge.Game.Equipment
{
    /// <summary>Where an actor's weapons are: put away, coming out, in hand, going back.</summary>
    public enum Readiness
    {
        Sheathed,
        Drawing,
        Drawn,
        Sheathing,
    }

    /// <summary>
    /// Whether an actor's weapons are ready, as Skyrim's ready-weapon key: what its sheathing slots hold
    /// (<see cref="EquipSlotRecord.Sheathes"/>, the hands) is put away until <see cref="Toggle"/> draws it, which takes
    /// <see cref="DrawSeconds"/>; toggling again sheathes it, taking <see cref="SheatheSeconds"/>. A draw or a sheathe
    /// runs to its end (a press meanwhile does nothing), so whatever acts on it (an attack, later) knows the weapon is
    /// in hand only when <see cref="Readiness.Drawn"/>. With nothing to draw it stays sheathed, and taking off the last
    /// weapon puts the actor back to sheathed at once. Equipping never draws. What a draw plays is its weapon's style's
    /// (<see cref="DrawAnimation"/>: a sword from the hip, a bow from the back), else the look's own; how long it takes
    /// is the look's to say (its clips: <see cref="Timings"/> by the style's move, else <see cref="SetTimes"/>). Changes
    /// are told as they happen (<see cref="Changed"/>) and on the stream when done (<see cref="WeaponsDrawn"/>,
    /// <see cref="WeaponsSheathed"/>). Engine-free; the game ticks it.
    /// </summary>
    public sealed class WeaponReadiness : IDisposable
    {
        /// <summary>Seconds a draw or a sheathe takes until the look says otherwise.</summary>
        public const float DefaultSeconds = 1f;

        readonly ActorEquipment _equipment;
        readonly IRecordStore _records;
        readonly GameEvents _events;
        ReadyTiming _draw = new ReadyTiming(DefaultSeconds, 1f);
        ReadyTiming _sheathe = new ReadyTiming(DefaultSeconds, 1f);
        float _elapsed;

        /// <param name="records">The world, which the equip slots are read from.</param>
        /// <param name="events">The game's stream, told when weapons are drawn and sheathed; null to tell no one.</param>
        public WeaponReadiness(ActorEquipment equipment, IRecordStore records, GameEvents events = null)
        {
            _equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
            _records = records ?? throw new ArgumentNullException(nameof(records));
            _events = events;
            _equipment.Changed += OnEquipmentChanged;
        }

        public Readiness State { get; private set; }

        /// <summary>How far the draw or the sheathe has come, 0 to 1; 1 while drawn, 0 while sheathed.</summary>
        public float Progress
        {
            get
            {
                switch (State)
                {
                    case Readiness.Drawn: return 1f;
                    case Readiness.Drawing: return Fraction(_elapsed, DrawSeconds);
                    case Readiness.Sheathing: return Fraction(_elapsed, SheatheSeconds);
                    default: return 0f;
                }
            }
        }

        /// <summary>The look's timings of its weapons' styles' draws and sheathes; until set, every one plays as <see cref="SetTimes"/> says.</summary>
        public IReadyTimings Timings { get; set; }

        /// <summary>The animation a draw plays now: the style's draw of the weapon it draws; null: the look's own.</summary>
        public string DrawAnimation => StyleMove(CombatMoveKind.Draw);

        /// <summary>The animation a sheathe plays now: the style's sheathe of the weapon it puts away; null: the look's own.</summary>
        public string SheatheAnimation => StyleMove(CombatMoveKind.Sheathe);

        /// <summary>
        /// The animation it stands in with its weapons drawn: the style's stance of the weapon it draws ("BowIdle");
        /// null: the look's own drawn stance.
        /// </summary>
        public string StanceAnimation => StyleMove(CombatMoveKind.Stance);

        /// <summary>How long a draw takes now, seconds.</summary>
        public float DrawSeconds => TimingOf(DrawAnimation, _draw).Seconds;

        /// <summary>How long a sheathe takes now, seconds.</summary>
        public float SheatheSeconds => TimingOf(SheatheAnimation, _sheathe).Seconds;

        /// <summary>How far into a draw the weapon leaves where it is carried for the hand, 0 to 1.</summary>
        public float DrawnAt => TimingOf(DrawAnimation, _draw).At;

        /// <summary>How far into a sheathe the weapon leaves the hand for where it is carried, 0 to 1.</summary>
        public float SheathedAt => TimingOf(SheatheAnimation, _sheathe).At;

        /// <summary>
        /// Whether the weapons are in hand rather than in their sheaths: from the moment a draw takes them
        /// (<see cref="DrawnAt"/>) to the moment a sheathe puts them back (<see cref="SheathedAt"/>).
        /// </summary>
        public bool InHand
        {
            get
            {
                switch (State)
                {
                    case Readiness.Drawn: return true;
                    case Readiness.Drawing: return Progress >= DrawnAt;
                    case Readiness.Sheathing: return Progress < SheathedAt;
                    default: return false;
                }
            }
        }

        /// <summary>The weapons went from sheath to hand (true) or back (false), as it happens, for the look.</summary>
        public event Action<bool> InHandChanged;

        /// <summary>Whether it holds anything to draw: something in a slot that sheathes.</summary>
        public bool HasWeapons
        {
            get
            {
                foreach (var equipped in _equipment.All)
                {
                    if (SheathesAny(equipped.Fit.Slots))
                        return true;
                }

                return false;
            }
        }

        /// <summary>The state changed (a draw began, ended, ...), as it happens, for the look.</summary>
        public event Action<Readiness> Changed;

        /// <summary>
        /// How the look's own draw and sheathe play, for weapons whose style names none (zero seconds is at once): how long
        /// each takes, and how far into each the weapon changes place (<paramref name="drawnAt"/>: sheath to hand;
        /// <paramref name="sheathedAt"/>: hand to sheath).
        /// </summary>
        public void SetTimes(float drawSeconds, float sheatheSeconds, float drawnAt = 1f, float sheathedAt = 1f)
        {
            var draw = new ReadyTiming(drawSeconds, drawnAt);
            var sheathe = new ReadyTiming(sheatheSeconds, sheathedAt);
            var wasInHand = InHand;
            _draw = draw;
            _sheathe = sheathe;
            TellInHand(wasInHand);
        }

        /// <summary>The ready-weapon key: draws what is sheathed, sheathes what is drawn; false when it does nothing.</summary>
        public bool Toggle()
        {
            if (State == Readiness.Sheathed && HasWeapons)
            {
                Begin(Readiness.Drawing);
                return true;
            }

            if (State == Readiness.Drawn)
            {
                Begin(Readiness.Sheathing);
                return true;
            }

            return false;
        }

        /// <summary>Lets <paramref name="deltaTime"/> seconds pass: a draw or a sheathe comes to its end.</summary>
        public void Tick(float deltaTime)
        {
            if (State != Readiness.Drawing && State != Readiness.Sheathing)
                return;
            var wasInHand = InHand;
            _elapsed += Math.Max(0f, deltaTime);
            if (State == Readiness.Drawing && _elapsed >= DrawSeconds)
                End(Readiness.Drawn);
            else if (State == Readiness.Sheathing && _elapsed >= SheatheSeconds)
                End(Readiness.Sheathed);
            else
                TellInHand(wasInHand);
        }

        public void Dispose() => _equipment.Changed -= OnEquipmentChanged;

        bool Sheathes(RecordId slot) => _records.TryGet(slot, out var record) && record is EquipSlotRecord equipSlot && equipSlot.Sheathes;

        // The move of `kind` of the first weapon put away and drawn whose style has one; null when none does.
        string StyleMove(CombatMoveKind kind)
        {
            foreach (var equipped in _equipment.All)
            {
                var weapon = equipped.Record.Weapon;
                if (weapon == null || !SheathesAny(equipped.Fit.Slots))
                    continue;
                if (_records.TryGet(weapon.Style, out var record) && record is CombatStyleRecord style && style.TryGetMove(kind, out var move))
                    return move.Animation;
            }

            return null;
        }

        bool SheathesAny(IEnumerable<RecordId> slots)
        {
            foreach (var slot in slots)
            {
                if (Sheathes(slot))
                    return true;
            }

            return false;
        }

        // The look's timing of a style's move, else of its own draw or sheathe.
        ReadyTiming TimingOf(string animation, ReadyTiming own) =>
            animation != null && Timings != null && Timings.TryGet(animation, out var timing) ? timing : own;

        // Nothing left to hold: the hands are empty, so the actor is as if sheathed.
        void OnEquipmentChanged()
        {
            if (State != Readiness.Sheathed && !HasWeapons)
                End(Readiness.Sheathed);
        }

        void Begin(Readiness state)
        {
            var wasInHand = InHand;
            State = state;
            _elapsed = 0f;
            Changed?.Invoke(state);
            TellInHand(wasInHand);
            Tick(0f);
        }

        void End(Readiness state)
        {
            var wasInHand = InHand;
            State = state;
            _elapsed = 0f;
            Changed?.Invoke(state);
            TellInHand(wasInHand);
            if (state == Readiness.Drawn)
                _events?.Publish(new WeaponsDrawn(_equipment.Owner));
            else
                _events?.Publish(new WeaponsSheathed(_equipment.Owner));
        }

        void TellInHand(bool wasInHand)
        {
            var inHand = InHand;
            if (inHand != wasInHand)
                InHandChanged?.Invoke(inHand);
        }

        static float Fraction(float elapsed, float seconds) => seconds > 0f ? Math.Min(1f, elapsed / seconds) : 1f;
    }
}
