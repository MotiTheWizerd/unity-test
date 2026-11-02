using System.Numerics;

namespace Forge.Editing.Placement
{
    /// <summary>One object a placement stroke will create: where on the ground (x, z), its heading and its scale.</summary>
    public readonly struct StrokeItem
    {
        public StrokeItem(Vector2 position, float yawDegrees, float scale)
        {
            Position = position;
            YawDegrees = yawDegrees;
            Scale = scale;
        }

        public Vector2 Position { get; }

        public float YawDegrees { get; }

        public float Scale { get; }

        public StrokeItem WithYaw(float yawDegrees) => new StrokeItem(Position, yawDegrees, Scale);
    }
}
