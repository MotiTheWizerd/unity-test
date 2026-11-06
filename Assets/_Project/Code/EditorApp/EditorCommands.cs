using System;
using System.Collections.Generic;
using System.Linq;
using Forge.Data;
using Forge.Data.Records;
using Forge.EditorApp.Tools;
using Forge.Editing;
using Forge.Editing.Placement;
using Forge.Editing.Water;
using Forge.World;
using UnityEngine;

namespace Forge.EditorApp
{
    /// <summary>
    /// One-shot editor actions shared by keyboard shortcuts and UI buttons, so
    /// each action has exactly one implementation.
    /// </summary>
    public sealed class EditorCommands
    {
        // Rays start this far above an object's top, so they begin above any terrain under it.
        const float ProbeHeight = 1000f;

        readonly EditorContext _context;
        readonly WorldView _view;
        readonly ScenePicker _picker;
        readonly ICameraRig _camera;
        readonly SlopeAlignment _slopes;
        readonly AssetSlots _slots;
        readonly Func<Ray> _pointerRay;

        /// <param name="pointerRay">The world ray under the mouse pointer now (where a dragged tile would land).</param>
        public EditorCommands(EditorContext context, WorldView view, ScenePicker picker, ICameraRig camera, SlopeAlignment slopes, AssetSlots slots, Func<Ray> pointerRay)
        {
            _pointerRay = pointerRay ?? throw new ArgumentNullException(nameof(pointerRay));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _picker = picker ?? throw new ArgumentNullException(nameof(picker));
            _camera = camera ?? throw new ArgumentNullException(nameof(camera));
            _slopes = slopes ?? throw new ArgumentNullException(nameof(slopes));
            _slots = slots ?? throw new ArgumentNullException(nameof(slots));
        }

        /// <summary>
        /// Repaints the whole space on show by its shape (the Paint tool's Auto button): rock on steep ground, dirt on
        /// slopes, sand at the shore, unpainted ground's layer elsewhere. One undo step.
        /// </summary>
        public void AutoPaintSpace()
        {
            var records = _context.Session.World.Records;
            if (!(_context.UnpaintedGround is RecordId flat))
            {
                _context.Report("Auto paint: no ground layer looks like unpainted ground");
                return;
            }

            // SHORTCUT: the roles are the base game's looks; per-world rules (a climate's or worldspace's) become data
            // when worlds need their own ground (see ARCHITECTURE.md).
            RecordId? Look(string assetKey) => Forge.Editing.Terrain.GroundLayers.WithLook(records, assetKey);
            var rules = new Forge.Editing.Terrain.AutoPaintRules(flat, Look("ground/dirt"), Look("ground/rock"), Look("ground/sand"));

            var painted = 0;
            if (_context.TryEdit("Auto paint", () => painted = Forge.Editing.Terrain.AutoPaint.Apply(_context.Session, _context.ActiveSpace, rules)))
                _context.Report(painted > 0 ? $"Auto painted {painted} chunk(s) by their shape" : "Auto paint: this space has no land");
        }

        /// <summary>Moves the current lake's surface to <paramref name="level"/> (the Water tool's level field).</summary>
        public void SetCurrentLakeLevel(float level)
        {
            var lake = _context.CurrentLakeRecord;
            if (lake == null)
                return;
            var changed = false;
            if (_context.TryEdit("Lake level", () => changed = WaterEdits.SetLakeLevel(_context.Session, lake.Id, level)) && changed)
                _context.Report(_context.Session.UndoDescription);
        }

        /// <summary>Removes the current lake (Del in the Water tool, or its Remove button).</summary>
        public void RemoveCurrentLake()
        {
            var lake = _context.CurrentLakeRecord;
            if (lake == null)
            {
                _context.Report("Water: click a lake to pick it first");
                return;
            }

            if (_context.TryEdit("Remove lake", () => WaterEdits.RemoveLake(_context.Session, lake.Id)))
            {
                _context.CurrentLake = null;
                _context.Report("Removed the lake");
            }
        }

        /// <summary>
        /// The material slots of the selected objects, in the order they are first worn, each once; empty when nothing
        /// selected has any. Slots come from the objects' assets (<see cref="AssetSlots"/>).
        /// </summary>
        public IReadOnlyList<string> SelectionSlots()
        {
            var slots = new List<string>();
            foreach (var id in _context.Selection.Ids)
            {
                foreach (var slot in SlotsOf(id))
                {
                    if (!slots.Contains(slot))
                        slots.Add(slot);
                }
            }

            return slots;
        }

