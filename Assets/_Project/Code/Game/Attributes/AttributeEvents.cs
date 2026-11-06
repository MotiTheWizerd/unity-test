using System;
using Forge.Data;
using Forge.Game.Actors;
using Forge.Game.Events;

namespace Forge.Game.Attributes
{
    /// <summary>A resource of an actor ran out: what it has left reached its min (health at zero, stamina spent).</summary>
    public readonly struct ResourceDepleted : IGameEvent
    {
        public ResourceDepleted(ActorId actor, RecordId attribute)
        {
            Actor = actor;
            Attribute = attribute;
        }

        public ActorId Actor { get; }

        public RecordId Attribute { get; }

        public override string ToString() => $"{Actor}: {Attribute} ran out";
    }

    /// <summary>A resource of an actor is full again.</summary>
    public readonly struct ResourceRefilled : IGameEvent
    {
        public ResourceRefilled(ActorId actor, RecordId attribute)
        {
            Actor = actor;
            Attribute = attribute;
        }

        public ActorId Actor { get; }

        public RecordId Attribute { get; }

        public override string ToString() => $"{Actor}: {Attribute} full";
    }

    /// <summary>
    /// Puts what happens to an actor's attributes on the game's stream (<see cref="GameEvents"/>): a resource running out
    /// (<see cref="ResourceDepleted"/>) or filling up (<see cref="ResourceRefilled"/>), named by the attributes' owner.
    /// The numbers themselves, changing every frame, stay on <see cref="ActorAttributes.Changed"/>.
    /// </summary>
    public static class AttributeEvents
    {
        /// <summary>Tells <paramref name="events"/> what happens to <paramref name="attributes"/> until the returned binding is disposed.</summary>
        public static IDisposable Bind(ActorAttributes attributes, GameEvents events) => new Binding(attributes, events);

        sealed class Binding : IDisposable
        {
            readonly ActorAttributes _attributes;
            readonly GameEvents _events;
            bool _disposed;

            public Binding(ActorAttributes attributes, GameEvents events)
            {
                _attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
                _events = events ?? throw new ArgumentNullException(nameof(events));
                _attributes.Depleted += OnDepleted;
                _attributes.Refilled += OnRefilled;
            }

            public void Dispose()
            {
                if (_disposed)
                    return;
                _disposed = true;
                _attributes.Depleted -= OnDepleted;
                _attributes.Refilled -= OnRefilled;
            }

            void OnDepleted(RecordId attribute) => _events.Publish(new ResourceDepleted(_attributes.Owner, attribute));

            void OnRefilled(RecordId attribute) => _events.Publish(new ResourceRefilled(_attributes.Owner, attribute));
        }
    }
}
