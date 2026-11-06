using System;
using System.Collections.Generic;
using Forge.Data;
using Forge.Data.Records;
using Forge.Game.Actors;

namespace Forge.Game.Attributes
{
    /// <summary>
    /// One actor's attributes, the player's or anyone's: its own numbers over the shared <see cref="AttributeSet"/>.
    /// <para>
    /// An attribute's value is its base (the record's, or the actor's own once set: a level gained) plus its terms (what
    /// other attributes add), then its modifiers: <c>(base + terms + adds) × (1 + percents / 100) × multipliers</c>, held
    /// within the record's min and max. Every modifier names its <see cref="ModifierSource"/>, so removing the source
    /// removes exactly what it did; a modifier given seconds lasts that long (a potion) and the rest until removed (an
    /// item worn). Values are worked out when something changes, not when read, and a change ripples on to whatever
    /// reads it, so reading is free however many actors there are.
    /// </para>
    /// <para>
    /// A resource's value is the most it holds; what it has left is <see cref="Current"/>, its value less the damage it
    /// took, so a bigger pool lifts what is left with it and a smaller one never leaves more than it holds. It refills
    /// at its regen rate once its regen delay has passed since it was last spent.
    /// </para>
    /// Engine-free; the game ticks it.
    /// </summary>
    public sealed class ActorAttributes
    {
        sealed class Modifier
        {
            public ModifierSource Source;
            public ModifierOp Op;
            public float Amount;
            public float Remaining;
        }

        readonly float[] _base;
        readonly float[] _values;
        readonly float[] _damage;
        readonly float[] _sinceSpent;
        readonly List<Modifier>[] _modifiers;
        readonly List<int> _timed = new List<int>();
        readonly bool[] _pending;

        /// <param name="owner">Whose attributes they are, which what happens to them names (<see cref="AttributeEvents"/>).</param>
        public ActorAttributes(AttributeSet set, ActorId owner)
        {
            Set = set ?? throw new ArgumentNullException(nameof(set));
            if (owner.IsDefault)
                throw new ArgumentException("Attributes belong to an actor.", nameof(owner));
            Owner = owner;
            var count = set.Count;
            _base = new float[count];
            _values = new float[count];
            _damage = new float[count];
            _sinceSpent = new float[count];
            _modifiers = new List<Modifier>[count];
            _pending = new bool[count];
            for (var slot = 0; slot < count; slot++)
            {
                _base[slot] = set.Records[slot].BaseValue;
                _sinceSpent[slot] = float.PositiveInfinity;
            }

            foreach (var slot in set.Order)
                _values[slot] = Compute(slot);
        }

        public AttributeSet Set { get; }

        /// <summary>Whose attributes they are.</summary>
        public ActorId Owner { get; }

        /// <summary>
        /// An attribute's value or what a resource has left changed; with what it is now. Told as it happens, often (a
        /// refilling bar every frame), for whatever draws the numbers; happenings go on the game's stream instead.
        /// </summary>
        public event Action<AttributeChange> Changed;

        /// <summary>A resource ran out (what it has left reached its min).</summary>
        public event Action<RecordId> Depleted;

        /// <summary>A resource is full again (refilled or restored to its most).</summary>
        public event Action<RecordId> Refilled;

        /// <summary>The attribute's value: a stat's number, a resource's most.</summary>
        public float Value(RecordId attribute) => _values[Set.SlotOf(attribute)];

        /// <summary>What a resource has left; a stat's value.</summary>
        public float Current(RecordId attribute) => CurrentOf(Set.SlotOf(attribute));

        /// <summary>What a resource has left of its most, 0 to 1 (a bar's fill); 1 for a stat or an empty pool.</summary>
        public float Fraction(RecordId attribute)
        {
            var slot = Set.SlotOf(attribute);
            var most = _values[slot];
            return most > 0f ? Clamp(CurrentOf(slot) / most, 0f, 1f) : 1f;
        }

        /// <summary>The base before terms and modifiers: the record's until <see cref="SetBase"/>.</summary>
        public float Base(RecordId attribute) => _base[Set.SlotOf(attribute)];

        /// <summary>Gives the actor its own base for an attribute (a level gained, an NPC's own strength).</summary>
        public void SetBase(RecordId attribute, float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentOutOfRangeException(nameof(value), value, "Must be a finite number.");
            var slot = Set.SlotOf(attribute);
            _base[slot] = value;
            Recompute(slot);
        }

