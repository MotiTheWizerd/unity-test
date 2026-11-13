using System;
using System.Numerics;
using Forge.Core.Geometry;
using Forge.Game.Actors;
using Forge.Game.Equipment;

namespace Forge.Game.Minds
{
    /// <summary>
    /// How a character fights (<see cref="Fighter"/>): how near it comes to strike, how near it lets its opponent come,
    /// how fast it closes in, how long it pauses between blows, and how often it raises its shield instead.
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
        /// <param name="standOff">Meters from its opponent closer than which it steps back, facing it, to fighting distance
        /// (<see cref="SpacedAt"/>); 0: it never gives ground.</param>
        public FighterSettings(float reach, float walkSpeed, float runSpeed, float runBeyond, float minPause, float maxPause, float blockChance,
            float blockSeconds, float standOff = 0f)
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
            if (!(standOff >= 0f) || !(standOff < reach))
                throw new ArgumentOutOfRangeException(nameof(standOff), standOff, "It gives ground short of its reach, or never (0).");
            Reach = reach;
            StandOff = standOff;
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
        /// 1.2 s, and stepping back to 1.25 m when its opponent comes within 1.1 m (two guards apart, and a little).
        /// </summary>
        public static FighterSettings SwordAndBoard(float walkSpeed, float runSpeed) =>
            new FighterSettings(1.4f, walkSpeed, Math.Max(walkSpeed, runSpeed), 5f, 0.8f, 2f, 0.35f, 1.2f, standOff: 1.1f);

        public float Reach { get; }

        /// <summary>Meters from its opponent closer than which it steps back; 0: it never gives ground.</summary>
        public float StandOff { get; }

        /// <summary>Where a step back ends: halfway between its stand-off and its reach, so it doesn't sway at one line.</summary>
        public float SpacedAt => (StandOff + Reach) * 0.5f;

        public float WalkSpeed { get; }

        public float RunSpeed { get; }

        public float RunBeyond { get; }

        public float MinPause { get; }

        public float MaxPause { get; }

        public float BlockChance { get; }

