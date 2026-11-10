using System;
using System.Collections.Generic;
using Forge.Game.Events;

namespace Forge.Game.Actors
{
    /// <summary>
    /// The <see cref="Mode"/> of every actor in a game, the player and the world's alike: one table whoever runs the game
    /// owns, read by whatever cares (the look, the music, an actor's own mind) and told on the stream when a mode
    /// changes (<see cref="ModeChanged"/>). A mode is not a flag but a fact with a reason: an actor is in
    /// <see cref="Mode.Fight"/> against someone (<see cref="Fight"/>), and stays so until each opponent is
    /// <see cref="Settle"/>d (fallen, gone) and then <see cref="CoolOff"/> seconds more, as Skyrim's combat ends a
    /// while after the last foe, so the music needn't stop on the blow; a fight joined meanwhile goes on without a
    /// change. Everyone is <see cref="Mode.Normal"/> until something happens, and again when the game ends
    /// (<see cref="Clear"/>). What starts a fight is for others to say (<see cref="Forge.Game.Combat.FightCauses"/>: a
    /// blow). Engine-free; the game ticks it.
    /// </summary>
    public sealed class ActorModes
    {
        /// <summary>Seconds after the last opponent is settled that an actor is still in <see cref="Mode.Fight"/>.</summary>
        public const float DefaultCoolOff = 5f;

        static readonly ActorId[] NoOpponents = Array.Empty<ActorId>();

        readonly Dictionary<ActorId, Row> _fighting = new Dictionary<ActorId, Row>();
        readonly List<ActorId> _done = new List<ActorId>();
        readonly GameEvents _events;

        /// <param name="events">The game's stream, told each change; null to tell no one.</param>
        /// <param name="coolOff">Seconds an actor stays in <see cref="Mode.Fight"/> past its last opponent; 0: until the next tick.</param>
        public ActorModes(GameEvents events = null, float coolOff = DefaultCoolOff)
        {
            if (coolOff < 0f || float.IsNaN(coolOff))
                throw new ArgumentOutOfRangeException(nameof(coolOff), coolOff, "A cool-off is seconds.");
            _events = events;
            CoolOff = coolOff;
        }

        public float CoolOff { get; }

        public Mode ModeOf(ActorId actor) => _fighting.ContainsKey(actor) ? Mode.Fight : Mode.Normal;

        /// <summary>Whom <paramref name="actor"/> fights now; none at ease, or cooling off.</summary>
        public IReadOnlyCollection<ActorId> OpponentsOf(ActorId actor) =>
            _fighting.TryGetValue(actor, out var row) ? row.Opponents : NoOpponents;

        public bool Fights(ActorId actor, ActorId opponent) =>
            _fighting.TryGetValue(actor, out var row) && row.Opponents.Contains(opponent);

        /// <summary>
        /// <paramref name="actor"/> fights <paramref name="opponent"/>: it enters <see cref="Mode.Fight"/> (told), or adds
        /// the opponent to the fight it is in (a cool-off is called off). One way only: the opponent fights back when
        /// its own cause says so.
        /// </summary>
        public void Fight(ActorId actor, ActorId opponent)
        {
            if (actor.IsDefault || opponent.IsDefault)
                throw new ArgumentException("A fight is between actors.");
            if (actor == opponent)
                throw new ArgumentException("An actor can't fight itself.", nameof(opponent));

            if (!_fighting.TryGetValue(actor, out var row))
            {
                row = new Row();
                _fighting.Add(actor, row);
                _events?.Publish(new ModeChanged(actor, Mode.Normal, Mode.Fight));
            }

            row.Opponents.Add(opponent);
            row.Cooling = null;
        }

        /// <summary>
        /// <paramref name="actor"/> no longer fights <paramref name="opponent"/> (it fell, it got away): with no opponent
        /// left it cools off, and is at ease <see cref="CoolOff"/> seconds on. Nothing when it wasn't fighting it.
        /// </summary>
        public void Settle(ActorId actor, ActorId opponent)
        {
            if (!_fighting.TryGetValue(actor, out var row) || !row.Opponents.Remove(opponent))
                return;
            if (row.Opponents.Count == 0)
                row.Cooling = CoolOff;
        }

        /// <summary>
        /// <paramref name="actor"/> fell: its own fight is over at once (told), and whoever fought it is settled with it.
        /// </summary>
        public void Fallen(ActorId actor)
        {
            if (_fighting.Remove(actor))
                _events?.Publish(new ModeChanged(actor, Mode.Fight, Mode.Normal));
            foreach (var pair in _fighting)
                Settle(pair.Key, actor);
        }

        /// <summary>Lets <paramref name="deltaTime"/> seconds pass: a cool-off runs out and the actor is at ease (told).</summary>
        public void Tick(float deltaTime)
        {
            foreach (var pair in _fighting)
            {
                var row = pair.Value;
                if (row.Cooling == null)
                    continue;
                row.Cooling -= Math.Max(0f, deltaTime);
                if (row.Cooling <= 0f)
                    _done.Add(pair.Key);
            }

            foreach (var actor in _done)
                EndFight(actor);
            _done.Clear();
        }

        /// <summary>Everyone is at ease (told): the game ended.</summary>
        public void Clear()
        {
            foreach (var pair in _fighting)
                _done.Add(pair.Key);
            foreach (var actor in _done)
                EndFight(actor);
            _done.Clear();
        }

        void EndFight(ActorId actor)
        {
            if (_fighting.Remove(actor))
                _events?.Publish(new ModeChanged(actor, Mode.Fight, Mode.Normal));
        }

        // An actor in Fight: whom against, and once none is left, how long until it is at ease (null: not cooling).
        sealed class Row
        {
            public readonly HashSet<ActorId> Opponents = new HashSet<ActorId>();
            public float? Cooling;
        }
    }
}
