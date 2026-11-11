using Forge.Data.Records;
using Forge.Game.Attributes;

namespace Forge.Game.Combat
{
    /// <summary>
    /// Waking up whole: every resource an actor has (health, stamina, magicka) filled back to its most at once, as a
    /// night's rest would. The player's, while fights are tested: killed, it wakes up where it fell (the game decides
    /// whom, <c>GameSession</c>). Engine-free.
    /// </summary>
    public static class Revival
    {
        /// <summary>Fills every resource of <paramref name="attributes"/> to its most; nothing for an actor without any.</summary>
        public static void WakeUp(ActorAttributes attributes)
        {
            if (attributes == null)
                return;
            foreach (var record in attributes.Set.Records)
            {
                if (record.Kind == AttributeKind.Resource)
                    attributes.Restore(record.Id, attributes.Value(record.Id));
            }
        }
    }
}
