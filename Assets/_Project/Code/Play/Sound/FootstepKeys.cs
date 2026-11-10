using System.Collections.Generic;
using Forge.Game.Player;

namespace Forge.Play.Sound
{
    /// <summary>
    /// Which footstep sounds a step of a gait on a surface may play, most particular first, and how loud each is
    /// played for it, so a new folder under Content/Sounds/Footsteps is heard the moment it is built:
    /// <list type="number">
    /// <item>the surface's own at the gait, or a gentler one ("footsteps/wet/run", then "footsteps/wet/walk");</item>
    /// <item>the surface's own at any gait ("footsteps/wet");</item>
    /// <item>the gait's own on any ground, or a gentler one ("footsteps/sprint", then "footsteps/run").</item>
    /// </list>
    /// A sound made for another gait than the step's is played louder or softer by how hard each gait treads
    /// (<see cref="Tread"/>); one for no gait counts as a walk's. Pure, so testable.
    /// </summary>
    public static class FootstepKeys
    {
        public const string Group = "footsteps";

        /// <summary>The keys a step of <paramref name="gait"/> on <paramref name="surface"/> (null: none in particular) may play, each with its gain.</summary>
        public static IEnumerable<(string Key, float Gain)> For(string surface, Gait gait)
        {
            if (gait == Gait.Still)
                yield break;
            var tread = Tread(gait);
            if (!string.IsNullOrEmpty(surface))
            {
                foreach (var each in Gentler(gait))
                    yield return ($"{Group}/{surface}/{Name(each)}", tread / Tread(each));
                yield return ($"{Group}/{surface}", tread / Tread(Gait.Walk));
            }

            foreach (var each in Gentler(gait))
                yield return ($"{Group}/{Name(each)}", tread / Tread(each));
        }

        /// <summary>How hard <paramref name="gait"/> treads, a walk's being 1.</summary>
        public static float Tread(Gait gait)
        {
            switch (gait)
            {
                case Gait.Sneak: return 0.45f;
                case Gait.Run: return 1.3f;
                case Gait.Sprint: return 1.5f;
                default: return 1f;
            }
        }

        // The gait and those gentler than it whose sounds may stand in, in order: a sprint's, a run's, a walk's.
        static IEnumerable<Gait> Gentler(Gait gait)
        {
            yield return gait;
            if (gait == Gait.Sprint)
                yield return Gait.Run;
            if (gait != Gait.Walk)
                yield return Gait.Walk;
        }

        static string Name(Gait gait) => gait.ToString().ToLowerInvariant();
    }
}
