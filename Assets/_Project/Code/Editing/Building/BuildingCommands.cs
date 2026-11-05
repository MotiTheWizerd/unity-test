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
            registry.Add(new FlattenAreaCommand(context));
            registry.Add(new ExtendLandCommand(context));
            registry.Add(new SetSeaLevelCommand(context));
            registry.Add(new SetClimateCommand(context));
            registry.Add(new AddLakeCommand(context));
            registry.Add(new SetLakeLevelCommand(context));
            registry.Add(new RemoveLakeCommand(context));
            registry.Add(new PlaceObjectsCommand(context));
            registry.Add(new PlaceLineCommand(context));
            registry.Add(new PlaceOutlineCommand(context));
            registry.Add(new AdjustObjectsCommand(context));
            registry.Add(new DeleteObjectsCommand(context));
        }
    }
}
