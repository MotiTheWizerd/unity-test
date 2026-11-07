using System;
using Forge.Data.Records;
using Forge.EditorApp.Tools;
using Forge.Editing.Placement;
using Forge.Game.Items;
using Forge.World;
using UnityEngine;

namespace Forge.EditorApp
{
    /// <summary>
    /// Where an item dropped while playing from here comes to rest: on the first thing straight below (the land, a table),
    /// seated by the editor's own rule (<see cref="ObjectSeating"/>), so a dropped item lies exactly as a placed one would.
    /// </summary>
    public sealed class SeatedItemPlacement : IItemPlacement
    {
        readonly ScenePicker _picker;
        readonly ObjectSeating _seating;

        public SeatedItemPlacement(ScenePicker picker, ObjectSeating seating)
        {
            _picker = picker ?? throw new ArgumentNullException(nameof(picker));
            _seating = seating ?? throw new ArgumentNullException(nameof(seating));
        }

        public bool TryRest(BaseObjectRecord item, System.Numerics.Vector3 above, float heading,
            out System.Numerics.Vector3 position, out System.Numerics.Quaternion rotation)
        {
            position = default;
            rotation = System.Numerics.Quaternion.Identity;
            if (!_picker.TryPickWorld(new Ray(above.ToUnity(), Vector3.down), out var hit))
                return false;

            var pose = _seating.Seat(item, hit.Point.ToNumerics(), ScenePicker.IsGround(hit.Collider), heading, 1f);
            position = pose.Position;
            rotation = pose.Rotation;
            return true;
        }
    }
}
