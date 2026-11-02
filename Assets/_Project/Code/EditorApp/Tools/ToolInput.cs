using UnityEngine;

namespace Forge.EditorApp.Tools
{
    /// <summary>One frame of pointer and key input, already filtered by the controller (e.g. UI hits removed).</summary>
    public readonly struct ToolInput
    {
        public ToolInput(Ray ray, bool pointerOverUI, bool primaryPressed, bool primaryHeld, bool primaryReleased, bool additive, bool cancel, bool delete, bool snap, float deltaTime)
        {
            Ray = ray;
            PointerOverUI = pointerOverUI;
            DeltaTime = deltaTime;
            PrimaryPressed = primaryPressed;
            PrimaryHeld = primaryHeld;
            PrimaryReleased = primaryReleased;
            Additive = additive;
            Cancel = cancel;
            Delete = delete;
            Snap = snap;
        }

        /// <summary>World ray under the pointer.</summary>
        public Ray Ray { get; }

        /// <summary>The pointer is over the UI panel, so <see cref="Ray"/> points at the panel, not the world.</summary>
        public bool PointerOverUI { get; }

        public bool PrimaryPressed { get; }

        public bool PrimaryHeld { get; }

        public bool PrimaryReleased { get; }

        /// <summary>Modifier for additive selection (Shift).</summary>
        public bool Additive { get; }

        /// <summary>Esc: cancel the current action.</summary>
        public bool Cancel { get; }

        public bool Delete { get; }

        /// <summary>Modifier for snapping gizmo drags to steps (Ctrl).</summary>
        public bool Snap { get; }

        /// <summary>Seconds since the previous frame (unscaled), for time-based tools like brushes.</summary>
        public float DeltaTime { get; }
    }
}
