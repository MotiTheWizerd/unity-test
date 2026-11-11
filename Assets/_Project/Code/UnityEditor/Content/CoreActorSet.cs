using System.Collections.Generic;
using Forge.Data;
using Forge.Data.Records;
using Forge.Game.Actors;
using Forge.Game.Equipment;

namespace Forge.UnityEditor.Content
{
    /// <summary>
    /// The actors the base game ships (<see cref="CoreActors"/>): the player, who starts a sword-and-board Viking, the
    /// Viking Sword in the right hand and the Painted Round Shield in the left (put away), with the Hunting Bow in the
    /// pack and a hundred arrows equipped for it; the training dummy, which the Training Dummy object is; and the
    /// characters that stand in the world (<see cref="CoreModels.Npcs"/>), each a sword-and-board fighter like the
    /// player, sword and shield put away until a fight.
    /// </summary>
    static class CoreActorSet
    {
        public static IEnumerable<ActorRecord> All(string pluginId)
        {
            yield return new ActorRecord(CoreActors.Player, "Player", new[]
            {
                new ActorItem(CoreModels.VikingSword(pluginId), equip: new[] { CoreEquipSlots.RightHand }),
                new ActorItem(CoreModels.PaintedRoundShield(pluginId), equip: new[] { CoreEquipSlots.LeftHand }),
                new ActorItem(CoreModels.HuntingBow(pluginId)),
                new ActorItem(CoreModels.Arrow(pluginId), 100, new[] { CoreEquipSlots.Ammo }),
            });
            yield return new ActorRecord(CoreActors.TrainingDummy, "Training Dummy");
            foreach (var npc in CoreModels.Npcs)
                yield return new ActorRecord(Npc(pluginId, npc.Id), npc.Name, new[]
                {
                    new ActorItem(CoreModels.VikingSword(pluginId), equip: new[] { CoreEquipSlots.RightHand }),
                    new ActorItem(CoreModels.PaintedRoundShield(pluginId), equip: new[] { CoreEquipSlots.LeftHand }),
                });
        }

        /// <summary>The actor the character <paramref name="id"/> (<see cref="CoreModels.Npcs"/>) is while the world plays: its record's id.</summary>
        public static RecordId Npc(string pluginId, string id) => ActorRecord.IdFor(pluginId, id);
    }
}
