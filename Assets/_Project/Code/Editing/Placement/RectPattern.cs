namespace Forge.Editing.Placement
{
    /// <summary>How <see cref="RectLayout"/> lays one kind of object over a rectangle.</summary>
    public enum RectPattern
    {
        /// <summary>Around its edge, inside it, closed at the corners: walls, fences, hedges.</summary>
        Outline,

        /// <summary>Over all of it, in rows: floor tiles, paving, crops, an orchard.</summary>
        Fill,
    }
}
