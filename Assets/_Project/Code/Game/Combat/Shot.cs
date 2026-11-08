using System;
using Forge.Data.Records;
using Forge.Game.Actors;

namespace Forge.Game.Combat
{
    /// <summary>
    /// What a weapon that shoots let go (an arrow from a bow): who shot it, with what, what it is (the ammo used up), and
    /// how hard (<see cref="Power"/>, from how far the bow was drawn). Where it flies is the look's (it leaves the
    /// string, and flies through the engine's world); whom it reaches it strikes once (<see cref="ActorCombat.Strike(Shot, ActorId)"/>).
    /// </summary>
    public sealed class Shot
    {
        /// <summary>
        /// Meters a second it leaves a fully drawn bow at, a real longbow's arrow. One for every bow for now; a bow's own
        /// is data to come.
        /// </summary>
        public const float FullDrawSpeed = 60f;

        internal Shot(ActorId shooter, Swing swing, BaseObjectRecord ammo, float power)
        {
            Shooter = shooter;
            Swing = swing ?? throw new ArgumentNullException(nameof(swing));
            Ammo = ammo ?? throw new ArgumentNullException(nameof(ammo));
            Power = power;
        }

        public ActorId Shooter { get; }

        /// <summary>The draw it was loosed from, and so the weapon and the hand.</summary>
        public Swing Swing { get; }

        /// <summary>The weapon that shot it (the bow).</summary>
        public BaseObjectRecord Weapon => Swing.Weapon.Record;

        /// <summary>What flies: the ammo it used up (an arrow), whose look it has.</summary>
        public BaseObjectRecord Ammo { get; }

        /// <summary>How hard it flies and strikes, from <see cref="ActorCombat.WeakestShot"/> (loosed as soon as it was nocked) to 1 (full draw).</summary>
        public float Power { get; }

        /// <summary>Meters a second it leaves the string at: a weak shot drops short.</summary>
        public float Speed => FullDrawSpeed * Power;

        /// <summary>Whether it has struck someone; a shot strikes once.</summary>
        public bool Landed { get; internal set; }
    }
}
