using System;
using System.Collections.Generic;
using System.Threading;

namespace Forge.Game.Events
{
    /// <summary>
    /// The game's event stream: what happened, told to whoever cares, without the teller knowing who listens. Quests,
    /// the HUD, sound, AI and the world's characters' memories all hear the same facts (<see cref="IGameEvent"/>).
    /// <para>
    /// Only happenings go on it: a resource ran out, an actor fell. Numbers that change every frame (a bar refilling)
    /// stay with their owner's own events, read by the one thing that draws them.
    /// </para>
    /// <para>
    /// <see cref="Publish{T}"/> only queues; <see cref="Flush"/> tells, in the order things were published, whatever their
    /// type. The game's loop flushes once a frame, after the frame's rules have run, so no listener ever runs inside a
    /// half-done step. What listeners publish while being told is told in the same flush, after what came before it; a
    /// chain that never ends is cut off after <see cref="MaxPerFlush"/> and reported. Listeners hear in the order they
    /// subscribed; one that throws is reported and the rest still hear. A listener stops hearing the moment its
    /// subscription is disposed, even mid-flush.
    /// </para>
    /// Engine-free and allocation-free in steady play (events are structs, queues and lists keep their capacity). Not
    /// thread-safe: it belongs to the game's loop.
    /// </summary>
    public sealed class GameEvents
    {
        /// <summary>The most events one <see cref="Flush"/> tells before it decides listeners are publishing in a loop.</summary>
        public const int MaxPerFlush = 10000;

        readonly Action<Exception> _onError;
        readonly List<Channel> _order = new List<Channel>();
        Channel[] _channels = new Channel[16];
        AllSubscription[] _all = Array.Empty<AllSubscription>();
        List<Exception> _errors;
        int _next;
        bool _flushing;

        /// <param name="onError">Where a listener's exception, or a runaway chain, is reported (the engine's log); null
        /// to throw them from <see cref="Flush"/>, all together, once every listener has been told.</param>
        public GameEvents(Action<Exception> onError = null) => _onError = onError;

        /// <summary>Events published and not yet told.</summary>
        public int Pending => _order.Count - _next;

        /// <summary>Queues <paramref name="e"/>, to be told at the next <see cref="Flush"/> (or later in this one, while flushing).</summary>
        public void Publish<T>(T e) where T : struct, IGameEvent
        {
            var channel = ChannelOf<T>();
            channel.Enqueue(e);
            _order.Add(channel);
        }

