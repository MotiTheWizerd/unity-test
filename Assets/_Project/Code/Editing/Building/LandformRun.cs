using System;
using System.Collections.Generic;
using System.Numerics;
using Forge.Data.Terrain;
using Forge.Editing.Commands;
using Forge.Editing.Terrain;

namespace Forge.Editing.Building
{
    /// <summary>
    /// What the sculpting commands (sculpt_hill, sculpt_ridge) share: the profile by name, one <see cref="LandformSculpting"/>
    /// over the space's land, its refusals in words for the caller, the ground written with the objects on it as one
    /// undo step, and what came of it.
    /// </summary>
    static class LandformRun
    {
        public const float DefaultRoughness = 0.3f;

        /// <summary>The profiles by the names the commands take.</summary>
        public static readonly IReadOnlyDictionary<string, LandformProfile> Profiles = new Dictionary<string, LandformProfile>(StringComparer.OrdinalIgnoreCase)
        {
            { "round", LandformProfile.Round },
            { "peak", LandformProfile.Peak },
            { "plateau", LandformProfile.Plateau },
        };

        /// <param name="profile">The profile's name, or null for round.</param>
        public static SculptResult Sculpt(BuildContext context, IReadOnlyList<Vector2> spine, float width, float height, string profile,
            float? roughness, Func<LandformSculpting, string> describe)
        {
            if (height == 0f)
                throw new CommandException("A height of 0 changes nothing: give how high to raise the ground, or a negative height for how deep to dig.");

            var shape = ProfileNamed(profile);
            var landform = Refusal.Of(() => new LandformSculpting(context.Session.World.Records, context.Space, spine, width, height, shape, roughness ?? DefaultRoughness));
            if (landform.Highest == null)
                throw new CommandException("There is no land under that landform; inspect_area shows where the ground is.");

            TerrainEdits.Commit(context.Session, landform, context.FollowTerrain(), out var moved);
            return new SculptResult(describe(landform) + Outcome(landform, moved), landform, moved);
        }

        /// <summary>The profile in words, e.g. "peaked".</summary>
        public static string Words(LandformProfile profile) =>
            profile == LandformProfile.Peak ? "peaked" : profile == LandformProfile.Plateau ? "flat-topped" : "round";

        /// <summary>"Raised" or "Dug", and "high" or "deep", by the height's sign.</summary>
        public static (string Verb, string Measure) Direction(float height) => height > 0f ? ("Raised", "high") : ("Dug", "deep");

        static LandformProfile ProfileNamed(string name)
        {
            if (name == null)
                return LandformProfile.Round;
            if (Profiles.TryGetValue(name.Trim(), out var profile))
                return profile;
            throw new CommandException($"There is no profile '{name}'. The profiles are: {string.Join(", ", Profiles.Keys)}.");
        }

        // How the ground came out: its new top (or bottom), the limit if it was reached, and the objects that moved.
        static string Outcome(LandformSculpting landform, int moved)
        {
            var outcome = landform.Height > 0f
                ? FormattableString.Invariant($": the ground now reaches {landform.Highest:0.#} m at its highest")
                : FormattableString.Invariant($": the ground now goes down to {landform.Lowest:0.#} m at its lowest");
            if (landform.ReachedTheLimit)
                outcome += FormattableString.Invariant($"; some of it stopped at the terrain's limit (0 to {WorldGrid.MaxTerrainHeight:0} m)");
            outcome += ".";
            if (moved > 0)
                outcome += $" {moved} object(s) moved with the ground.";
            return outcome;
        }
    }

    /// <summary>What a sculpting command made of the ground.</summary>
    public sealed class SculptResult : ICommandResult
    {
        internal SculptResult(string summary, LandformSculpting landform, int movedObjects)
        {
            Summary = summary;
            Highest = Rounding.Coarse(landform.Highest ?? 0f);
            Lowest = Rounding.Coarse(landform.Lowest ?? 0f);
            MostRaised = Rounding.Coarse(landform.MostRaised);
            MostLowered = Rounding.Coarse(landform.MostLowered);
            MovedObjects = movedObjects;
        }

        public string Summary { get; }

        /// <summary>The highest ground under the landform now, in meters: where a snow line could start.</summary>
        public float Highest { get; }

        /// <summary>The lowest ground under the landform now, in meters.</summary>
        public float Lowest { get; }

        /// <summary>The most any ground was raised, in meters.</summary>
        public float MostRaised { get; }

        /// <summary>The most any ground was lowered, in meters.</summary>
        public float MostLowered { get; }

        /// <summary>How many objects moved with the ground.</summary>
        public int MovedObjects { get; }
    }
}
