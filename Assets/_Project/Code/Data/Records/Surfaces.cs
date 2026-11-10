using System.Linq;

namespace Forge.Data.Records
{
    /// <summary>
    /// What a thing is to stand on or strike ("grass", "stone", "wood", "metal", "wet"): a lowercase word that sounds
    /// (and later marks) are found by, so a mod's ground or object names one and is heard as such; an unknown one falls
    /// back to the plain sounds. A word rather than a list, so a mod can add its own (ice, bone) without touching the base.
    /// </summary>
    public static class Surfaces
    {
        /// <summary>Whether <paramref name="surface"/> is one: a lowercase word of letters and digits.</summary>
        public static bool IsValid(string surface) =>
            !string.IsNullOrEmpty(surface) && surface.All(c => (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'));
    }
}
