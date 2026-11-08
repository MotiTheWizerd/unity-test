using System;
using Forge.Game.Cameras;
using Forge.Game.Combat;
using Forge.Game.Equipment;
using Forge.World.Assets;
using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// A bow's draw on its archer's look: from the moment the drawing hand reaches the quiver
    /// (<see cref="CharacterModel.TryArrowTaken"/>) an arrow of the ammo equipped is in it, tail in the fist and point
    /// down as it comes out; by the time it is on the string (the draw's window opens) it lies along the draw
    /// (<see cref="ArcheryAim.TryDrawLine"/>), until it is loosed. As the arrow comes to the string the upper body turns
    /// (<see cref="CharacterModel.TurnUpperBody"/>) so that the draw points at what the archer aims at
    /// (<see cref="ArcheryAim.Target"/>), high or low, and wherever the clip's own stance would point it, by at most
    /// <see cref="MostTurn"/> (the body itself turns to the aim meanwhile, <see cref="Game.Player.PlayerCharacter.Aiming"/>);
    /// it eases out once the shot is gone. Works on each frame's pose (<see cref="CharacterModel.Posed"/>). Bound while the look exists.
    /// </summary>
    public sealed class ShotView : IDisposable
    {
        // Seconds the aim takes to let go of the upper body once the draw is over.
        const float AimFade = 0.2f;

        // Degrees the upper body turns at most to bring the draw onto the aim: the clip's own stance and the aim's
        // height; the rest is the body's to turn (PlayerCharacter.Aiming), so it never twists round to aim behind.
        const float MostTurn = 75f;

        readonly ActorCombat _combat;
        readonly ActorEquipment _equipment;
        readonly CharacterModel _model;
        readonly IAssetCatalog _assets;
        readonly Func<ViewPose> _view;
        GameObject _arrow;
        string _arrowKey;
        float _aim;
        float _onString;

        /// <param name="view">Where the archer looks from and which way (the camera).</param>
        public ShotView(ActorCombat combat, ActorEquipment equipment, CharacterModel model, IAssetCatalog assets, Func<ViewPose> view)
        {
            _combat = combat ?? throw new ArgumentNullException(nameof(combat));
            _equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _assets = assets ?? throw new ArgumentNullException(nameof(assets));
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _model.Posed += OnPosed;
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
            var taken = drawing != null && _model.TryArrowTaken(drawing.Move.Animation, out var moment) ? moment : 0f;
            if (drawing != null)
                _onString = OnString(drawing, taken);
            _aim = any ? 1f : Mathf.MoveTowards(_aim, 0f, Time.deltaTime / AimFade);
            Aim(Mathf.SmoothStep(0f, 1f, _aim) * _onString);

            if (drawing == null || drawing.Progress < taken || !TryArrow(drawing))
            {
                if (_arrow != null)
                    _arrow.SetActive(false);
                return;
            }

            Lay();
        }

        // How far the arrow has come from the quiver to the string, 0 to 1, eased.
        static float OnString(Swing drawing, float taken)
        {
            var span = drawing.Timing.HitFrom - taken;
            return span > 1e-4f ? Mathf.SmoothStep(0f, 1f, (drawing.Progress - taken) / span) : 1f;
        }

        // Turns the upper body by `weight` of the turn that brings the draw onto the aim.
        void Aim(float weight)
        {
            if (!(weight > 0f) || !ArcheryAim.TryDrawLine(_model, out var from, out var along))
                return;
            var aimed = ArcheryAim.Target(_view(), from) - from;
            if (aimed.sqrMagnitude < 1e-6f)
                return;
            var turn = Quaternion.RotateTowards(Quaternion.identity, Quaternion.FromToRotation(along, aimed.normalized), MostTurn);
            _model.TurnUpperBody(Quaternion.Slerp(Quaternion.identity, turn, weight));
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
            }

            _arrow.SetActive(true);
            return true;
        }

        // Tail in the drawing hand: point down out of the fist as it leaves the quiver, turning to lie along the draw by
        // the time it is on the string.
        void Lay()
        {
            if (!_model.TryGrip(true, out var hand) || !ArcheryAim.TryDrawLine(_model, out var from, out var along))
                return;
            var outOfQuiver = hand.rotation * Quaternion.Euler(180f, 0f, 0f);
            var onBow = Quaternion.FromToRotation(Vector3.up, along);
            ArrowLook.LayByTail(_arrow, from, Quaternion.Slerp(outOfQuiver, onBow, _onString));
        }
    }
}
