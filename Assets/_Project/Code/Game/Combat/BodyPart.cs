namespace Forge.Game.Combat
{
    /// <summary>
    /// Where on a body a blow or a shot met it, either side alike: what its look's hitbox there is
    /// (<c>Forge.Play.Hitbox</c>), told with the hit (<see cref="HitLanded.Part"/>) for whatever comes to care (a blow to
    /// the head, the armor on an arm). <see cref="None"/>: not known (a body met as a whole, a training dummy).
    /// </summary>
    public enum BodyPart
    {
        None,
        Head,
        Neck,
        Chest,
        Belly,
        Pelvis,
        UpperArm,
        Forearm,
        Hand,
        Thigh,
        Shin,
        Foot,
    }
}
