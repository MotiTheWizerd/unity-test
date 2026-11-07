using System;
using Forge.Data.Plugins;
using Forge.Game.Equipment;
using Forge.World.Assets;

namespace Forge.Play
{
    /// <summary>
    /// An actor's equipment shown on its look as it changes: what it equips appears where its slot holds it, what comes
    /// off goes (<see cref="HeldItems"/>). Bound while the look exists (the player while spawned); the rules never know it
    /// is there.
    /// </summary>
    public sealed class EquipmentView : IDisposable
    {
        readonly ActorEquipment _equipment;
        readonly HeldItems _held;

        public EquipmentView(ActorEquipment equipment, CharacterModel model, IRecordStore records, IAssetCatalog assets)
        {
            _equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
            _held = new HeldItems(model, records, assets);
            _equipment.Equipped += _held.Hold;
            _equipment.Unequipped += OnUnequipped;
            foreach (var equipped in _equipment.All)
                _held.Hold(equipped);
        }

        public void Dispose()
        {
            _equipment.Equipped -= _held.Hold;
            _equipment.Unequipped -= OnUnequipped;
            _held.Dispose();
        }

        void OnUnequipped(EquippedItem equipped, UnequipReason reason) => _held.Release(equipped);
    }
}
