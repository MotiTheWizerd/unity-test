using Forge.Data;

namespace Forge.EditorApp
{
    /// <summary>What an assets browser tile being dragged into the world carries (<see cref="EditorContext.Drag"/>).</summary>
    public readonly struct AssetDrag
    {
        public AssetDrag(AssetDragKind kind, RecordId id)
        {
            Kind = kind;
            Id = id;
        }

        public AssetDragKind Kind { get; }

        /// <summary>The material or ground layer dragged.</summary>
        public RecordId Id { get; }
    }

    /// <summary>The kinds of tile that can be dropped on the world.</summary>
    public enum AssetDragKind
    {
        /// <summary>Dresses the part of an object it is dropped on.</summary>
        Material,

        /// <summary>Paints a spot of ground where it is dropped.</summary>
        Ground,
    }
}
