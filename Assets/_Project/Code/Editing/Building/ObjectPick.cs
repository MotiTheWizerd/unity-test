using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Forge.Data;
using Forge.Data.Records;
using Forge.Editing.Commands;

namespace Forge.Editing.Building
{
    /// <summary>
    /// The placed objects a command acts on (delete_objects, move_objects): by id, or everything standing inside an
    /// area (a rectangle or corners, as <see cref="GroundArea"/> reads them), either way narrowed to some kinds.
    /// Refused in words when it is neither or both, or names a kind that doesn't exist.
    /// </summary>
    static class ObjectPick
    {
        public sealed class Picked
        {
            internal Picked(IReadOnlyList<PlacedObjectRecord> objects, string where, IReadOnlyList<string> missing)
            {
                Objects = objects;
                Where = where;
                Missing = missing;
            }

            public IReadOnlyList<PlacedObjectRecord> Objects { get; }

            /// <summary>The area in words, e.g. " in a 20 × 20 m area around (64, 64), heading 0°"; empty when picked by id.</summary>
            public string Where { get; }

            /// <summary>Ids that name no placed object here.</summary>
            public IReadOnlyList<string> Missing { get; }
        }

        public static Picked Of(BuildContext context, IReadOnlyList<string> ids, float? x, float? z, float? width, float? depth,
            float? heading, IReadOnlyList<GroundPoint> points, IReadOnlyList<string> kinds)
        {
            var byArea = x.HasValue || z.HasValue || width.HasValue || depth.HasValue || points != null;
            if (byArea == (ids != null))
                throw new CommandException("Give either the objects' ids or an area (a rectangle or points), not both and not neither.");

            // Every kind is checked first, so a typo acts on nothing.
            var only = kinds == null ? null : new HashSet<RecordId>(kinds.Select(kind => context.BaseObject(kind).Id));
            bool Wanted(PlacedObjectRecord placed) => only == null || only.Contains(placed.BaseId);

            if (!byArea)
            {
                var found = new List<PlacedObjectRecord>();
                var missing = new List<string>();
                foreach (var id in ids.Distinct())
                {
                    var placed = context.PlacedInSpace(id);
                    if (placed == null)
                        missing.Add(id);
                    else if (Wanted(placed))
                        found.Add(placed);
                }

                return new Picked(found, "", missing);
            }

            var (area, words) = GroundArea.Of(x, z, width, depth, heading, points);
            var inside = context.PlacedObjects()
                .Where(placed => Wanted(placed) && area.SignedDistance(new Vector2(placed.Position.X, placed.Position.Z)) <= 0f)
                .ToList();
            return new Picked(inside, " in " + words, Array.Empty<string>());
        }
    }
}