        public float BlockSeconds { get; }
    }

    /// <summary>
    /// How a character fights with a bow (<see cref="Fighter"/>): how near it comes to shoot, how near it lets its
    /// opponent come, how long it waits between shots and holds a full draw before it looses, and how true it aims.
    /// It closes in as it would to strike (<see cref="FighterSettings"/>' paces).
    /// </summary>
    public readonly struct ArcherySettings
    {
        /// <param name="range">Meters from its opponent within which it shoots rather than closes in.</param>
        /// <param name="standOff">Meters from its opponent closer than which it backs away, facing it and still shooting,
        /// to <paramref name="spacedAt"/>; 0: it never gives ground.</param>
        /// <param name="spacedAt">Where backing away ends: beyond its stand-off, within its range.</param>
        /// <param name="minPause">The shortest it waits between one shot (the bow brought down) and drawing the next, seconds.</param>
        /// <param name="maxPause">The longest it waits.</param>
        /// <param name="minAim">The shortest it aims at full draw before it looses, seconds.</param>
        /// <param name="maxAim">The longest it aims.</param>
        /// <param name="spread">Degrees at most a shot strays from a true one, each shot its own way.</param>
        public ArcherySettings(float range, float standOff, float spacedAt, float minPause, float maxPause, float minAim, float maxAim,
            float spread)
        {
            if (!(range > 0f) || float.IsInfinity(range))
                throw new ArgumentOutOfRangeException(nameof(range), range, "Must be a positive number.");
            if (!(standOff >= 0f) || !(spacedAt > standOff) || !(spacedAt <= range))
                throw new ArgumentOutOfRangeException(nameof(spacedAt), spacedAt, "Distances must be 0 <= stand-off < spaced at <= range.");
            if (!(minPause >= 0f) || !(maxPause >= minPause) || float.IsInfinity(maxPause))
                throw new ArgumentOutOfRangeException(nameof(maxPause), maxPause, "Pauses must be 0 <= min <= max.");
            if (!(minAim >= 0f) || !(maxAim >= minAim) || float.IsInfinity(maxAim))
                throw new ArgumentOutOfRangeException(nameof(maxAim), maxAim, "Aims must be 0 <= min <= max.");
            if (!(spread >= 0f) || !(spread < 45f))
                throw new ArgumentOutOfRangeException(nameof(spread), spread, "Degrees, 0 (true every time) to under 45.");
            Range = range;
            StandOff = standOff;
            SpacedAt = spacedAt;
            MinPause = minPause;
            MaxPause = maxPause;
            MinAim = minAim;
            MaxAim = maxAim;
            Spread = spread;
        }

        /// <summary>
        /// A hunter: shooting from within 25 m, backing away to 8 m when its opponent comes within 5 m, 0.6 to 1.8 s
        /// between shots, aiming 0.3 to 1 s at full draw, each shot within 1.5° of true (up to 0.4 m off at 15 m).
        /// </summary>
        public static ArcherySettings Hunter => new ArcherySettings(25f, 5f, 8f, 0.6f, 1.8f, 0.3f, 1f, 1.5f);

        public float Range { get; }

        /// <summary>Meters from its opponent closer than which it backs away; 0: it never gives ground.</summary>
        public float StandOff { get; }

        /// <summary>Where backing away ends.</summary>
        public float SpacedAt { get; }

        public float MinPause { get; }

        public float MaxPause { get; }

        public float MinAim { get; }

        public float MaxAim { get; }

        public float Spread { get; }
    }

    /// <summary>
    /// A mind that fights back. At ease (<see cref="Mode.Normal"/>) it is another mind (its day: <see cref="Wander"/>),
    /// its weapons away. In a fight (<see cref="Mode.Fight"/>) it draws first, faces its nearest opponent and fights as
    /// what it holds is fought with (<see cref="MindSight.Shoots"/>); its opponent gone, it stands ready. The fight
    /// over, it puts its weapons away and goes back to its day.
    /// <para>
    /// With a blade (<see cref="FighterSettings"/>) it closes in (walking, running from afar) and in reach stands and
    /// strikes with its right hand, pausing between blows, now and then raising its shield for a while instead; crowded
    /// (within <see cref="FighterSettings.StandOff"/>) it steps back to fighting distance, facing its foe, its guard as
    /// it was, never mid-swing.
    /// </para>
    /// <para>
    /// With a bow (<see cref="ArcherySettings"/>) it closes in to its range, and within it stands and shoots: as each
    /// pause runs out it draws, aims a while at full draw and looses, each shot a little off true its own way
    /// (<see cref="MindDecision.Aim"/>); crowded it backs away, facing its foe, shooting as it goes.
    /// </para>
    /// What strikes it decides on its own dice (<see cref="Random"/>), so no two fighters keep the same time. Engine-free.
    /// </summary>
    public sealed class Fighter : IMind
    {
        readonly IMind _atEase;
        readonly FighterSettings _settings;
        readonly ArcherySettings _archery;
        readonly Random _random;

        // Seconds until its next move in reach (a blow, a block, a draw), and seconds its shield stays up (0: it is down);
        // whether it is stepping back to fighting distance; whether it draws a bow it hasn't loosed yet, the seconds it
        // still aims at full draw, and how far off.
        float _pause;
        float _blocking;
        bool _givingGround;
        bool _drawing;
        float _aiming;
        Vector2 _aimOff;

        /// <param name="atEase">The mind it is while not fighting (its day).</param>
        /// <param name="settings">How it fights with a blade, and its paces closing in.</param>
        /// <param name="archery">How it fights with a bow.</param>
        /// <param name="random">Its own dice.</param>
        public Fighter(IMind atEase, FighterSettings settings, ArcherySettings archery, Random random)
        {
            _atEase = atEase ?? throw new ArgumentNullException(nameof(atEase));
            _settings = settings;
            _archery = archery;
            _random = random ?? throw new ArgumentNullException(nameof(random));
            NextPause(_settings.MinPause, _settings.MaxPause);
        }

        public MindDecision Decide(MindSight sight, float deltaTime)
        {
            if (!(deltaTime > 0f) || float.IsInfinity(deltaTime))
                throw new ArgumentOutOfRangeException(nameof(deltaTime), deltaTime, "Must be a positive number.");
            // Weapons not out, no draw is under way (putting them away ends it).
            if (sight.Weapons != Readiness.Drawn)
                _drawing = false;
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

        // Fighting: draw, then face the opponent and fight it as what it holds is fought with; no opponent, stand ready,
        // the shield down (a bow's draw held, for the next).
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
            return sight.Shoots ? Shoot(sight, way, distance, facing, deltaTime) : Melee(sight, way, distance, facing, deltaTime);
        }

        // With a blade: out of reach close in, the shield down; in reach stand and, as each pause runs out, strike or raise
        // the shield for a while.
        MindDecision Melee(MindSight sight, Vector2 way, float distance, float? facing, float deltaTime)
        {
            _drawing = false;
            if (distance > _settings.Reach)
            {
                _blocking = 0f;
                _givingGround = false;
                return CloseIn(way, distance, facing, sight.GuardUp);
            }

            var step = GiveGround(way, distance, _settings.StandOff, _settings.SpacedAt, sight.RightHandBusy);
            if (_blocking > 0f)
            {
                _blocking -= deltaTime;
                return new MindDecision(step, facing, releaseLeft: _blocking <= 0f);
            }

            if (sight.RightHandBusy || sight.GuardUp)
                return new MindDecision(step, facing);
            _pause -= deltaTime;
            if (_pause > 0f)
                return new MindDecision(step, facing);

            NextPause(_settings.MinPause, _settings.MaxPause);
            if (_random.NextDouble() < _settings.BlockChance)
            {
                _blocking = _settings.BlockSeconds;
                return new MindDecision(step, facing, useLeft: true);
            }

            return new MindDecision(step, facing, useRight: true);
        }

        // With a bow: a draw under way is seen through wherever its foe goes; else out of range it closes in, and in range
        // it stands (backing away, crowded) and, as each pause runs out, draws, its aim a little off true its own way.
        MindDecision Shoot(MindSight sight, Vector2 way, float distance, float? facing, float deltaTime)
        {
            _blocking = 0f;
            if (_drawing)
                return Draw(sight, way, distance, facing, deltaTime);
            if (distance > _archery.Range)
            {
                _givingGround = false;
                return CloseIn(way, distance, facing, sight.GuardUp);
            }

            var step = GiveGround(way, distance, _archery.StandOff, _archery.SpacedAt, hold: false);
            if (sight.RightHandBusy)
                return new MindDecision(step, facing);
            _pause -= deltaTime;
            if (_pause > 0f)
                return new MindDecision(step, facing);

            _drawing = true;
            _aiming = Between(_archery.MinAim, _archery.MaxAim);
            _aimOff = Stray(_archery.Spread);
            return new MindDecision(step, facing, useRight: true, aim: _aimOff);
        }

        // Drawing: once at full draw it aims a while, then looses and waits for the next shot; a draw that never came (no
        // arrows, too tired) or was cut short waits likewise.
        MindDecision Draw(MindSight sight, Vector2 way, float distance, float? facing, float deltaTime)
        {
            var step = GiveGround(way, distance, _archery.StandOff, _archery.SpacedAt, hold: false);
            if (!sight.RightHandBusy)
            {
                _drawing = false;
                NextPause(_archery.MinPause, _archery.MaxPause);
                return new MindDecision(step, facing);
            }

            if (sight.DrawPull < 1f)
                return new MindDecision(step, facing, aim: _aimOff);
            _aiming -= deltaTime;
            if (_aiming > 0f)
                return new MindDecision(step, facing, aim: _aimOff);

            _drawing = false;
            NextPause(_archery.MinPause, _archery.MaxPause);
            return new MindDecision(step, facing, releaseRight: true, aim: _aimOff);
        }

        // Out of reach: it closes in, walking near, running from afar, the shield down.
        MindDecision CloseIn(Vector2 way, float distance, float? facing, bool guardUp)
        {
            var speed = distance > _settings.RunBeyond ? _settings.RunSpeed : _settings.WalkSpeed;
            return new MindDecision(way / distance * speed, facing, releaseLeft: guardUp);
        }

        // Crowded (within `standOff`), it steps straight back from its foe at a walk until it is `spacedAt` again, its
        // moves going on as they would standing; never while it `hold`s (a blow plays out where it began). Zero when it
        // holds its ground.
        Vector2 GiveGround(Vector2 way, float distance, float standOff, float spacedAt, bool hold)
        {
            if (!(standOff > 0f) || distance < 1e-4f)
                return Vector2.Zero;
            if (distance < standOff)
                _givingGround = true;
            else if (distance >= spacedAt)
                _givingGround = false;
            return _givingGround && !hold ? -way / distance * _settings.WalkSpeed : Vector2.Zero;
        }

        void NextPause(float min, float max) => _pause = Between(min, max);

        float Between(float min, float max) => min + (float)_random.NextDouble() * (max - min);

        // A way off true within `spread` degrees (x to the right, y up), every way alike: evenly over the disc.
        Vector2 Stray(float spread)
        {
            var off = spread * (float)Math.Sqrt(_random.NextDouble());
            var turn = _random.NextDouble() * 2.0 * Math.PI;
            return new Vector2(off * (float)Math.Cos(turn), off * (float)Math.Sin(turn));
        }
    }
}
