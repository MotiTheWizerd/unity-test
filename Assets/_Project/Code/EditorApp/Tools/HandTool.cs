using System;

namespace Forge.EditorApp.Tools
{
    /// <summary>
    /// Drag with the left button to pan the view, the way the middle button does in every tool: the point grabbed
    /// stays under the pointer. Edits nothing, so it works while the agent's build is open. Esc returns to Select.
    /// </summary>
    public sealed class HandTool : IEditorTool
    {
        readonly EditorContext _context;
        readonly ICameraRig _camera;
        bool _panning;

        public HandTool(EditorContext context, ICameraRig camera)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _camera = camera ?? throw new ArgumentNullException(nameof(camera));
        }

        public void Activate() => _panning = false;

        public void Deactivate() => _panning = false;

        public void Tick(in ToolInput input)
        {
            if (input.Cancel && !_panning)
            {
                _context.SelectTool(ToolKind.Select);
                return;
            }

            if (input.PrimaryPressed)
            {
                _panning = true;
                _camera.BeginPan(input.Pointer);
            }

            if (!input.PrimaryHeld)
                _panning = false;
            if (_panning)
                _camera.Pan(input.PointerDelta);
        }
    }
}
