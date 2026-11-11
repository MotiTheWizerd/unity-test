using System.Numerics;

namespace Forge.Game.Actors
{
    /// <summary>Where the actors standing in a game are, for whoever has only their ids (the nearest of its opponents).</summary>
    public interface IStandingActors
    {
        /// <summary>Where <paramref name="actor"/>'s feet are while it stands up in the game; null when it isn't in it or lies fallen.</summary>
        Vector3? FeetOf(ActorId actor);
    }
}
