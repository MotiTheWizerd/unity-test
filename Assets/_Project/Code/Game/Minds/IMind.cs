namespace Forge.Game.Minds
{
    /// <summary>
    /// A character's mind: each frame it sees its situation (<see cref="MindSight"/>) and says what its body does next
    /// (<see cref="MindDecision"/>): where to walk, which way to face, what the hands do. Engine-free, so a mind is
    /// tested without a body; whoever carries the decision out (the world's characters) never knows which mind decided.
    /// </summary>
    public interface IMind
    {
        /// <summary>What its character does over the next <paramref name="deltaTime"/> seconds, from what it sees now.</summary>
        MindDecision Decide(MindSight sight, float deltaTime);
    }
}
