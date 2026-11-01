using Forge.Data;
using UnityEngine;

namespace Forge.World
{
    /// <summary>
    /// Tags a scene object with the record it presents, so tools can map raycast
    /// hits back to data. The record id is runtime-only state set by <see cref="WorldView"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RecordLink : MonoBehaviour
    {
        public RecordId RecordId { get; internal set; }
    }
}
