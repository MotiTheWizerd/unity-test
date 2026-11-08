using System.Collections.Generic;
using Forge.Data.Records;
using Forge.Game.Actors;
using Forge.Game.Equipment;

namespace Forge.UnityEditor.Content
{
    /// <summary>
    /// The actors the base game ships (<see cref="CoreActors"/>): the player, who starts an archer, the Hunting Bow in
    /// hand (put away) and a hundred arrows equipped, with the Viking Sword in the pack; and the training dummy, which the
    /// Training Dummy object is.
    /// </summary>
    static class CoreActorSet
    {
        public static IEnumerable<ActorRecord> All(string pluginId)
        {
            yield return new ActorRecord(CoreActors.Player, "Player", new[]
            {
                new ActorItem(CoreModels.VikingSword(pluginId)),
                new ActorItem(CoreModels.HuntingBow(pluginId), equip: new[] { CoreEquipSlots.LeftHand }),
                new ActorItem(CoreModels.Arrow(pluginId), 100, new[] { CoreEquipSlots.Ammo }),
            });
            yield return new ActorRecord(CoreActors.TrainingDummy, "Training Dummy");
        }
    }
}
