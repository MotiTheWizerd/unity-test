using System;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Game.Combat;

namespace Forge.Play
{
    /// <summary>
    /// An actor's raised guard given a face on its look (<see cref="GuardFace"/>): each frame, while its fighting has a
    /// guard up (<see cref="ActorCombat.Guard"/>: a shield raised in its window), the shield that hand holds shows a face a
    /// blade meets before the body behind it; lowered, the face is away. Bound while the look exists; the rules never know
    /// it is there.
    /// </summary>
    public sealed class GuardView
    {
        readonly ActorCombat _combat;
        readonly CharacterModel _model;
        readonly IRecordStore _records;
        GuardFace _raised;

        /// <param name="records">The world, which a guard's slot's bone is read from.</param>
        public GuardView(ActorCombat combat, CharacterModel model, IRecordStore records)
        {
            _combat = combat ?? throw new ArgumentNullException(nameof(combat));
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _records = records ?? throw new ArgumentNullException(nameof(records));
        }

        /// <summary>Puts the face of the guard up now on the shield raising it, and takes any other down.</summary>
        public void Tick()
        {
            var face = FaceOf(_combat.Guard);
            if (face == _raised)
                return;
            if (_raised != null)
                _raised.Raise(false);
            face?.Raise(true);
            _raised = face;
        }

        // The face of what the guard's hand holds; null with no guard up, or nothing in that hand.
        GuardFace FaceOf(Swing guard)
        {
            if (guard == null || !_records.TryGet(guard.Slot, out var record) || !(record is EquipSlotRecord slot) ||
                !_model.TryGetHeld(slot.Bone, out var look, out _))
                return null;
            return GuardFace.On(look);
        }
    }
}
