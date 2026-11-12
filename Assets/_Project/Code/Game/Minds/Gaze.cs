using System;
using System.Collections.Generic;
using System.Numerics;
using Forge.Core.Geometry;
using Forge.Game.Actors;

namespace Forge.Game.Minds
{
    /// <summary>
    /// How a character's eyes go (<see cref="Gaze"/>): how near someone must come to be noticed, how far they go before
    /// it looks away, how far round its eyes reach, and how long it keeps on someone it lost sight of.
    /// </summary>
    public readonly struct GazeSettings
    {
        /// <param name="notice">Meters (level) within which it looks at someone.</param>
        /// <param name="letGo">Meters (level) past which it looks away; <paramref name="notice"/> or more, so someone at the edge isn't looked at and away by turns.</param>
        /// <param name="sight">Degrees either side of where its body faces that it sees: in front of it and beside it, not behind.</param>
        /// <param name="linger">Seconds it keeps on someone out of sight before it looks away (someone passing behind a post is followed through).</param>
        public GazeSettings(float notice, float letGo, float sight, float linger)
        {
            if (!(notice > 0f) || float.IsInfinity(notice))
                throw new ArgumentOutOfRangeException(nameof(notice), notice, "Must be a positive number.");
            if (!(letGo >= notice) || float.IsInfinity(letGo))
                throw new ArgumentOutOfRangeException(nameof(letGo), letGo, "Must be the notice distance or more.");
            if (!(sight > 0f) || sight > 180f)
                throw new ArgumentOutOfRangeException(nameof(sight), sight, "Degrees either side, more than 0 to 180.");
            if (!(linger >= 0f) || float.IsInfinity(linger))
                throw new ArgumentOutOfRangeException(nameof(linger), linger, "Must be 0 or more seconds.");
            Notice = notice;
            LetGo = letGo;
            Sight = sight;
            Linger = linger;
        }

        /// <summary>Someone's: looks at whoever comes within 4 m before it or beside it (100° either side), away past 5 m or after half a second out of sight.</summary>
        public static GazeSettings Person => new GazeSettings(4f, 5f, 100f, 0.5f);

        /// <summary>Meters (level) within which it looks at someone.</summary>
        public float Notice { get; }

        /// <summary>Meters (level) past which it looks away.</summary>
        public float LetGo { get; }

        /// <summary>Degrees either side of where its body faces that it sees.</summary>
        public float Sight { get; }

        /// <summary>Seconds it keeps on someone out of sight before it looks away.</summary>
        public float Linger { get; }
    }

    /// <summary>
    /// Someone a character takes an interest in (<see cref="Gaze"/>): who, where their eyes are (the scene's), and
    /// whether it is focused on them (<see cref="Focused"/>: a foe) or only glances at them when they come near (someone
    /// passing by).
    /// </summary>
    public readonly struct GazeInterest
    {
        /// <param name="focused">Whether it keeps its eyes on them wherever they stand, seen or not, for as long as they are of interest.</param>
        public GazeInterest(ActorId actor, Vector3 eyes, bool focused = false)
        {
            Actor = actor;
            Eyes = eyes;
            Focused = focused;
        }

        public ActorId Actor { get; }

        public Vector3 Eyes { get; }

        /// <summary>
        /// Whether it keeps its eyes on them wherever they stand, seen or not, for as long as they are of interest: a foe
        /// it fights, whose every move it watches (and its mind knows where it is).
        /// </summary>
        public bool Focused { get; }
    }

    /// <summary>
    /// Whom a character looks at, its head turned to them while its body goes about its business (the look turns it,
    /// as far as a neck turns): the first of those it takes an interest in (<see cref="GazeInterest"/>, most
    /// interesting first: its foe, then the player passing by) that it notices: one it is focused on
    /// (<see cref="GazeInterest.Focused"/>, a foe) wherever they stand, seen or not; anyone else within
    /// <see cref="GazeSettings.Notice"/>, before it or beside it (<see cref="GazeSettings.Sight"/> of where its body
    /// faces) and seen (<see cref="ILineOfSight"/>: no wall between their eyes). Once looking, it keeps on them as they
    /// go: on one it is focused on for as long as they are of interest; on anyone else until they are past
    /// <see cref="GazeSettings.LetGo"/>, behind it or out of sight for longer than <see cref="GazeSettings.Linger"/>; and
    /// until someone more interesting is noticed. Someone of no more interest (gone, fallen, the fight over) is let go
    /// at once. Engine-free; positions are the scene's.
    /// </summary>
    public sealed class Gaze
    {
        readonly ILineOfSight _sight;
        readonly float _sightCosine;
        float _unseen;

        public Gaze(GazeSettings settings, ILineOfSight sight)
        {
            _sight = sight ?? throw new ArgumentNullException(nameof(sight));
            Settings = settings;
            _sightCosine = (float)Math.Cos(settings.Sight * Compass.DegreesToRadians);
        }

        public GazeSettings Settings { get; }

        /// <summary>Whom it looks at; default when no one.</summary>
        public ActorId Target { get; private set; }

        /// <summary>Whether it looks at anyone.</summary>
        public bool Looking => !Target.IsDefault;

        /// <summary>
        /// Looks about for <paramref name="deltaTime"/> seconds from <paramref name="eyes"/>, its body facing
        /// <paramref name="heading"/> (compass degrees), among <paramref name="interests"/>, most interesting first.
        /// </summary>
        public void Look(Vector3 eyes, float heading, IReadOnlyList<GazeInterest> interests, float deltaTime)
        {
            if (interests == null)
                throw new ArgumentNullException(nameof(interests));

            var forward = Compass.Forward(heading);
            var current = Looking ? IndexOf(interests, Target) : -1;
            if (Looking && current < 0)
                LookAway();

            // Someone noticed who is more interesting than whom it looks at (than no one: anyone) takes its eye.
            var before = current < 0 ? interests.Count : current;
            for (var i = 0; i < before; i++)
            {
                if (!Sees(eyes, forward, interests[i], Settings.Notice))
                    continue;
                Target = interests[i].Actor;
                _unseen = 0f;
                return;
            }

            if (current < 0)
                return;
            if (Sees(eyes, forward, interests[current], Settings.LetGo))
            {
                _unseen = 0f;
                return;
            }

            _unseen += Math.Max(0f, deltaTime);
            if (_unseen > Settings.Linger)
                LookAway();
        }

        /// <summary>Looks at no one (it fell, it was taken away); it notices anew from the next <see cref="Look"/>.</summary>
        public void LookAway()
        {
            Target = default;
            _unseen = 0f;
        }

        // One it is focused on, always; anyone else within `range` (level), within its sight of where it faces, and nothing
        // between: right over or under it, someone is seen whichever way it faces.
        bool Sees(Vector3 eyes, Vector2 forward, GazeInterest interest, float range)
        {
            if (interest.Focused)
                return true;
            var other = interest.Eyes;
            var level = new Vector2(other.X - eyes.X, other.Z - eyes.Z);
            var apart = level.Length();
            if (apart > range)
                return false;
            if (apart > 1e-4f && Vector2.Dot(level / apart, forward) < _sightCosine)
                return false;
            return _sight.Clear(eyes, other);
        }

        static int IndexOf(IReadOnlyList<GazeInterest> interests, ActorId actor)
        {
            for (var i = 0; i < interests.Count; i++)
            {
                if (interests[i].Actor == actor)
                    return i;
            }

            return -1;
        }
    }
}
