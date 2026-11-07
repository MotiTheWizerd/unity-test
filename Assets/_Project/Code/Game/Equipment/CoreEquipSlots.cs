using Forge.Data;
using Forge.Data.Records;

namespace Forge.Game.Equipment
{
    /// <summary>
    /// The equip slots of the base game (forge.core), as its content names them: a hand each, so an actor holds one
    /// one-handed thing in each, or one two-handed thing in both. Records, so a mod retunes or adds slots; the rules read
    /// none of them by name, only content does.
    /// </summary>
    public static class CoreEquipSlots
    {
        /// <summary>The base game's plugin, which defines them.</summary>
        public const string Namespace = "forge.core";

        public static readonly RecordId RightHand = EquipSlotRecord.IdFor(Namespace, "right_hand");

        public static readonly RecordId LeftHand = EquipSlotRecord.IdFor(Namespace, "left_hand");

        /// <summary>A one-handed thing: either hand, the right first.</summary>
        public static EquipFit[] OneHanded() => new[] { EquipFit.Of(RightHand), EquipFit.Of(LeftHand) };

        /// <summary>A thing for the off hand only, as a shield: the left.</summary>
        public static EquipFit[] OffHand() => new[] { EquipFit.Of(LeftHand) };

        /// <summary>A two-handed thing: both hands at once.</summary>
        public static EquipFit[] TwoHanded() => new[] { EquipFit.Of(RightHand, LeftHand) };
    }
}
