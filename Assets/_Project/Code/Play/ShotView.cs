using System;
using Forge.Game.Combat;
using Forge.Game.Equipment;
using Forge.World.Assets;
using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// A bow's draw on its archer's look: from the moment the drawing hand reaches the quiver
    /// (<see cref="CharacterModel.TryArrowTaken"/>) an arrow of the ammo equipped is in it, tail in the fist and point
    /// down as it comes out; by the time it is on the string (the draw's window opens) it lies along the draw, from the
    /// drawing hand to the bow hand, until it is loosed. While drawing, the upper body bends to the aim's height
    /// (<see cref="CharacterModel.BendUpperBody"/>), easing in and out, so the arrow points where the archer looks. Works
    /// on each frame's pose (<see cref="CharacterModel.Posed"/>). Bound while the look exists.
    /// </summary>
    public sealed class ShotView : IDisposable
    {
        // Seconds the bend takes to come in as a draw starts, and to go once it is over.
        const float AimFade = 0.2f;

        readonly ActorCombat _combat;
        readonly ActorEquipment _equipment;
        readonly CharacterModel _model;
        readonly IAssetCatalog _assets;
        readonly Func<float> _aimPitch;
        GameObject _arrow;
        string _arrowKey;
        float _tail;
        float _aim;

        /// <param name="aimPitch">How high the archer looks, degrees above level (below: less than zero).</param>
        public ShotView(ActorCombat combat, ActorEquipment equipment, CharacterModel model, IAssetCatalog assets, Func<float> aimPitch)
        {
            _combat = combat ?? throw new ArgumentNullException(nameof(combat));
            _equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _assets = assets ?? throw new ArgumentNullException(nameof(assets));
            _aimPitch = aimPitch ?? throw new ArgumentNullException(nameof(aimPitch));
            _model.Posed += OnPosed;
        }

        /// <summary>
        /// The line an arrow on <paramref name="model"/>'s string lies along: from the drawing (right) hand's grip
        /// <paramref name="direction"/>wards to the bow (left) hand's; false without both hands.
        /// </summary>
        public static bool TryDrawLine(CharacterModel model, out Vector3 from, out Vector3 direction)
        {
            from = direction = default;
            if (model == null || !model.TryGrip(true, out var drawing) || !model.TryGrip(false, out var bow))
                return false;
            from = drawing.position;
            var way = bow.position - from;
            direction = way.sqrMagnitude > 1e-6f ? way.normalized : model.Root.transform.forward;
            return true;
        }

        public void Dispose()
        {
            _model.Posed -= OnPosed;
            if (_arrow != null)
                UnityEngine.Object.Destroy(_arrow);
        }

        void OnPosed()
        {
            var drawing = Drawing(out var any);
            _aim = Mathf.MoveTowards(_aim, any ? 1f : 0f, Time.deltaTime / AimFade);
            if (_aim > 0f)
                _model.BendUpperBody(_aimPitch() * Mathf.SmoothStep(0f, 1f, _aim));

            var taken = drawing != null && _model.TryArrowTaken(drawing.Move.Animation, out var moment) ? moment : 0f;
            if (drawing == null || drawing.Progress < taken || !TryArrow(drawing))
            {
                if (_arrow != null)
                    _arrow.SetActive(false);
                return;
            }

            Lay(drawing, taken);
        }

        // The draw of a weapon that shoots not yet loosed, if any; and whether any such swing plays at all (loosed too).
        Swing Drawing(out bool any)
        {
            any = false;
            foreach (var swing in _combat.Swings)
            {
                if (!swing.Shoots)
                    continue;
                any = true;
                if (!swing.Loosed)
                    return swing;
            }

            return null;
        }

        // The look of what the draw shoots (an arrow's own, not the quiver it is worn as), made when it changes.
        bool TryArrow(Swing drawing)
        {
            var slot = drawing.Weapon.Record.Weapon.Ammo;
            var ammo = slot.HasValue ? _equipment.In(slot.Value)?.Record : null;
            if (ammo == null)
                return false;
            if (_arrow == null || _arrowKey != ammo.AssetKey)
            {
                if (_arrow != null)
                    UnityEngine.Object.Destroy(_arrow);
                _arrowKey = ammo.AssetKey;
                _arrow = _assets.Instantiate(ammo.AssetKey, null);
                if (_arrow == null)
                    return false;
                _model.Adopt(_arrow);
                _tail = ArrowLook.Tail(_arrow);
            }

            _arrow.SetActive(true);
            return true;
        }

        // Tail in the drawing hand: point down out of the fist as it leaves the quiver, turning to lie along the draw by
        // the time it is on the string.
        void Lay(Swing drawing, float taken)
        {
            if (!_model.TryGrip(true, out var hand) || !TryDrawLine(_model, out var from, out var along))
                return;
            var span = drawing.Timing.HitFrom - taken;
            var onString = span > 1e-4f ? Mathf.SmoothStep(0f, 1f, (drawing.Progress - taken) / span) : 1f;
            var outOfQuiver = hand.rotation * Quaternion.Euler(180f, 0f, 0f);
            var onBow = Quaternion.FromToRotation(Vector3.up, along);
            var rotation = Quaternion.Slerp(outOfQuiver, onBow, onString);
            var arrow = _arrow.transform;
            arrow.rotation = rotation;
            arrow.position = from + rotation * Vector3.up * (_tail * arrow.lossyScale.y);
        }
    }
}
