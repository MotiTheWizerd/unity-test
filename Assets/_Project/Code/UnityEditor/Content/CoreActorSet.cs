using System.Collections.Generic;
using Forge.Data;
using Forge.Data.Records;
using Forge.Game.Actors;
using Forge.Game.Equipment;

namespace Forge.UnityEditor.Content
{
    /// <summary>
    /// The actors the base game ships (<see cref="CoreActors"/>): the player and the characters that stand in the world
    /// (<see cref="CoreModels.Npcs"/>), each an archer while the bow fights are worked on (<see cref="Archer"/>); and the
    /// training dummy, which the Training Dummy object is.
    /// </summary>
    static class CoreActorSet
    {
        public static IEnumerable<ActorRecord> All(string pluginId)
        {
            yield return new ActorRecord(CoreActors.Player, "Player", Archer(pluginId));
            yield return new ActorRecord(CoreActors.TrainingDummy, "Training Dummy");
            foreach (var npc in CoreModels.Npcs)
                yield return new ActorRecord(Npc(pluginId, npc.Id), npc.Name, Archer(pluginId));
        }

        // An archer's kit: the Hunting Bow in the left hand (put away) and a hundred arrows equipped for it, with the
        // Viking Sword and the Painted Round Shield in the pack, to be equipped for a fight up close.
        static ActorItem[] Archer(string pluginId) => new[]
        {
            new ActorItem(CoreModels.HuntingBow(pluginId), equip: new[] { CoreEquipSlots.LeftHand }),
            new ActorItem(CoreModels.Arrow(pluginId), 100, new[] { CoreEquipSlots.Ammo }),
            new ActorItem(CoreModels.VikingSword(pluginId)),
            new ActorItem(CoreModels.PaintedRoundShield(pluginId)),
        };

        /// <summary>The actor the character <paramref name="id"/> (<see cref="CoreModels.Npcs"/>) is while the world plays: its record's id.</summary>
        public static RecordId Npc(string pluginId, string id) => ActorRecord.IdFor(pluginId, id);
    }
}
