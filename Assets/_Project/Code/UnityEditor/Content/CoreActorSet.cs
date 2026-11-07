using System.Collections.Generic;
using Forge.Data.Records;
using Forge.Game.Actors;
using Forge.Game.Equipment;

namespace Forge.UnityEditor.Content
{
    /// <summary>
    /// The actors the base game ships (<see cref="CoreActors"/>): the player, who starts with the Viking Sword in the right
    /// hand, sheathed; and the training dummy, which the Training Dummy object is.
    /// </summary>
    static class CoreActorSet
    {
        public static IEnumerable<ActorRecord> All(string pluginId)
        {
            yield return new ActorRecord(CoreActors.Player, "Player", new[]
            {
                new ActorItem(CoreModels.VikingSword(pluginId), equip: new[] { CoreEquipSlots.RightHand }),
            });
            yield return new ActorRecord(CoreActors.TrainingDummy, "Training Dummy");
        }
    }
}
