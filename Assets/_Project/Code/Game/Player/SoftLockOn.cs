using System;
using System.Numerics;
using Forge.Core.Geometry;
using Forge.Game.Actors;
using Forge.Game.Equipment;

namespace Forge.Game.Player
{
    /// <summary>
    /// The player's soft lock-on: in a fight (<see cref="Mode.Fight"/>), its weapons out, framed from behind (its camera
    /// mode turns the body to its <see cref="BodyFacing.Movement"/>), the body squares up to its nearest opponent standing
    /// within <see cref="Range"/> (<see cref="BodyFacing.Target"/>), so it closes in, sidesteps and backs away facing its
    /// foe. Soft, because nothing else is taken over: the camera stays the mouse's and the move stays relative to it, so
    /// the player turns away and runs when it means to. A drawn bow still squares up to the look
    /// (<see cref="PlayerCharacter.Aiming"/> comes first); in first person the body is the look. At ease, cooling off, its
    /// weapons away or its foe out of range, the body faces as the camera mode says. Engine-free; the player's session
    /// asks it each frame before the player moves.
    /// </summary>
    public sealed class SoftLockOn
    {
        /// <summary>Meters from the player's feet to its foe's within which it squares up to it.</summary>
        public const float DefaultRange = 8f;

        readonly ActorModes _modes;
        readonly ActorId _actor;

        /// <param name="modes">What every actor is up to, which says whom <paramref name="actor"/> fights.</param>
        /// <param name="actor">Whose body this turns: the player's.</param>
        /// <param name="range">Meters within which it squares up to its foe.</param>
        public SoftLockOn(ActorModes modes, ActorId actor, float range = DefaultRange)
        {
            _modes = modes ?? throw new ArgumentNullException(nameof(modes));
            if (actor.IsDefault)
                throw new ArgumentException("An actor has an id.", nameof(actor));
            if (!(range > 0f) || float.IsInfinity(range))
                throw new ArgumentOutOfRangeException(nameof(range), range, "Must be a positive number.");
            _actor = actor;
            Range = range;
        }

        public float Range { get; }

        /// <summary>Whom the body squared up to at the last <see cref="Face"/>; default when it faced as the camera says.</summary>
        public ActorId Foe { get; private set; }

        /// <summary>
        /// Turns <paramref name="body"/> as its camera mode says (<paramref name="view"/>), or onto its foe, standing in
        /// <paramref name="standing"/>, while its weapons are out or coming out (<paramref name="readiness"/>, as the
        /// drawn moves play).
        /// </summary>
        public void Face(PlayerCharacter body, BodyFacing view, Readiness readiness, IStandingActors standing)
        {
            if (body == null)
                throw new ArgumentNullException(nameof(body));

            body.Facing = view;
            Foe = default;
            var weaponsOut = readiness == Readiness.Drawing || readiness == Readiness.Drawn;
            if (view != BodyFacing.Movement || !weaponsOut
                || !Opponents.TryNearest(_modes, _actor, body.Feet, standing, out var foe, out var feet)
                || Vector3.Distance(body.Feet, feet) > Range)
                return;

            Foe = foe;
            body.Facing = BodyFacing.Target;
            // Right over or under its foe there is no way to it: the body keeps the way it faces.
            var toward = new Vector2(feet.X - body.Feet.X, feet.Z - body.Feet.Z);
            body.TargetHeading = toward.LengthSquared() > 1e-6f ? Compass.HeadingOf(toward) : body.BodyHeading;
        }
    }
}
