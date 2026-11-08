using System;
using System.Collections.Generic;
using Forge.Data;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Game.Equipment;
using Forge.World.Assets;
using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// What a character holds and wears, on its look (<see cref="CharacterModel"/>): each thing equipped is built from
    /// the asset the world uses and hung from the bone its slot names (<see cref="EquipSlotRecord.Bone"/>), at the first
    /// slot of its fit (a two-handed sword in the right hand), or hung in its sheath while the model's weapons are put
    /// away and the slot sheathes (<see cref="CharacterModel.InHand"/>); a slot with no bone shows nothing. It is held by
    /// its grip (<see cref="ModelGrip"/>), and held and carried as its <see cref="HeldLook"/> says (a bow: string to the
    /// body, on the back while put away).
    /// </summary>
    public sealed class HeldItems : IDisposable
    {
        readonly CharacterModel _model;
        readonly IRecordStore _records;
        readonly IAssetCatalog _assets;
        readonly Dictionary<EquippedItem, GameObject> _looks = new Dictionary<EquippedItem, GameObject>();

        /// <param name="records">The world, which the equip slots are read from.</param>
        /// <param name="assets">The looks of the world's things, by asset key.</param>
        public HeldItems(CharacterModel model, IRecordStore records, IAssetCatalog assets)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _records = records ?? throw new ArgumentNullException(nameof(records));
            _assets = assets ?? throw new ArgumentNullException(nameof(assets));
        }

        /// <summary>Shows <paramref name="equipped"/> where its slot holds it.</summary>
        public void Hold(EquippedItem equipped)
        {
            if (equipped == null)
                throw new ArgumentNullException(nameof(equipped));
            if (_looks.ContainsKey(equipped) || !TrySlot(equipped.Fit.Slots[0], out var slot) || slot.Bone == null)
                return;

            var look = _assets.Instantiate(equipped.Record.AssetKey, null);
            if (look != null && _model.Attach(look, ModelGrip.Of(look), slot.Bone, slot.Sheathes, look.GetComponent<HeldLook>()))
                _looks.Add(equipped, look);
        }

        /// <summary>Takes <paramref name="equipped"/>'s look away.</summary>
        public void Release(EquippedItem equipped)
        {
            if (equipped == null || !_looks.TryGetValue(equipped, out var look))
                return;
            _looks.Remove(equipped);
            _model.Detach(look);
        }

        public void Dispose()
        {
            foreach (var look in _looks.Values)
                _model.Detach(look);
            _looks.Clear();
        }

        bool TrySlot(RecordId id, out EquipSlotRecord slot)
        {
            slot = _records.TryGet(id, out var record) ? record as EquipSlotRecord : null;
            return slot != null;
        }
    }
}
