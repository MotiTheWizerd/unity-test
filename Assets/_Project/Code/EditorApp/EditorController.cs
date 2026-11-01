using System;
using System.Linq;
using Forge.Data;
using Forge.Data.Records;
using Forge.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Forge.EditorApp
{
    /// <summary>
    /// Translates input into edits: click places the selected object on the
    /// surface under the cursor, Delete removes the object under the cursor,
    /// 1-9 picks from the palette, Ctrl+S saves, Ctrl+Z / Ctrl+Y (or
    /// Ctrl+Shift+Z) undo and redo.
    /// </summary>
    public sealed class EditorController : MonoBehaviour
    {
        [SerializeField] Camera _camera;
        [SerializeField] EditorOverlay _overlay;
        [SerializeField] float _maxRayDistance = 2000f;

        [NonSerialized] EditorContext _context;

        public void Initialize(EditorContext context) => _context = context;

        void Update()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (_context == null || keyboard == null || mouse == null)
                return;

            HandlePaletteKeys(keyboard);

            if (keyboard.ctrlKey.isPressed)
            {
                if (keyboard.sKey.wasPressedThisFrame)
                    _context.Save();
                else if (keyboard.zKey.wasPressedThisFrame && !keyboard.shiftKey.isPressed)
                    _context.Undo();
                else if (keyboard.yKey.wasPressedThisFrame || (keyboard.zKey.wasPressedThisFrame && keyboard.shiftKey.isPressed))
                    _context.Redo();
            }

            var pointer = mouse.position.ReadValue();
            if (_overlay.IsPointerOverPanel(pointer))
                return;

            if (mouse.leftButton.wasPressedThisFrame && !mouse.rightButton.isPressed)
                Run("place", () => PlaceAt(pointer));

            if (keyboard.deleteKey.wasPressedThisFrame)
                Run("delete", () => DeleteAt(pointer));
        }

        void HandlePaletteKeys(Keyboard keyboard)
        {
            var palette = _context.Session.World.Records.OfType<BaseObjectRecord>().OrderBy(r => r.Id).ToList();
            for (var i = 0; i < Math.Min(9, palette.Count); i++)
            {
                if (keyboard[Key.Digit1 + i].wasPressedThisFrame)
                {
                    _context.SelectedBaseId = palette[i].Id;
                    _context.Report($"Selected {palette[i].DisplayName}");
                }
            }
        }

        void PlaceAt(Vector2 pointer)
        {
            if (_context.SelectedBaseId.IsDefault || !Raycast(pointer, out var hit))
                return;

            var id = _context.Session.NewId("ref");
            var name = _context.Session.World.Records.TryGet(_context.SelectedBaseId, out var baseRecord) && baseRecord is BaseObjectRecord b
                ? b.DisplayName
                : _context.SelectedBaseId.ToString();
            _context.Session.Put(
                new PlacedObjectRecord(id, _context.SelectedBaseId, hit.point.ToNumerics(), System.Numerics.Quaternion.Identity, 1f),
                $"Place {name}");
            _context.Report($"Placed {name} ({id})");
        }

        void DeleteAt(Vector2 pointer)
        {
            if (!Raycast(pointer, out var hit))
                return;

            var link = hit.collider.GetComponentInParent<RecordLink>();
            if (link == null || !_context.Session.World.Records.TryGet(link.RecordId, out var record) || !(record is PlacedObjectRecord))
                return;

            _context.Session.Delete(link.RecordId, $"Delete {link.RecordId.Name}");
            _context.Report($"Deleted {link.RecordId}");
        }

        bool Raycast(Vector2 pointer, out RaycastHit hit) =>
            Physics.Raycast(_camera.ScreenPointToRay(pointer), out hit, _maxRayDistance);

        // Edits are validated by the session; surface failures instead of crashing the tool.
        void Run(string action, Action edit)
        {
            try
            {
                edit();
            }
            catch (Exception ex) when (ex is DataValidationException || ex is ArgumentException)
            {
                Debug.LogWarning($"[Forge] {action} failed: {ex.Message}");
                _context.Report($"{action} failed: {ex.Message}");
            }
        }
    }
}
