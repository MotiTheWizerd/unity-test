using Forge.Data;
using Forge.Data.Records;

namespace Forge.Game.Actors
{
    /// <summary>
    /// The actors of the base game (forge.core) the game reads by name: the player's record, whose items a game begins
    /// with. A record, so a mod changes the starting kit by overriding it.
    /// </summary>
    public static class CoreActors
    {
        /// <summary>The base game's plugin, which defines them.</summary>
        public const string Namespace = "forge.core";

        public static readonly RecordId Player = ActorRecord.IdFor(Namespace, "player");

        /// <summary>A training dummy: something to strike, which strikes nothing back.</summary>
        public static readonly RecordId TrainingDummy = ActorRecord.IdFor(Namespace, "training_dummy");
    }
}
