namespace Forge.Play.Sound
{
    /// <summary>
    /// When a foot comes down, from how fast it moves: a foot that swings moves faster than its body, and one set down
    /// stands still in the world. So a step lands as a swinging foot (past <see cref="Lift"/> times the body's pace)
    /// slows to nearly standing (under <see cref="Plant"/> times it), whatever the clip, the slope or the crouch. One per
    /// foot. Pure, so testable.
    /// </summary>
    public sealed class StepDetector
    {
        /// <summary>A foot moving slower than this share of its body's pace is set down.</summary>
        public const float Plant = 0.35f;

        /// <summary>A foot moving faster than this share of its body's pace has lifted into a swing.</summary>
        public const float Lift = 1.2f;

        bool _swinging;

        /// <summary>
        /// Whether the foot came down this frame, moving at <paramref name="footSpeed"/> while its body goes at
        /// <paramref name="bodySpeed"/> (both meters a second, over the world).
        /// </summary>
        public bool Update(float footSpeed, float bodySpeed)
        {
            if (!(bodySpeed > 0f))
                return false;
            if (_swinging && footSpeed < Plant * bodySpeed)
            {
                _swinging = false;
                return true;
            }

            if (!_swinging && footSpeed > Lift * bodySpeed)
                _swinging = true;
            return false;
        }

        /// <summary>Forgets the foot's swing (it stood still, or left the ground), so the next step counts from its next lift.</summary>
        public void Reset() => _swinging = false;
    }
}
