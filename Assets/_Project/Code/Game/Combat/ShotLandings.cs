using System;
using System.Numerics;
using Forge.Data;
using Forge.Data.Records;
using Forge.Game.Events;
using Forge.Game.World;

namespace Forge.Game.Combat
{
    /// <summary>
    /// What becomes of a shot once it lands (an arrow in the ground or in the dummy): it breaks, as often as its ammo's
    /// <see cref="AmmoPart.BreakChance"/> says, or it stays where it landed as the item it is, a placed object playing made
    /// (<see cref="WorldChanges.Place"/>), takeable again like any other; the stream hears <see cref="ShotLanded"/>.
    /// Where it landed is the caller's to say (the engine flies it). Engine-free.
    /// </summary>
    public sealed class ShotLandings
    {
        readonly WorldChanges _world;
        readonly GameEvents _events;
        readonly Func<float> _roll;

        /// <param name="roll">A random number from 0 up to 1, rolled against the break chance; null for the system's random.</param>
        public ShotLandings(WorldChanges world, GameEvents events, Func<float> roll = null)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _events = events ?? throw new ArgumentNullException(nameof(events));
            if (roll == null)
            {
                var random = new Random();
                roll = () => (float)random.NextDouble();
            }

            _roll = roll;
        }

        /// <summary>
        /// <paramref name="shot"/> landed in <paramref name="space"/> at <paramref name="position"/>, turned
        /// <paramref name="rotation"/> (its look's pose there): the placed object it now is, or null when it broke.
        /// </summary>
        public PlacedObjectRecord Land(Shot shot, RecordId space, Vector3 position, Quaternion rotation)
        {
            if (shot == null)
                throw new ArgumentNullException(nameof(shot));
            var breakChance = shot.Ammo.Ammo?.BreakChance ?? 0f;
            var broke = breakChance > 0f && _roll() < breakChance;
            var placed = broke ? null : _world.Place(shot.Ammo.Id, space, position, rotation);
            _events.Publish(new ShotLanded(shot.Shooter, shot.Ammo.Id, placed?.Id));
            return placed;
        }
    }
}
