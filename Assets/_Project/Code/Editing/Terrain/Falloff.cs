namespace Forge.Editing.Terrain
{
    /// <summary>The soft edge of terrain edits, shared by the brush and levelled areas.</summary>
    public static class Falloff
    {
        /// <summary>Full effect (1) at <paramref name="t"/> = 0, easing smoothly to none (0) at 1 and beyond.</summary>
        public static float Smooth(float t)
        {
            if (t <= 0f)
                return 1f;
            if (t >= 1f)
                return 0f;
            return 1f - t * t * (3f - 2f * t);
        }
    }
}