        /// <summary>
        /// Adds <paramref name="modifier"/> from <paramref name="source"/>, for <paramref name="seconds"/> if given (it then
        /// wears off by itself), else until the source is removed.
        /// </summary>
        public void Add(ModifierSource source, AttributeModifier modifier, float? seconds = null)
        {
            if (source.Key == null)
                throw new ArgumentException("A modifier names its source.", nameof(source));
            if (seconds.HasValue && !(seconds.Value > 0f && !float.IsInfinity(seconds.Value)))
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "Must be a positive number of seconds.");

            var slot = Set.SlotOf(modifier.Attribute);
            var list = _modifiers[slot] ?? (_modifiers[slot] = new List<Modifier>());
            list.Add(new Modifier
            {
                Source = source,
                Op = modifier.Op,
                Amount = modifier.Amount,
                Remaining = seconds ?? float.PositiveInfinity,
            });
            if (seconds.HasValue && !_timed.Contains(slot))
                _timed.Add(slot);
            Recompute(slot);
        }

        /// <summary>Takes away every modifier <paramref name="source"/> added; how many there were.</summary>
        public int Remove(ModifierSource source)
        {
            var removed = 0;
            for (var slot = 0; slot < _modifiers.Length; slot++)
            {
                var list = _modifiers[slot];
                var count = list?.RemoveAll(m => m.Source == source) ?? 0;
                if (count == 0)
                    continue;
                removed += count;
                _pending[slot] = true;
            }

            RecomputePending();
            return removed;
        }

