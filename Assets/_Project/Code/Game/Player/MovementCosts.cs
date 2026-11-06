using System;
using System.Linq;
using Forge.Data;
using Forge.Game.Attributes;
using Forge.Game.Motion;

namespace Forge.Game.Player
{
    /// <summary>
    /// What moving costs, paid from the actor's attributes (<see cref="CoreAttributes"/>), as in Skyrim: sprinting spends
    /// <see cref="CoreAttributes.SprintCost"/> stamina a second, and running out leaves the actor winded (no sprint) until
    /// a quarter of its stamina is back; a jump needs and spends <see cref="CoreAttributes.JumpCost"/>; a fall past
    /// <see cref="CoreAttributes.FallSafeHeight"/> costs <see cref="CoreAttributes.FallDamage"/> health a meter. The
    /// costs are attributes, so items and effects change them through modifiers. Engine-free.
    /// </summary>
    public sealed class MovementCosts
    {
        /// <summary>The share of its stamina a winded actor must get back before it sprints again.</summary>
        public const float RecoverFraction = 0.25f;

        readonly FallTracker _fall = new FallTracker();

        static readonly RecordId[] Needs =
        {
            CoreAttributes.Stamina, CoreAttributes.Health, CoreAttributes.SprintCost, CoreAttributes.JumpCost,
            CoreAttributes.FallSafeHeight, CoreAttributes.FallDamage,
        };

        public MovementCosts(ActorAttributes attributes)
        {
            Attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
            var missing = Needs.Where(id => !attributes.Set.Contains(id)).Select(id => $"Moving costs need the attribute {id}, which the world lacks.").ToArray();
            if (missing.Length > 0)
                throw new DataValidationException(missing);
        }

        public ActorAttributes Attributes { get; }

        /// <summary>Out of stamina from sprinting: no sprint until <see cref="RecoverFraction"/> of it is back.</summary>
        public bool Winded { get; private set; }

        /// <summary>A move was refused for want of an attribute (stamina for a jump, or running dry mid-sprint).</summary>
        public event Action<RecordId> ShortOf;

        /// <summary>
        /// The commands as the actor can afford them over <paramref name="deltaTime"/> seconds, spending the sprint's
        /// stamina: no sprint while winded, no jump without the stamina for it.
        /// </summary>
        public PlayerCommands Afford(PlayerCommands commands, float deltaTime)
        {
            if (Winded && Attributes.Fraction(CoreAttributes.Stamina) >= RecoverFraction)
                Winded = false;

            var sprint = commands.Sprint && !Winded && commands.Move.LengthSquared() > 1e-6f;
            if (sprint && deltaTime > 0f)
            {
                var wanted = Attributes.Value(CoreAttributes.SprintCost) * deltaTime;
                if (Attributes.Drain(CoreAttributes.Stamina, wanted) < wanted)
                {
                    Winded = true;
                    sprint = false;
                    ShortOf?.Invoke(CoreAttributes.Stamina);
                }
            }

            var jump = commands.Jump;
            if (jump && Attributes.Current(CoreAttributes.Stamina) < Attributes.Value(CoreAttributes.JumpCost))
            {
                jump = false;
                ShortOf?.Invoke(CoreAttributes.Stamina);
            }

            return new PlayerCommands(commands.Move, commands.Turn, sprint, jump, commands.ToggleWalk);
        }

        /// <summary>
        /// Pays for what the move did: a jump taken off, and a fall that ended this frame, the body standing on
        /// something or not with its feet at <paramref name="height"/>.
        /// </summary>
        public void Pay(bool jumped, bool grounded, float height)
        {
            if (jumped)
                Attributes.Drain(CoreAttributes.Stamina, Attributes.Value(CoreAttributes.JumpCost));

            var fell = _fall.Step(grounded, height);
            var past = fell - Attributes.Value(CoreAttributes.FallSafeHeight);
            if (past > 0f)
                Attributes.Drain(CoreAttributes.Health, past * Attributes.Value(CoreAttributes.FallDamage));
        }

        /// <summary>The world moved under the actor by <paramref name="dy"/> (a rebase).</summary>
        public void Shift(float dy) => _fall.Shift(dy);

        /// <summary>The actor was put somewhere: no fall in progress.</summary>
        public void Reset() => _fall.Reset();
    }
}
