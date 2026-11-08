using System;

namespace Forge.Game.Player
{
    /// <summary>What the use key did this frame: tapped (use what is in sight), held past a moment (grab it), let go after a hold.</summary>
    public enum UseKeyEvent
    {
        None,

        /// <summary>Pressed and let go within the hold time: use (E: take).</summary>
        Tap,

        /// <summary>Held down the hold time: grab what is in sight, Skyrim's way.</summary>
        HoldStarted,

        /// <summary>Let go after a hold: drop what was grabbed.</summary>
        HoldEnded,
    }

    /// <summary>
    /// One key that does two things by how long it is held, as Skyrim's use key does: a tap uses, a hold grabs. A tap is
    /// told when the key comes up (it can't be told from a hold before), a hold the moment it has lasted
    /// <see cref="HoldSeconds"/>. Fed whether the key is down each frame. Engine-free.
    /// </summary>
    public sealed class UseKey
    {
        float _held = -1f;
        bool _holding;

        /// <param name="holdSeconds">How long the key must be held to be a hold, seconds; more than zero.</param>
        public UseKey(float holdSeconds)
        {
            if (!(holdSeconds > 0f) || float.IsInfinity(holdSeconds))
                throw new ArgumentOutOfRangeException(nameof(holdSeconds), holdSeconds, "A hold lasts a finite number of seconds above zero.");
            HoldSeconds = holdSeconds;
        }

        public float HoldSeconds { get; }

        /// <summary>The key is <paramref name="down"/> after <paramref name="deltaTime"/> seconds more: what that makes it do.</summary>
        public UseKeyEvent Update(bool down, float deltaTime)
        {
            if (down)
            {
                // Just pressed: from now on it is counted; it was a hold once it has lasted long enough.
                _held = _held < 0f ? 0f : _held + Math.Max(0f, deltaTime);
                if (_holding || _held < HoldSeconds)
                    return UseKeyEvent.None;
                _holding = true;
                return UseKeyEvent.HoldStarted;
            }

            if (_held < 0f)
                return UseKeyEvent.None;
            var wasHolding = _holding;
            _held = -1f;
            _holding = false;
            return wasHolding ? UseKeyEvent.HoldEnded : UseKeyEvent.Tap;
        }

        /// <summary>Forgets a press under way (the controls went off).</summary>
        public void Reset()
        {
            _held = -1f;
            _holding = false;
        }
    }
}
