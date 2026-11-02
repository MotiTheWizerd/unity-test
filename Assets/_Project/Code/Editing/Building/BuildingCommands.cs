using System;
using Forge.Editing.Commands;

namespace Forge.Editing.Building
{
    /// <summary>The world-building commands, registered together.</summary>
    public static class BuildingCommands
    {
        public static void AddTo(CommandRegistry registry, BuildContext context)
        {
            if (registry == null)
                throw new ArgumentNullException(nameof(registry));

            registry.Add(new ListObjectsCommand(context));
            registry.Add(new InspectAreaCommand(context));
            registry.Add(new PlaceObjectsCommand(context));
            registry.Add(new PlaceLineCommand(context));
            registry.Add(new DeleteObjectsCommand(context));
        }
    }
}
