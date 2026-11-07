using System;
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
    /// weapon puts the actor back to sheathed at once. Equipping never draws. How long a draw takes is the look's to say
    /// (its clips, <see cref="SetTimes"/>). Changes are told as they happen (<see cref="Changed"/>) and on the stream
    /// when done (<see cref="WeaponsDrawn"/>, <see cref="WeaponsSheathed"/>). Engine-free; the game ticks it.
    /// </summary>
    public sealed class WeaponReadiness : IDisposable
    {
        /// <summary>Seconds a draw or a sheathe takes until the look says otherwise.</summary>
        public const float DefaultSeconds = 1f;

        readonly ActorEquipment _equipment;
        readonly IRecordStore _records;
        readonly GameEvents _events;
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

        public float DrawSeconds { get; private set; } = DefaultSeconds;

        public float SheatheSeconds { get; private set; } = DefaultSeconds;

        /// <summary>Whether it holds anything to draw: something in a slot that sheathes.</summary>
        public bool HasWeapons
        {
            get
            {
                foreach (var equipped in _equipment.All)
                {
                    foreach (var slot in equipped.Fit.Slots)
                    {
                        if (Sheathes(slot))
                            return true;
                    }
                }

                return false;
            }
        }

        /// <summary>The state changed (a draw began, ended, ...), as it happens, for the look.</summary>
        public event Action<Readiness> Changed;

        /// <summary>How long a draw and a sheathe take, as the look's clips play them; zero is at once.</summary>
        public void SetTimes(float drawSeconds, float sheatheSeconds)
        {
            if (!(drawSeconds >= 0f) || float.IsInfinity(drawSeconds))
                throw new ArgumentOutOfRangeException(nameof(drawSeconds), drawSeconds, "Must be a finite number of seconds, zero or more.");
            if (!(sheatheSeconds >= 0f) || float.IsInfinity(sheatheSeconds))
                throw new ArgumentOutOfRangeException(nameof(sheatheSeconds), sheatheSeconds, "Must be a finite number of seconds, zero or more.");
            DrawSeconds = drawSeconds;
            SheatheSeconds = sheatheSeconds;
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
            _elapsed += Math.Max(0f, deltaTime);
            if (State == Readiness.Drawing && _elapsed >= DrawSeconds)
                End(Readiness.Drawn);
            else if (State == Readiness.Sheathing && _elapsed >= SheatheSeconds)
                End(Readiness.Sheathed);
        }

        public void Dispose() => _equipment.Changed -= OnEquipmentChanged;

        bool Sheathes(RecordId slot) => _records.TryGet(slot, out var record) && record is EquipSlotRecord equipSlot && equipSlot.Sheathes;

        // Nothing left to hold: the hands are empty, so the actor is as if sheathed.
        void OnEquipmentChanged()
        {
            if (State != Readiness.Sheathed && !HasWeapons)
                End(Readiness.Sheathed);
        }

        void Begin(Readiness state)
        {
            State = state;
            _elapsed = 0f;
            Changed?.Invoke(state);
            Tick(0f);
        }

        void End(Readiness state)
        {
            State = state;
            _elapsed = 0f;
            Changed?.Invoke(state);
            if (state == Readiness.Drawn)
                _events?.Publish(new WeaponsDrawn(_equipment.Owner));
            else
                _events?.Publish(new WeaponsSheathed(_equipment.Owner));
        }

        static float Fraction(float elapsed, float seconds) => seconds > 0f ? Math.Min(1f, elapsed / seconds) : 1f;
    }
}
