using System;
using System.Numerics;
using Forge.Core.Geometry;
using Forge.Game.Actors;
using Forge.Game.Equipment;

namespace Forge.Game.Minds
{
    /// <summary>
    /// How a character fights (<see cref="Fighter"/>): how near it comes to strike, how fast it closes in, how long it
    /// pauses between blows, and how often it raises its shield instead.
    /// </summary>
    public readonly struct FighterSettings
    {
        /// <param name="reach">Meters from its feet to its opponent's within which it strikes rather than walks.</param>
        /// <param name="walkSpeed">How fast it closes in, meters per second, near its opponent.</param>
        /// <param name="runSpeed">How fast it closes in from afar.</param>
        /// <param name="runBeyond">Meters from its opponent beyond which it runs rather than walks.</param>
        /// <param name="minPause">The shortest it waits between one blow (or block) and the next, seconds.</param>
        /// <param name="maxPause">The longest it waits.</param>
        /// <param name="blockChance">The share of its moves, 0 to 1, that raise the shield rather than strike.</param>
        /// <param name="blockSeconds">How long it holds the shield up, seconds.</param>
        public FighterSettings(float reach, float walkSpeed, float runSpeed, float runBeyond, float minPause, float maxPause, float blockChance,
            float blockSeconds)
        {
            if (!(reach > 0f) || float.IsInfinity(reach))
                throw new ArgumentOutOfRangeException(nameof(reach), reach, "Must be a positive number.");
            if (!(walkSpeed > 0f) || !(runSpeed >= walkSpeed) || float.IsInfinity(runSpeed))
                throw new ArgumentOutOfRangeException(nameof(runSpeed), runSpeed, "Speeds must be 0 < walk <= run.");
            if (!(runBeyond >= reach) || float.IsInfinity(runBeyond))
                throw new ArgumentOutOfRangeException(nameof(runBeyond), runBeyond, "It runs from beyond its reach at the nearest.");
            if (!(minPause >= 0f) || !(maxPause >= minPause) || float.IsInfinity(maxPause))
                throw new ArgumentOutOfRangeException(nameof(maxPause), maxPause, "Pauses must be 0 <= min <= max.");
            if (!(blockChance >= 0f) || !(blockChance <= 1f))
                throw new ArgumentOutOfRangeException(nameof(blockChance), blockChance, "A share, 0 to 1.");
            if (!(blockSeconds > 0f) || float.IsInfinity(blockSeconds))
                throw new ArgumentOutOfRangeException(nameof(blockSeconds), blockSeconds, "Must be a positive number.");
            Reach = reach;
            WalkSpeed = walkSpeed;
            RunSpeed = runSpeed;
            RunBeyond = runBeyond;
            MinPause = minPause;
            MaxPause = maxPause;
            BlockChance = blockChance;
            BlockSeconds = blockSeconds;
        }

        /// <summary>
        /// A sword-and-board fighter closing in at <paramref name="walkSpeed"/>, running at <paramref name="runSpeed"/>
        /// from beyond 5 m, striking within 1.4 m with 0.8 to 2 s between moves, a third of them the shield raised for
        /// 1.2 s.
        /// </summary>
        public static FighterSettings SwordAndBoard(float walkSpeed, float runSpeed) =>
            new FighterSettings(1.4f, walkSpeed, Math.Max(walkSpeed, runSpeed), 5f, 0.8f, 2f, 0.35f, 1.2f);

        public float Reach { get; }

        public float WalkSpeed { get; }

        public float RunSpeed { get; }

        public float RunBeyond { get; }

        public float MinPause { get; }

        public float MaxPause { get; }

        public float BlockChance { get; }

        public float BlockSeconds { get; }
    }

