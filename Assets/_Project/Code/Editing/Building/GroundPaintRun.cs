using System;
using Forge.Data;
using Forge.Editing.Commands;
using Forge.Editing.Terrain;

namespace Forge.Editing.Building
{
    /// <summary>
    /// What the ground painting commands share: the ground looked up by id or name, one <see cref="GroundPainting"/>
    /// over the space's land, its refusals in words for the caller, and the paint written as one undo step.
    /// </summary>
    static class GroundPaintRun
    {
        /// <summary>The ground's default soft edge, in meters: enough that a painted shape doesn't look cut out.</summary>
        public const float DefaultEdge = 1f;

        /// <param name="shape">Paints the shape; returns the texels it painted.</param>
        /// <returns>The painting and how many chunks it wrote.</returns>
        public static (GroundPainting Painting, int Chunks) Paint(BuildContext context, string ground, Func<GroundPainting, int> shape)
        {
            var layer = context.Ground(ground);
            if (!(context.UnpaintedGround is RecordId unpainted))
                throw new CommandException("This world has no ground layer for unpainted land, so painting would change how all of it looks; nothing was painted.");

            var painting = new GroundPainting(context.Session.World.Records, context.Space, layer.Id, unpainted);
            if (Refusal.Of(() => shape(painting)) == 0)
                throw new CommandException("There is no land under that shape; inspect_area shows where the ground is.");

            return (painting, TerrainEdits.Commit(context.Session, painting));
        }
    }
}
