using System;
using System.Numerics;
using Forge.Data;
using Forge.Data.Records;
using Forge.Game.Actors;
using Forge.Game.Events;
using Forge.Game.Items;
using Forge.Game.World;

namespace Forge.Game.Combat
{
    /// <summary>
    /// What becomes of a shot once it lands: it breaks, as often as its ammo's <see cref="AmmoPart.BreakChance"/> says, or
    /// it stays as the item it is: where it landed in the world (an arrow in the ground or in the dummy, <see cref="Land"/>),
    /// a placed object playing made (<see cref="WorldChanges.Place"/>), takeable again like any other; or, stuck in
    /// someone (<see cref="Lodge"/>), carried by them, to be taken from them (Skyrim's arrows in a corpse). The stream hears
    /// <see cref="ShotLanded"/>. Where it landed is the caller's to say (the engine flies it). Engine-free.
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
            var placed = Breaks(shot) ? null : _world.Place(shot.Ammo.Id, space, position, rotation);
            _events.Publish(new ShotLanded(shot.Shooter, shot.Ammo.Id, placed?.Id));
            return placed;
        }

        /// <summary>
        /// <paramref name="shot"/> stuck in <paramref name="into"/>, who carries <paramref name="inventory"/>: unless it
        /// broke, it is theirs to carry now. Whether it stays.
        /// </summary>
        public bool Lodge(Shot shot, ActorId into, Inventory inventory)
        {
            if (shot == null)
                throw new ArgumentNullException(nameof(shot));
            if (inventory == null)
                throw new ArgumentNullException(nameof(inventory));
            var stays = !Breaks(shot);
            if (stays)
                inventory.Add(shot.Ammo.Id);
            _events.Publish(new ShotLanded(shot.Shooter, shot.Ammo.Id, null, stays ? into : (ActorId?)null));
            return stays;
        }

        // Whether it breaks as it lands, rolled against its ammo's chance.
        bool Breaks(Shot shot)
        {
            var breakChance = shot.Ammo.Ammo?.BreakChance ?? 0f;
            return breakChance > 0f && _roll() < breakChance;
        }
    }
}