        IReadOnlyList<string> SlotsOf(RecordId placedId)
        {
            var records = _context.Session.World.Records;
            return records.TryGet(placedId, out var record) && record is PlacedObjectRecord placed
                   && records.TryGet(placed.BaseId, out var kind) && kind is BaseObjectRecord baseObject
                ? _slots.SlotsOf(baseObject)
                : Array.Empty<string>();
        }

        /// <summary>The slot picking a material dresses: the one chosen when the selection has it, else its first; null when it has none.</summary>
        public string DressedSlot(IReadOnlyList<string> slots) =>
            slots.Count == 0 ? null
            : _context.MaterialSlot != null && slots.Contains(_context.MaterialSlot) ? _context.MaterialSlot
            : slots[0];

        /// <summary>
        /// What the selection wears in <paramref name="slot"/>: the material of the first selected object that has one
        /// there, or null when they all keep their base look.
        /// </summary>
        public RecordId? SelectionMaterial(string slot)
        {
            if (slot == null)
                return null;
            var records = _context.Session.World.Records;
            foreach (var id in _context.Selection.Ids)
            {
                if (records.TryGet(id, out var record) && record is PlacedObjectRecord placed
                    && MaterialOverrides.In(MaterialOverrides.Layer(BaseOf(placed)?.Materials, placed.Materials), slot) is RecordId worn)
                    return worn;
            }

            return null;
        }

        BaseObjectRecord BaseOf(PlacedObjectRecord placed) =>
            _context.Session.World.Records.TryGet(placed.BaseId, out var record) ? record as BaseObjectRecord : null;

        /// <summary>The one placed object selected, or null when the selection is empty or holds more than one.</summary>
        public PlacedObjectRecord SingleSelected() =>
            _context.Selection.Count == 1 && _context.Session.World.Records.TryGet(_context.Selection.Ids.First(), out var record)
                ? record as PlacedObjectRecord
                : null;

        /// <summary>A name for a variant of the one selected object: its kind's name with what makes it differ, e.g. "Small House (Thatch)".</summary>
        public string SuggestedVariantName()
        {
            var placed = SingleSelected();
            var kind = placed != null ? BaseOf(placed) : null;
            if (kind == null)
                return string.Empty;
            var worn = placed.Materials.Select(m => NameOf(m.Material)).Distinct().ToList();
            return worn.Count == 0 ? $"{kind.DisplayName} (variant)" : $"{kind.DisplayName} ({string.Join(", ", worn)})";
        }

        /// <summary>
        /// Saves the one selected object's look as a new kind of object named <paramref name="name"/>
        /// (<see cref="ObjectEdits.MakeVariant"/>), files it beside its kind in the assets browser and makes it what the
        /// placing tools lay next. One undo step.
        /// </summary>
        public void MakeVariantOfSelection(string name)
        {
            var placed = SingleSelected();
            if (placed == null)
            {
                _context.Report("Make variant: select one object first");
                return;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                _context.Report("Make variant: give it a name");
                return;
            }

            RecordId variant = default;
            if (_context.TryEdit("Make variant", () => variant = ObjectEdits.MakeVariant(_context.Session, placed.Id, name)))
            {
                _context.SelectedBaseId = variant;
                _context.Report($"Saved {name.Trim()} as a new object; place more of it from the assets browser");
            }
        }

        /// <summary>
        /// Dresses the chosen slot (<see cref="DressedSlot"/>) of every selected object that has it in
        /// <paramref name="material"/> (the Materials page's balls). One undo step.
        /// </summary>
        public void DressSelection(RecordId material)
        {
            var slot = DressedSlot(SelectionSlots());
            if (slot == null)
            {
                _context.Report(_context.Selection.IsEmpty ? "Material: select an object first" : "Material: the selection has no material slots");
                return;
            }

            var changed = 0;
            if (_context.TryEdit("Set material", () => changed = ObjectEdits.SetMaterial(_context.Session, SelectedWithSlot(slot), slot, material)))
                _context.Report(changed > 0
                    ? $"{NameOf(material)} on {Materials.SlotLabel(slot)} of {changed} object(s)"
                    : $"Already {NameOf(material)} on {Materials.SlotLabel(slot)}");
        }

        /// <summary>Gives every selected object back its base object's look in every slot (the Materials page's Reset look). One undo step.</summary>
        public void ResetSelectionMaterials()
        {
            var changed = 0;
            if (_context.TryEdit("Reset materials", () => changed = ObjectEdits.ResetMaterials(_context.Session, _context.Selection.Ids)))
                _context.Report(changed > 0 ? $"Reset the materials of {changed} object(s)" : "Materials: the selection wears its own look already");
        }

