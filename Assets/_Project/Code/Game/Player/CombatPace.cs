using System;
using Forge.Game.Actors;

namespace Forge.Game.Player
{
    /// <summary>
    /// How the player moves in a fight (<see cref="Mode.Fight"/>, read off its <see cref="ActorModes"/> each frame): the
    /// everyday run gives way to a walk, so it circles its foe rather than dashing past, and the run key runs (the
    /// everyday pace) instead of sprinting; a walk forced on it already (too laden) stays one. At ease nothing changes.
    /// Moti's call: "in fight mode the character isn't running by default, only with the running key." A rule of the
    /// player's own controls (<see cref="IMovementLimit"/>): a character's mind picks its paces itself.
    /// </summary>
    public sealed class CombatPace : IMovementLimit
    {
        readonly ActorModes _modes;
        readonly ActorId _actor;

        /// <param name="modes">What every actor is up to, which says whether <paramref name="actor"/> fights.</param>
        /// <param name="actor">Whose pace this rules: the player's.</param>
        public CombatPace(ActorModes modes, ActorId actor)
        {
            _modes = modes ?? throw new ArgumentNullException(nameof(modes));
            if (actor.IsDefault)
                throw new ArgumentException("An actor has an id.", nameof(actor));
            _actor = actor;
        }

        /// <summary>Whether the actor fights now, and so walks unless told to run.</summary>
        public bool InAFight => _modes.ModeOf(_actor) == Mode.Fight;

        /// <summary>The commands as a fight lets them through: the run key runs, nothing sprints, and without it the player walks.</summary>
        public PlayerCommands Allow(PlayerCommands commands) =>
            InAFight
                ? commands.Allowing(sprint: false, jump: commands.Jump, walk: commands.Walk || !commands.Sprint)
                : commands;
    }
}
