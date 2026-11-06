using System.Collections.Generic;
using System.Linq;
using Forge.Data.Records;
using Forge.Editing.Placement;

namespace Forge.Editing.Building
{
    /// <summary>A placed object as the commands report it: what it is, where it stands, which way it faces, how big.</summary>
    public sealed class PlacedObjectInfo
    {
        public PlacedObjectInfo(PlacedObjectRecord placed, string name)
        {
            Id = placed.Id.ToString();
            Object = placed.BaseId.ToString();
            Name = name;
            X = Rounding.Meters(placed.Position.X);
            Y = Rounding.Meters(placed.Position.Y);
            Z = Rounding.Meters(placed.Position.Z);
            Heading = Rounding.Degrees(PlacementMath.HeadingOf(placed.Rotation));
            Scale = Rounding.Meters(placed.Scale);
            if (placed.Materials.Count > 0)
                Materials = placed.Materials.ToDictionary(m => m.Slot, m => m.Material.ToString());
        }

        public string Id { get; }

        /// <summary>Id of its base object.</summary>
        public string Object { get; }

        public string Name { get; }

        public float X { get; }

        public float Y { get; }

        public float Z { get; }

        /// <summary>Compass direction its front faces, in degrees.</summary>
        public float Heading { get; }

        public float Scale { get; }

        /// <summary>The slots it wears another material in than its kind does (slot → material id); null when none.</summary>
        public IReadOnlyDictionary<string, string> Materials { get; }
    }
}