        /// <summary>
        /// Applies <paramref name="change"/> to the transform of every selected object (the inspector's numbers), as one
        /// undo step named <paramref name="verb"/>; objects it leaves as they were are skipped.
        /// </summary>
        public void TransformSelection(string verb, Func<PlacedObjectRecord, PlacedObjectRecord> change)
        {
            if (change == null)
                throw new ArgumentNullException(nameof(change));
            var changed = 0;
            if (_context.TryEdit(verb, () => changed = ObjectEdits.Transform(_context.Session, _context.Selection.Ids, change, verb)))
                _context.Report(changed > 0 ? $"{verb}: {changed} object(s)" : $"{verb}: nothing changed");
        }

        // Selected objects whose asset has the slot, so dressing it never writes an override no one could see.
        List<RecordId> SelectedWithSlot(string slot) =>
            _context.Selection.Ids.Where(id => SlotsOf(id).Contains(slot)).ToList();

        string NameOf(RecordId material) =>
            _context.Session.World.Records.TryGet(material, out var record) && record is MaterialRecord found ? found.DisplayName : material.ToString();

        /// <summary>
        /// The placed object and material slot under the mouse pointer: where a dragged material would land. False over
        /// anything else, or a part that wears no named material.
        /// </summary>
        public bool TryPartUnderPointer(out RecordId id, out string slot) =>
            _picker.TryPickPlacedObject(_pointerRay(), _context.Session.World.Records, out id, out _, out slot) && slot != null;

        /// <summary>
        /// Dresses the part under the mouse pointer in <paramref name="material"/> (a material ball dropped on it), that one
        /// object only, and makes its slot the chosen one. One undo step.
        /// </summary>
        public void DropMaterial(RecordId material)
        {
            if (!TryPartUnderPointer(out var id, out var slot))
            {
                _context.Report("Material: drop it on a part of an object");
                return;
            }

            _context.MaterialSlot = slot;
            var changed = 0;
            if (_context.TryEdit("Set material", () => changed = ObjectEdits.SetMaterial(_context.Session, new[] { id }, slot, material)))
                _context.Report(changed > 0 ? $"{NameOf(material)} on {Materials.SlotLabel(slot)}" : $"Already {NameOf(material)} on {Materials.SlotLabel(slot)}");
        }

        /// <summary>
        /// Paints one dab of <paramref name="layer"/> where the mouse pointer meets the land (a ground tile dropped there),
        /// with the paint brush's size and strength, and makes it the paint tool's ground. One undo step.
        /// </summary>
        public void DropGround(RecordId layer)
        {
            if (!(_context.UnpaintedGround is RecordId unpainted))
            {
                _context.Report("Paint: no ground layer looks like unpainted ground");
                return;
            }

            if (!_picker.TryPickTerrain(_pointerRay(), out var hit))
            {
                _context.Report("Ground: drop it on the land");
                return;
            }

            _context.PaintLayer = layer;
            var chunks = 0;
            if (_context.TryEdit("Paint", () =>
                {
                    var stroke = new Forge.Editing.Terrain.PaintStroke(_context.Session.World.Records, _context.ActiveSpace, layer,
                        new Forge.Editing.Terrain.BrushShape(_context.BrushRadius, _context.BrushStrength), unpainted);
                    stroke.ApplyDab(new System.Numerics.Vector2(hit.Point.x, hit.Point.z));
                    chunks = Forge.Editing.Terrain.TerrainEdits.Commit(_context.Session, stroke);
                }))
                _context.Report(chunks > 0 ? $"Painted a spot of {NameOfGround(layer)}" : "Ground: nothing to paint there");
        }

        string NameOfGround(RecordId layer) =>
            _context.Session.World.Records.TryGet(layer, out var record) && record is GroundLayerRecord ground ? ground.DisplayName : layer.ToString();

        /// <summary>Selects every placed object shown in the space on view (Ctrl+A): the loaded ones, as box select does.</summary>
        public void SelectAll()
        {
            var ids = _view.PlacedObjects.Select(pair => pair.Key).ToList();
            _context.Selection.Replace(ids);
            _context.Report(ids.Count > 0 ? $"Selected all {ids.Count} object(s) in view" : "Select all: no objects here");
        }

        /// <summary>Clears the selection (Ctrl+Shift+A; Esc in a select tool does the same).</summary>
        public void DeselectAll()
        {
            if (_context.Selection.IsEmpty)
                return;
            _context.Selection.Clear();
            _context.Report("Deselected");
        }

