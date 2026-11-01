using UnityEngine;

namespace Forge.EditorApp.Tools
{
    /// <summary>One frame of pointer and key input, already filtered by the controller (e.g. UI hits removed).</summary>
    public readonly struct ToolInput
    {
        public ToolInput(Ray ray, bool primaryPressed, bool primaryHeld, bool primaryReleased, bool additive, bool cancel, bool delete)
        {
            Ray = ray;
            PrimaryPressed = primaryPressed;
            PrimaryHeld = primaryHeld;
            PrimaryReleased = primaryReleased;
            Additive = additive;
            Cancel = cancel;
            Delete = delete;
        }

        /// <summary>World ray under the pointer.</summary>
        public Ray Ray { get; }

        public bool PrimaryPressed { get; }

        public bool PrimaryHeld { get; }

        public bool PrimaryReleased { get; }

        /// <summary>Modifier for additive selection (Shift).</summary>
        public bool Additive { get; }

        /// <summary>Esc: cancel the current action.</summary>
        public bool Cancel { get; }

        public bool Delete { get; }
    }
}
