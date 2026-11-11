using System;
using System.Numerics;

namespace Forge.Game.Minds
{
    /// <summary>
    /// How a character strolls about its home (<see cref="Wander"/>): how far from it, how fast, and how long it stands
    /// between walks.
    /// </summary>
    public readonly struct WanderSettings
    {
        public WanderSettings(float radius, float speed, float minPause, float maxPause)
        {
            if (!(radius > 0f) || float.IsInfinity(radius))
                throw new ArgumentOutOfRangeException(nameof(radius), radius, "Must be a positive number.");
            if (!(speed > 0f) || float.IsInfinity(speed))
                throw new ArgumentOutOfRangeException(nameof(speed), speed, "Must be a positive number.");
            if (!(minPause >= 0f) || !(maxPause >= minPause) || float.IsInfinity(maxPause))
                throw new ArgumentOutOfRangeException(nameof(maxPause), maxPause, "Pauses must be 0 <= min <= max.");
            Radius = radius;
            Speed = speed;
            MinPause = minPause;
            MaxPause = maxPause;
        }

        /// <summary>
        /// A stroll: within 6 m of home, at 1.8 m/s, standing 2 to 6 s between walks. Whoever moves the body sets the speed
        /// its walk is made at (<see cref="WithSpeed"/>): slower, the walk blends with the idle and the feet slide.
        /// </summary>
        public static WanderSettings Stroll => new WanderSettings(6f, 1.8f, 2f, 6f);

        /// <summary>These settings walking at <paramref name="speed"/> meters per second instead.</summary>
        public WanderSettings WithSpeed(float speed) => new WanderSettings(Radius, speed, MinPause, MaxPause);

        /// <summary>How far from home it walks, meters.</summary>
        public float Radius { get; }

        /// <summary>How fast it walks, meters per second.</summary>
        public float Speed { get; }

        /// <summary>The shortest it stands between walks, seconds.</summary>
        public float MinPause { get; }

        /// <summary>The longest it stands between walks, seconds.</summary>
        public float MaxPause { get; }
    }

    /// <summary>
    /// The simplest mind a character of the world has: it stands a while, walks to a spot near its home, stands again,
    /// and so on, as Skyrim's townsfolk mill about. It only says how to walk (<see cref="Step"/>); when its body gets
    /// nowhere (someone, a wall in the way) it gives the walk up, stands, and goes elsewhere, as it does when told the
    /// way is blocked (<see cref="Blocked"/>). On the ground plane (x, z), in whatever space its home is given in.
    /// Engine-free.
    /// </summary>
    public sealed class Wander
    {
        // How many spots it tries for one far enough from where it stands to be worth the walk.
        const int Tries = 8;

        // A walk is given up after this many seconds of getting less than this share of the way it was sent.
        public const float StuckSeconds = 0.5f;
        const float StuckShare = 0.25f;

        readonly WanderSettings _settings;
        readonly Random _random;
        float _pause;

        // Where it stood when last asked, how far it was sent from there, and how long it has got nowhere.
        Vector2 _last;
        float _sent;
        float _stuckFor;

        /// <param name="random">Its own dice, so no two characters keep the same time.</param>
        public Wander(Vector2 home, WanderSettings settings, Random random)
        {
            Home = home;
            _settings = settings;
            _random = random ?? throw new ArgumentNullException(nameof(random));
            // It starts standing, for a while of its own, so a crowd doesn't set off as one.
            _pause = (float)_random.NextDouble() * settings.MaxPause;
        }

        /// <summary>Where it strolls about.</summary>
        public Vector2 Home { get; }

        /// <summary>Where it is walking to; null while it stands.</summary>
        public Vector2? Target { get; private set; }

        /// <summary>
        /// How it walks from <paramref name="position"/> over the next <paramref name="deltaTime"/> seconds: its velocity
        /// over the ground, meters per second; zero while it stands. The last step of a walk lands on the spot.
        /// </summary>
        public Vector2 Step(Vector2 position, float deltaTime)
        {
            if (!(deltaTime > 0f) || float.IsInfinity(deltaTime))
                throw new ArgumentOutOfRangeException(nameof(deltaTime), deltaTime, "Must be a positive number.");
            if (GotNowhere(position, deltaTime))
                Stand();
            if (!Target.HasValue)
            {
                _pause -= deltaTime;
                if (_pause > 0f)
                    return Sent(position, Vector2.Zero, deltaTime);
                Target = Pick(position);
            }

            var way = Target.Value - position;
            var distance = way.Length();
            if (distance > _settings.Speed * deltaTime)
                return Sent(position, way / distance * _settings.Speed, deltaTime);

            Stand();
            return Sent(position, way / deltaTime, deltaTime);
        }

        /// <summary>Something stands in its way: it stops where it is, and after a while walks somewhere else.</summary>
        public void Blocked()
        {
            if (Target.HasValue)
                Stand();
        }

        // Walking, it got less than its share of the way it was sent since last asked: once that has lasted StuckSeconds,
        // it gives the walk up.
        bool GotNowhere(Vector2 position, float deltaTime)
        {
            if (!Target.HasValue || !(_sent > 0f))
                return false;
            _stuckFor = Vector2.Distance(position, _last) < _sent * StuckShare ? _stuckFor + deltaTime : 0f;
            return _stuckFor >= StuckSeconds;
        }

        // What it asks of its body, remembered to see next time whether the body got there.
        Vector2 Sent(Vector2 position, Vector2 velocity, float deltaTime)
        {
            _last = position;
            _sent = Target.HasValue ? velocity.Length() * deltaTime : 0f;
            return velocity;
        }

        void Stand()
        {
            Target = null;
            _stuckFor = 0f;
            _pause = _settings.MinPause + (float)_random.NextDouble() * (_settings.MaxPause - _settings.MinPause);
        }

        // A spot within its radius of home, evenly over the disk, at least a third of the radius from where it stands
        // when one of a few tries is (else the farthest tried).
        Vector2 Pick(Vector2 position)
        {
            var best = Home;
            var bestDistance = -1f;
            for (var i = 0; i < Tries; i++)
            {
                var angle = _random.NextDouble() * 2.0 * Math.PI;
                var reach = _settings.Radius * (float)Math.Sqrt(_random.NextDouble());
                var spot = Home + new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * reach;
                var distance = Vector2.Distance(spot, position);
                if (distance >= _settings.Radius / 3f)
                    return spot;
                if (distance > bestDistance)
                {
                    best = spot;
                    bestDistance = distance;
                }
            }

            return best;
        }
    }
}