    /// <summary>
    /// A mind that fights back. At ease (<see cref="Mode.Normal"/>) it is another mind (its day: <see cref="Wander"/>),
    /// its weapons away. In a fight (<see cref="Mode.Fight"/>) it draws first, faces its nearest opponent, closes in
    /// (walking, running from afar) and in reach stands and strikes with its right hand, pausing between blows, now and
    /// then raising its shield for a while instead; its opponent gone, it stands ready. The fight over, it puts its
    /// weapons away and goes back to its day. What strikes it decides on its own dice (<see cref="Random"/>), so no two
    /// fighters keep the same time. Engine-free.
    /// </summary>
    public sealed class Fighter : IMind
    {
        readonly IMind _atEase;
        readonly FighterSettings _settings;
        readonly Random _random;

        // Seconds until its next move in reach, and seconds its shield stays up (0: it is down).
        float _pause;
        float _blocking;

        /// <param name="atEase">The mind it is while not fighting (its day).</param>
        /// <param name="random">Its own dice.</param>
        public Fighter(IMind atEase, FighterSettings settings, Random random)
        {
            _atEase = atEase ?? throw new ArgumentNullException(nameof(atEase));
            _settings = settings;
            _random = random ?? throw new ArgumentNullException(nameof(random));
            NextPause();
        }

        public MindDecision Decide(MindSight sight, float deltaTime)
        {
            if (!(deltaTime > 0f) || float.IsInfinity(deltaTime))
                throw new ArgumentOutOfRangeException(nameof(deltaTime), deltaTime, "Must be a positive number.");
            return sight.Mode == Mode.Fight ? Fight(sight, deltaTime) : AtEase(sight, deltaTime);
        }

        // Not fighting: weapons still out go away first (the shield down), standing; coming out or going back, it waits;
        // away, its day goes on.
        MindDecision AtEase(MindSight sight, float deltaTime)
        {
            switch (sight.Weapons)
            {
                case Readiness.Drawn:
                    _blocking = 0f;
                    return new MindDecision(Vector2.Zero, readyWeapon: true, releaseLeft: sight.GuardUp);
                case Readiness.Sheathed:
                    return _atEase.Decide(sight, deltaTime);
                default:
                    return MindDecision.Stand;
            }
        }

        // Fighting: draw, then face the opponent; out of reach close in, the shield down; in reach stand and, as each
        // pause runs out, strike or raise the shield for a while.
        MindDecision Fight(MindSight sight, float deltaTime)
        {
            var facing = sight.Opponent is Vector2 at && Vector2.DistanceSquared(at, sight.Position) > 1e-6f
                ? Compass.HeadingOf(at - sight.Position)
                : (float?)null;
            switch (sight.Weapons)
            {
                case Readiness.Sheathed:
                    return new MindDecision(Vector2.Zero, facing, readyWeapon: true);
                case Readiness.Drawing:
                case Readiness.Sheathing:
                    return new MindDecision(Vector2.Zero, facing);
            }

            if (sight.Opponent == null)
            {
                _blocking = 0f;
                return new MindDecision(Vector2.Zero, releaseLeft: sight.GuardUp);
            }

            var way = sight.Opponent.Value - sight.Position;
            var distance = way.Length();
            if (distance > _settings.Reach)
            {
                _blocking = 0f;
                var speed = distance > _settings.RunBeyond ? _settings.RunSpeed : _settings.WalkSpeed;
                return new MindDecision(way / distance * speed, facing, releaseLeft: sight.GuardUp);
            }

            if (_blocking > 0f)
            {
                _blocking -= deltaTime;
                return new MindDecision(Vector2.Zero, facing, releaseLeft: _blocking <= 0f);
            }

            if (sight.RightHandBusy || sight.GuardUp)
                return new MindDecision(Vector2.Zero, facing);
            _pause -= deltaTime;
            if (_pause > 0f)
                return new MindDecision(Vector2.Zero, facing);

            NextPause();
            if (_random.NextDouble() < _settings.BlockChance)
            {
                _blocking = _settings.BlockSeconds;
                return new MindDecision(Vector2.Zero, facing, useLeft: true);
            }

            return new MindDecision(Vector2.Zero, facing, useRight: true);
        }

        void NextPause() => _pause = _settings.MinPause + (float)_random.NextDouble() * (_settings.MaxPause - _settings.MinPause);
    }
}
