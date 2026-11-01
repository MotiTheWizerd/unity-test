namespace Forge.Editing.Terrain
{
    public enum BrushMode
    {
        Raise,
        Lower,

        /// <summary>Pulls heights toward the height under the cursor when the stroke began.</summary>
        Flatten,

        /// <summary>Pulls heights toward the average of their neighbours.</summary>
        Smooth,
    }
}
