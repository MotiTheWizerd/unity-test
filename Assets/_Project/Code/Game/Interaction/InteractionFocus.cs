using System;
using System.Collections.Generic;
using System.Numerics;
using Forge.Data;
using Forge.Data.Plugins;
using Forge.Game.Actors;
using Forge.Game.Cameras;

namespace Forge.Game.Interaction
{
    /// <summary>
    /// What an actor has in its sights and what using it would do: the prompt shows <see cref="Offer"/>, the use key
    /// does it (<see cref="Activate"/>). Each frame <see cref="Update"/> looks along the actor's view
    /// (<see cref="ReachRay"/>, through an <see cref="IInteractionProbe"/>) and keeps the first object some interaction
    /// makes an offer for. An offer is worked out again only when what is in sight changes (records are immutable, so a
    /// changed record is another one) or <see cref="Refresh"/> says the rules changed, so steady looking costs nothing.
    /// Engine-free.
    /// </summary>
    public sealed class InteractionFocus
    {
        readonly ActorId _actor;
        readonly IRecordStore _records;
        readonly Interactions _interactions;
        readonly IInteractionProbe _probe;
        readonly float _reach;
        readonly List<RecordId> _found = new List<RecordId>(2);
        IInteraction _interaction;

        /// <param name="records">The world the actor plays in, which targets are read from.</param>
        /// <param name="reach">How far from its eyes the actor reaches, in meters.</param>
        public InteractionFocus(ActorId actor, IRecordStore records, Interactions interactions, IInteractionProbe probe, float reach)
        {
            if (actor.IsDefault)
                throw new ArgumentException("A focus belongs to an actor.", nameof(actor));
            if (!(reach > 0f) || float.IsInfinity(reach))
                throw new ArgumentOutOfRangeException(nameof(reach), reach, "Reach is a positive number of meters.");

            _actor = actor;
            _records = records ?? throw new ArgumentNullException(nameof(records));
            _interactions = interactions ?? throw new ArgumentNullException(nameof(interactions));
            _probe = probe ?? throw new ArgumentNullException(nameof(probe));
            _reach = reach;
        }

        /// <summary>What is in sight with something to do to it; none when <see cref="Offer"/> is null.</summary>
        public InteractionTarget Target { get; private set; }

        /// <summary>What the use key would do now, or null: nothing in sight to use.</summary>
        public InteractionOffer Offer { get; private set; }

        /// <summary>Looks along this frame's <paramref name="view"/> from an actor whose eyes are at <paramref name="eyes"/>.</summary>
        public void Update(ViewPose view, Vector3 eyes)
        {
            _found.Clear();
            if (ReachRay.TryGet(view, eyes, _reach, out var from, out var direction, out var length))
                _probe.Find(from, direction, length, _found);

            foreach (var id in _found)
            {
                if (!InteractionTarget.TryResolve(_records, id, out var target))
                    continue;
                if (Offer != null && target.Is(Target))
                    return;
                if (_interactions.TryOffer(target, _actor, out var interaction, out var offer))
                {
                    Focus(target, interaction, offer);
                    return;
                }
            }

            Focus(default, null, null);
        }

        /// <summary>
        /// Does what is on offer; false when nothing is. The focus lets go of what it used, so the next
        /// <see cref="Update"/> finds what is there now.
        /// </summary>
        public bool Activate()
        {
            if (Offer == null)
                return false;

            var target = Target;
            var interaction = _interaction;
            Focus(default, null, null);
            interaction.Perform(target, _actor);
            return true;
        }

        /// <summary>Asks for the offer again at the next <see cref="Update"/> (something it depends on changed).</summary>
        public void Refresh() => Focus(default, null, null);

        void Focus(InteractionTarget target, IInteraction interaction, InteractionOffer offer)
        {
            Target = target;
            _interaction = interaction;
            Offer = offer;
        }
    }
}
