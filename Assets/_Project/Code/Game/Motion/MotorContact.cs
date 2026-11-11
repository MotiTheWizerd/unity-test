namespace Forge.Game.Motion
{
    /// <summary>What the body touched during its last move, which the next step of <see cref="Player.PlayerLocomotion"/> reacts to.</summary>
    public readonly struct MotorContact
    {
        public MotorContact(bool grounded, bool hitCeiling)
        {
            Grounded = grounded;
            HitCeiling = hitCeiling;
        }

        /// <summary>Standing on something it can stand on (ground, a floor, a rock), not sliding off a slope too steep.</summary>
        public bool Grounded { get; }

        /// <summary>Its head hit something on the way up.</summary>
        public bool HitCeiling { get; }
    }
}
