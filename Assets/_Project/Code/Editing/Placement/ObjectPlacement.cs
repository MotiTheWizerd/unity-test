using Forge.Data;

namespace Forge.Editing.Placement
{
    /// <summary>A new object to place: its base object, its seated pose and its scale.</summary>
    public readonly struct ObjectPlacement
    {
        public ObjectPlacement(RecordId baseId, ObjectPose pose, float scale)
        {
            BaseId = baseId;
            Pose = pose;
            Scale = scale;
        }

        public RecordId BaseId { get; }

        public ObjectPose Pose { get; }

        public float Scale { get; }
    }
}
