namespace Forge.Editing.Placement
{
    /// <summary>
    /// What placement needs to know about an object's look, at scale 1 and no rotation: its ground
    /// footprint, and how far its pivot sits above its lowest point (0 for pivots at the base).
    /// Measured by the presentation layer; plain data here.
    /// </summary>
    public readonly struct PlacementShape
    {
        public PlacementShape(Footprint footprint, float pivotAboveBottom)
        {
            Footprint = footprint;
            PivotAboveBottom = pivotAboveBottom;
        }

        public Footprint Footprint { get; }

        public float PivotAboveBottom { get; }
    }
}
