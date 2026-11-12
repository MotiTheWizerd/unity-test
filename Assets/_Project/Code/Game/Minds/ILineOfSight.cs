using System.Numerics;

namespace Forge.Game.Minds
{
    /// <summary>
    /// Whether one point sees another: nothing solid stands between them (a wall, a rock, the ground; not people, who
    /// are seen past). What a character's eyes go by (<see cref="Gaze"/>); the engine's physics answers in play, a fake
    /// in tests. Positions are the scene's.
    /// </summary>
    public interface ILineOfSight
    {
        bool Clear(Vector3 from, Vector3 to);
    }
}