        /// <summary>Whether <paramref name="source"/> has modifiers on the actor.</summary>
        public bool Has(ModifierSource source)
        {
            foreach (var list in _modifiers)
            {
                if (list != null && list.Exists(m => m.Source == source))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Spends up to <paramref name="amount"/> of a resource (a sprint's second, a blow): as much as it has, and how
        /// much that was. Spending holds back its refill for its regen delay.
        /// </summary>
        public float Drain(RecordId resource, float amount)
        {
            var slot = ResourceSlot(resource);
            if (!(amount >= 0f) || float.IsInfinity(amount))
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Must be a finite amount, zero or more.");
            _sinceSpent[slot] = 0f;
            var taken = Math.Min(amount, Math.Max(0f, CurrentOf(slot) - Set.Records[slot].Min));
            if (taken > 0f)
                SetDamage(slot, _damage[slot] + taken);
            return taken;
        }

        /// <summary>Spends <paramref name="amount"/> of a resource only if it has it all (a jump); whether it did.</summary>
        public bool TrySpend(RecordId resource, float amount)
        {
            var slot = ResourceSlot(resource);
            if (!(amount >= 0f) || float.IsInfinity(amount))
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Must be a finite amount, zero or more.");
            if (CurrentOf(slot) - Set.Records[slot].Min < amount)
                return false;
            Drain(resource, amount);
            return true;
        }

        /// <summary>Gives back up to <paramref name="amount"/> of a resource (a potion, a rest), never past its most.</summary>
        public void Restore(RecordId resource, float amount)
        {
            var slot = ResourceSlot(resource);
            if (!(amount >= 0f) || float.IsInfinity(amount))
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Must be a finite amount, zero or more.");
            SetDamage(slot, Math.Max(0f, _damage[slot] - amount));
        }

        /// <summary>Lets <paramref name="deltaTime"/> seconds pass: timed modifiers wear off, resources refill.</summary>
        public void Tick(float deltaTime)
        {
            if (!(deltaTime > 0f) || float.IsInfinity(deltaTime))
                return;

            WearOff(deltaTime);

            for (var slot = 0; slot < _damage.Length; slot++)
            {
                var record = Set.Records[slot];
                if (record.Kind != AttributeKind.Resource)
                    continue;
                _sinceSpent[slot] += deltaTime;
                if (_damage[slot] <= 0f || !record.RegenRate.HasValue)
                    continue;
                var delay = record.RegenDelay.HasValue ? Math.Max(0f, Value(record.RegenDelay.Value)) : 0f;
                if (_sinceSpent[slot] < delay)
                    continue;
                // Only the part of this tick after the delay ran out refills.
                var refilling = Math.Min(deltaTime, _sinceSpent[slot] - delay);
                var rate = Value(record.RegenRate.Value);
                if (rate > 0f)
                    SetDamage(slot, Math.Max(0f, _damage[slot] - rate * refilling));
            }
        }

        void WearOff(float deltaTime)
        {
            for (var i = _timed.Count - 1; i >= 0; i--)
            {
                var slot = _timed[i];
                var list = _modifiers[slot];
                var anyTimed = false;
                var expired = false;
                foreach (var modifier in list)
                {
                    if (float.IsPositiveInfinity(modifier.Remaining))
                        continue;
                    modifier.Remaining -= deltaTime;
                    if (modifier.Remaining <= 0f)
                        expired = true;
                    else
                        anyTimed = true;
                }

                if (expired)
                {
                    list.RemoveAll(m => m.Remaining <= 0f);
                    _pending[slot] = true;
                }

                if (!anyTimed)
                    _timed.RemoveAt(i);
            }

            RecomputePending();
        }

        int ResourceSlot(RecordId attribute)
        {
            var slot = Set.SlotOf(attribute);
            if (Set.Records[slot].Kind != AttributeKind.Resource)
                throw new InvalidOperationException($"{attribute} is a stat; only a resource is spent or restored.");
            return slot;
        }

        float CurrentOf(int slot)
        {
            var record = Set.Records[slot];
            if (record.Kind != AttributeKind.Resource)
                return _values[slot];
            return Math.Max(record.Min, _values[slot] - _damage[slot]);
        }

        void SetDamage(int slot, float damage)
        {
            var before = CurrentOf(slot);
            _damage[slot] = damage;
            var after = CurrentOf(slot);
            if (after == before)
                return;
            var id = Set.Records[slot].Id;
            Changed?.Invoke(new AttributeChange(id, _values[slot], after));
            if (after <= Set.Records[slot].Min && before > Set.Records[slot].Min)
                Depleted?.Invoke(id);
            else if (after >= _values[slot] && before < _values[slot])
                Refilled?.Invoke(id);
        }

        void Recompute(int slot)
        {
            _pending[slot] = true;
            RecomputePending();
        }

        // Works out every pending attribute and whatever reads them, in the set's order (each after what it reads),
        // telling of those that changed.
        void RecomputePending()
        {
            foreach (var slot in Set.Order)
            {
                if (!_pending[slot])
                    continue;
                _pending[slot] = false;

                var before = _values[slot];
                var currentBefore = CurrentOf(slot);
                _values[slot] = Compute(slot);
                if (_values[slot] == before)
                    continue;

                foreach (var reader in Set.ReadersOf(slot))
                    _pending[reader] = true;
                Changed?.Invoke(new AttributeChange(Set.Records[slot].Id, _values[slot], CurrentOf(slot)));
                var min = Set.Records[slot].Min;
                if (Set.Records[slot].Kind == AttributeKind.Resource && CurrentOf(slot) <= min && currentBefore > min)
                    Depleted?.Invoke(Set.Records[slot].Id);
            }
        }

        float Compute(int slot)
        {
            var record = Set.Records[slot];
            var sum = _base[slot];
            var terms = Set.TermsOf(slot);
            for (var i = 0; i < terms.Count; i++)
                sum += record.Terms[i].Per * _values[terms[i]];

            var percent = 0f;
            var factor = 1f;
            var list = _modifiers[slot];
            if (list != null)
            {
                foreach (var modifier in list)
                {
                    switch (modifier.Op)
                    {
                        case ModifierOp.Add:
                            sum += modifier.Amount;
                            break;
                        case ModifierOp.Percent:
                            percent += modifier.Amount;
                            break;
                        case ModifierOp.Multiply:
                            factor *= modifier.Amount;
                            break;
                    }
                }
            }

            var value = sum * (1f + percent / 100f) * factor;
            return Clamp(value, record.Min, record.Max ?? float.MaxValue);
        }

        static float Clamp(float value, float min, float max) => value < min ? min : value > max ? max : value;
    }

    /// <summary>An attribute after a change: its value (a resource's most) and what it has left (a stat: its value).</summary>
    public readonly struct AttributeChange
    {
        public AttributeChange(RecordId attribute, float value, float current)
        {
            Attribute = attribute;
            Value = value;
            Current = current;
        }

        public RecordId Attribute { get; }

        public float Value { get; }

        public float Current { get; }
    }
}