        /// <summary>Tells <paramref name="handler"/> every <typeparamref name="T"/> from now on, until the returned subscription is disposed.</summary>
        public IDisposable Subscribe<T>(Action<T> handler) where T : struct, IGameEvent
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));
            return ChannelOf<T>().Add(handler);
        }

        /// <summary>
        /// Tells <paramref name="handler"/> every event, after its own listeners, until disposed: for a log, a recording,
        /// a memory. Each event is boxed for it, so it is for those, not for gameplay.
        /// </summary>
        public IDisposable SubscribeAll(Action<IGameEvent> handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));
            var subscription = new AllSubscription(this, handler);
            _all = Appended(_all, subscription);
            return subscription;
        }

        /// <summary>Tells every queued event, and whatever their listeners publish, in order. Does nothing when already flushing.</summary>
        public void Flush()
        {
            if (_flushing)
                return;

            _flushing = true;
            try
            {
                while (_next < _order.Count)
                {
                    if (_next >= MaxPerFlush)
                    {
                        Report(new InvalidOperationException(
                            $"Events kept publishing events: {MaxPerFlush} told in one flush and {_order.Count - _next} more dropped. " +
                            "A listener is likely publishing what it hears."));
                        break;
                    }

                    _order[_next++].TellNext(this);
                }
            }
            finally
            {
                // Whatever was not told (a runaway chain) leaves its queue too, so queues and order stay in step.
                for (var i = _next; i < _order.Count; i++)
                    _order[i].DropNext();
                _order.Clear();
                _next = 0;
                _flushing = false;
            }

            if (_errors != null)
            {
                var errors = _errors;
                _errors = null;
                throw new AggregateException("Game event listeners failed.", errors);
            }
        }

        void Report(Exception error)
        {
            if (_onError != null)
                _onError(error);
            else
                (_errors ??= new List<Exception>()).Add(error);
        }

        void TellAll<T>(T e) where T : struct, IGameEvent
        {
            var all = _all;
            if (all.Length == 0)
                return;
            IGameEvent boxed = e;
            foreach (var subscription in all)
            {
                if (subscription.Disposed)
                    continue;
                try
                {
                    subscription.Handler(boxed);
                }
                catch (Exception error)
                {
                    Report(error);
                }
            }
        }

        Channel<T> ChannelOf<T>() where T : struct, IGameEvent
        {
            var index = TypeIndex<T>.Value;
            if (index >= _channels.Length)
                Array.Resize(ref _channels, Math.Max(index + 1, _channels.Length * 2));
            return (Channel<T>)(_channels[index] ??= new Channel<T>());
        }

        void Remove(AllSubscription subscription) => _all = Removed(_all, subscription);

        // Listener lists are copied on change, never in place, so a flush walks a list nobody changes under it.
        static TItem[] Appended<TItem>(TItem[] items, TItem item)
        {
            var copy = new TItem[items.Length + 1];
            Array.Copy(items, copy, items.Length);
            copy[items.Length] = item;
            return copy;
        }

        static TItem[] Removed<TItem>(TItem[] items, TItem item) where TItem : class
        {
            var index = Array.IndexOf(items, item);
            if (index < 0)
                return items;
            var copy = new TItem[items.Length - 1];
            Array.Copy(items, copy, index);
            Array.Copy(items, index + 1, copy, index, items.Length - index - 1);
            return copy;
        }

        abstract class Channel
        {
            public abstract void TellNext(GameEvents events);

            public abstract void DropNext();
        }

        sealed class Channel<T> : Channel where T : struct, IGameEvent
        {
            readonly Queue<T> _queue = new Queue<T>();
            Subscription[] _subscriptions = Array.Empty<Subscription>();

            public void Enqueue(T e) => _queue.Enqueue(e);

            public override void TellNext(GameEvents events)
            {
                var e = _queue.Dequeue();
                foreach (var subscription in _subscriptions)
                {
                    if (subscription.Disposed)
                        continue;
                    try
                    {
                        subscription.Handler(e);
                    }
                    catch (Exception error)
                    {
                        events.Report(error);
                    }
                }

                events.TellAll(e);
            }

            public override void DropNext() => _queue.Dequeue();

            public IDisposable Add(Action<T> handler)
            {
                var subscription = new Subscription(this, handler);
                _subscriptions = Appended(_subscriptions, subscription);
                return subscription;
            }

            void Remove(Subscription subscription) => _subscriptions = Removed(_subscriptions, subscription);

            sealed class Subscription : IDisposable
            {
                readonly Channel<T> _channel;

                public Subscription(Channel<T> channel, Action<T> handler)
                {
                    _channel = channel;
                    Handler = handler;
                }

                public Action<T> Handler { get; }

                public bool Disposed { get; private set; }

                public void Dispose()
                {
                    if (Disposed)
                        return;
                    Disposed = true;
                    _channel.Remove(this);
                }
            }
        }

        sealed class AllSubscription : IDisposable
        {
            readonly GameEvents _events;

            public AllSubscription(GameEvents events, Action<IGameEvent> handler)
            {
                _events = events;
                Handler = handler;
            }

            public Action<IGameEvent> Handler { get; }

            public bool Disposed { get; private set; }

            public void Dispose()
            {
                if (Disposed)
                    return;
                Disposed = true;
                _events.Remove(this);
            }
        }

        // A small number for each event type, the same for every stream, so finding a type's channel is an array read.
        static class TypeIndex
        {
            static int _count;

            public static int Next() => Interlocked.Increment(ref _count) - 1;
        }

        static class TypeIndex<T>
        {
            public static readonly int Value = TypeIndex.Next();
        }
    }
}
