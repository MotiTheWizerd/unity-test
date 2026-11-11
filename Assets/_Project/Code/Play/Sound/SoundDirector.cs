using System;
using System.Collections.Generic;
using Forge.Game.Actors;
using Forge.Game.Combat;
using Forge.Game.Events;
using Forge.Game.Player;
using UnityEngine;

namespace Forge.Play.Sound
{
    /// <summary>
    /// The game's happenings heard: it listens to the game's stream and plays each one's sound where its actor is, so
    /// the rules never play a sound, only say what happened (an actor jumped, a blow swung, a bow was drawn and loosed).
    /// Which sound each plays is a key under Content/Sounds (<see cref="Jump"/>: "foley/jump"; a combat move's moment and a
    /// shot's release, by its animation: <see cref="MoveOf"/>, "combat/swordslash", "combat/bowshot", "combat/bowrelease");
    /// one the catalog lacks plays nothing, so a sound is added by dropping its folder in and building. A sound of the
    /// actor's own body plays in its voice when it has a version in it (<see cref="Voiced"/>). Where an actor is
    /// comes from whoever runs the game (the player's feet today, the world's actors later).
    /// </summary>
    public sealed class SoundDirector : IDisposable
    {
        /// <summary>What an actor taking off in a jump plays.</summary>
        public const string Jump = "foley/jump";

        /// <summary>What a combat move's moment or a shot's release plays, by its animation ("SwordSlash": "combat/swordslash").</summary>
        public static string MoveOf(string animation) => "combat/" + animation.ToLowerInvariant();

        /// <summary>
        /// The version of <paramref name="key"/> made in <paramref name="voice"/> ("foley/jump", "female": "foley/jumpfemale",
        /// the folder Foley/JumpFemale); the key itself without a voice.
        /// </summary>
        public static string Voiced(string key, string voice) =>
            string.IsNullOrWhiteSpace(voice) ? key : key + voice.Trim().ToLowerInvariant();

        // Meters above its feet a weapon is heard from: about the hands.
        const float HandsHeight = 1.2f;

        readonly SoundPlayer _sounds;
        readonly Func<ActorId, Vector3?> _whereIs;
        readonly Func<ActorId, string> _voiceOf;
        readonly IDisposable[] _subscriptions;
        // Each actor's bow creaking as it is drawn, cut short when it is loosed.
        readonly Dictionary<ActorId, SoundHandle> _drawing = new Dictionary<ActorId, SoundHandle>();

        /// <param name="events">The game's stream, which it listens to.</param>
        /// <param name="whereIs">Where an actor is now (scene space), or null when it can't say (not in the scene).</param>
        /// <param name="voiceOf">The voice an actor's own sounds are made in (<see cref="CharacterVoice"/>), or null for none.</param>
        public SoundDirector(GameEvents events, SoundPlayer sounds, Func<ActorId, Vector3?> whereIs, Func<ActorId, string> voiceOf = null)
        {
            if (events == null)
                throw new ArgumentNullException(nameof(events));
            _sounds = sounds ?? throw new ArgumentNullException(nameof(sounds));
            _whereIs = whereIs ?? throw new ArgumentNullException(nameof(whereIs));
            _voiceOf = voiceOf ?? (_ => null);
            _subscriptions = new[]
            {
                events.Subscribe<ActorJumped>(e => PlayVoicedAt(e.Actor, Jump)),
                events.Subscribe<MoveWindowOpened>(OnMoveWindowOpened),
                events.Subscribe<ShotLoosed>(OnShotLoosed),
            };
        }

        // A blow's swish, a shield's raise; a bow's creak, kept to be cut short when it is loosed.
        void OnMoveWindowOpened(MoveWindowOpened e)
        {
            var sound = PlayAt(e.Actor, MoveOf(e.Animation), HandsHeight);
            if (e.Measure == MoveMeasure.Draw)
                _drawing[e.Actor] = sound;
        }

        // The draw's creak stops as the string snaps.
        void OnShotLoosed(ShotLoosed e)
        {
            if (_drawing.TryGetValue(e.Actor, out var drawing))
            {
                _sounds.Stop(drawing);
                _drawing.Remove(e.Actor);
            }

            if (e.Release != null)
                PlayAt(e.Actor, MoveOf(e.Release), HandsHeight);
        }

        // `key` where `actor` is (`above` its feet), when the actor is anywhere and the sound is known.
        SoundHandle PlayAt(ActorId actor, string key, float above = 0f) =>
            _whereIs(actor) is Vector3 where ? _sounds.Play(key, where + Vector3.up * above) : SoundHandle.None;

        // A sound of the actor's own body (a jump's breath): in its voice when there is a version in it, else as it is.
        void PlayVoicedAt(ActorId actor, string key)
        {
            if (_whereIs(actor) is Vector3 where)
                _sounds.PlayFirst(new[] { (Voiced(key, _voiceOf(actor)), 1f), (key, 1f) }, where);
        }

        public void Dispose()
        {
            foreach (var subscription in _subscriptions)
                subscription.Dispose();
        }
    }
}