        /// <summary>
        /// Copies the selected objects where they stand, materials and all (Ctrl+D, as in Unity and the Creation Kit), and
        /// selects the copies so they can be moved straight away. One undo step.
        /// </summary>
        public void DuplicateSelection()
        {
            if (_context.Selection.IsEmpty)
            {
                _context.Report("Duplicate: select objects first");
                return;
            }

            IReadOnlyList<PlacedObjectRecord> copies = Array.Empty<PlacedObjectRecord>();
            if (!_context.TryEdit("Duplicate", () => copies = ObjectEdits.Duplicate(_context.Session, _context.Selection.Ids, "Duplicate")))
                return;
            if (copies.Count == 0)
            {
                _context.Report("Duplicate: nothing selected can be copied");
                return;
            }

            _context.Selection.Replace(copies.Select(c => c.Id));
            _context.Report($"Duplicated {copies.Count} object(s) in place; move them where you want them");
        }

        /// <summary>Glides the camera so the whole selection fills the view (F).</summary>
        public void FrameSelection()
        {
            var found = false;
            var combined = default(Bounds);
            foreach (var id in _context.Selection.Ids)
            {
                if (!_view.TryGetObject(id, out var go) || !RendererBounds.TryGet(go, out var bounds))
                    continue;
                if (found)
                    combined.Encapsulate(bounds);
                else
                    combined = bounds;
                found = true;
            }

            if (!found)
            {
                _context.Report("Frame: nothing selected");
                return;
            }

            _camera.Frame(combined.center, combined.extents.magnitude);
        }

        /// <summary>
        /// Rests each selected object's lowest point on the terrain directly below
        /// (or above, if it is buried). Objects that align to the slope are seated and tilted
        /// to the ground under their footprint, exactly as placing does. One undo step for the
        /// whole selection.
        /// </summary>
        public void DropSelectionToGround()
        {
            if (_context.Selection.IsEmpty)
            {
                _context.Report("Drop to ground: nothing selected");
                return;
            }

            var poses = new Dictionary<RecordId, (System.Numerics.Vector3 Position, System.Numerics.Quaternion Rotation)>();
            var missed = 0;
            foreach (var id in _context.Selection.Ids)
            {
                if (!_context.Session.World.Records.TryGet(id, out var record) || !(record is PlacedObjectRecord placed))
                    continue;

                if (TrySeatOnSlope(placed, out var pose) || TryDropStraight(placed, out pose))
                    poses[id] = pose;
                else
                    missed++;
            }

            var moved = 0;
            if (_context.TryEdit("Drop to ground", () => moved = ObjectEdits.Transform(_context.Session, poses.Keys,
                    placed => placed.WithTransform(poses[placed.Id].Position, poses[placed.Id].Rotation, placed.Scale), "Drop")))
            {
                _context.Report(missed == 0
                    ? $"Dropped {moved} object(s) to ground"
                    : $"Dropped {moved} object(s); {missed} had no terrain below");
            }
        }

        // Fences, walls, rocks: the same seating as placing, keeping the object's heading.
        bool TrySeatOnSlope(PlacedObjectRecord placed, out (System.Numerics.Vector3, System.Numerics.Quaternion) pose)
        {
            pose = default;
            var shape = _slopes.ShapeToAlign(placed);
            if (!shape.HasValue)
                return false;

            var center = new System.Numerics.Vector2(placed.Position.X, placed.Position.Z);
            if (!GroundFit.TryPose(center, shape.Value, PlacementMath.HeadingOf(placed.Rotation), placed.Scale,
                    _picker.TerrainHeightAt, GroundFit.DefaultMaxTiltDegrees, out var pivot, out var rotation))
                return false;

            pose = (pivot, rotation);
            return true;
        }

        // Everything else drops straight down, keeping its rotation, so its lowest drawn point lands on the surface.
        // The object's local position is its world one (it sits under the world root); heights are never rebased, so
        // the renderer bounds' heights are world heights too.
        bool TryDropStraight(PlacedObjectRecord placed, out (System.Numerics.Vector3, System.Numerics.Quaternion) pose)
        {
            pose = default;
            if (!_view.TryGetObject(placed.Id, out var go) || !RendererBounds.TryGet(go, out var bounds))
                return false;

            var position = go.transform.localPosition;
            var probe = new Ray(new Vector3(position.x, bounds.max.y + ProbeHeight, position.z), Vector3.down);
            if (!_picker.TryPickTerrain(probe, out var ground))
                return false;

            var target = placed.Position;
            target.Y = ground.Point.y + (position.y - bounds.min.y);
            pose = (target, placed.Rotation);
            return true;
        }
    }
}
