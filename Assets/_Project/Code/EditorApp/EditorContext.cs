using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Forge.Data;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Data.Serialization;
using Forge.EditorApp.Tools;
using Forge.Editing;
using Forge.Editing.Palette;
using Forge.Editing.Terrain;

namespace Forge.EditorApp
{
    /// <summary>
    /// Shared state of the running editor: the session, selection, active tool,
    /// palette choice, where the active plugin is saved, and the last status
    /// message. Plain C# so tools and UI stay decoupled from each other.
    /// </summary>
    public sealed class EditorContext
    {
        readonly PluginFileStore _activeStore;
        float _brushRadius = 8f;
        float _brushStrength = 0.5f;
        ResolvedWorld _paletteWorld;
        ObjectPalette _palette;

        public EditorContext(EditSession session, PluginFileStore activeStore)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            _activeStore = activeStore ?? throw new ArgumentNullException(nameof(activeStore));
            SelectedBaseId = Palette.Objects.Select(r => r.Id).FirstOrDefault();
            Status = $"Loaded {session.World.LoadOrder.Count} plugin(s).";
        }

        public EditSession Session { get; }

        /// <summary>
        /// Base objects the place tool offers, by category. Cached per world version,
        /// because UI and shortcuts read it every frame and a large world has many records.
        /// </summary>
        public ObjectPalette Palette
        {
            get
            {
                if (!ReferenceEquals(_paletteWorld, Session.World))
                {
                    _paletteWorld = Session.World;
                    _palette = ObjectPalette.From(_paletteWorld.Records.OfType<BaseObjectRecord>());
                }

                return _palette;
            }
        }

        /// <summary>Time the last world-changing edit, undo or redo took, including the scene update.</summary>
        public double LastEditMs { get; private set; }

        public Selection Selection { get; } = new Selection();

        public ToolKind ActiveTool { get; set; } = ToolKind.Select;

        /// <summary>What dragging the select tool's gizmo does.</summary>
        public TransformMode TransformMode { get; private set; } = TransformMode.Move;

        /// <summary>Switches to the select tool in <paramref name="mode"/> (W / E / R and the panel buttons).</summary>
        public void SelectTransformMode(TransformMode mode)
        {
            TransformMode = mode;
            ActiveTool = ToolKind.Select;
        }

        public string ActivePluginPath => _activeStore.PathFor(Session.ActivePluginId);

        /// <summary>Base object the place tool creates.</summary>
        public RecordId SelectedBaseId { get; set; }

        /// <summary>Switches to the place tool with <paramref name="baseId"/> (1-9 and the palette buttons).</summary>
        public void SelectPlacement(RecordId baseId)
        {
            SelectedBaseId = baseId;
            ActiveTool = ToolKind.Place;
        }

        /// <summary>Turn per [ / ] press while placing; with Shift, <see cref="PlacementCoarseStep"/>.</summary>
        public const float PlacementStep = 15f;

        public const float PlacementCoarseStep = 90f;

        /// <summary>Yaw in degrees [0, 360) the place tool gives new objects.</summary>
        public float PlacementYaw { get; private set; }

        /// <summary>Turns the next placement by <paramref name="degrees"/> ([ / ] and the panel buttons).</summary>
        public void RotatePlacement(float degrees)
        {
            var yaw = (PlacementYaw + degrees) % 360f;
            PlacementYaw = yaw < 0f ? yaw + 360f : yaw;
        }

        public BrushMode BrushMode { get; set; } = BrushMode.Raise;

        /// <summary>Whether sculpting carries placed objects up and down with the ground.</summary>
        public bool ObjectsFollowTerrain { get; set; } = true;

        /// <summary>Brush radius in meters; clamped to the brush's valid range.</summary>
        public float BrushRadius
        {
            get => _brushRadius;
            set => _brushRadius = Math.Max(BrushSettings.MinRadius, Math.Min(BrushSettings.MaxRadius, value));
        }

        /// <summary>Lowest brush strength the editor offers (a weaker brush does nothing visible).</summary>
        public const float MinBrushStrength = 0.05f;

        /// <summary>Brush strength in [<see cref="MinBrushStrength"/>, 1]; clamped.</summary>
        public float BrushStrength
        {
            get => _brushStrength;
            set => _brushStrength = Math.Max(MinBrushStrength, Math.Min(1f, value));
        }

        public BrushSettings CurrentBrush() => new BrushSettings(BrushMode, BrushRadius, BrushStrength);

        public string Status { get; private set; }

        public void Report(string message) => Status = message;

        /// <summary>
        /// Runs an edit; rejected edits (invalid data) are reported instead of
        /// thrown, so tools and UI never crash on a bad action.
        /// </summary>
        public bool TryEdit(string action, Action edit)
        {
            try
            {
                Timed(edit);
                return true;
            }
            catch (Exception ex) when (ex is DataValidationException || ex is ArgumentException || ex is InvalidOperationException)
            {
                Report($"{action} failed: {ex.Message}");
                UnityEngine.Debug.LogWarning($"[Forge] {action} failed: {ex.Message}");
                return false;
            }
        }

        public void Undo()
        {
            var description = Session.UndoDescription;
            var undone = false;
            Timed(() => undone = Session.Undo());
            Report(undone ? $"Undid: {description}" : "Nothing to undo");
        }

        public void Redo()
        {
            var description = Session.RedoDescription;
            var redone = false;
            Timed(() => redone = Session.Redo());
            Report(redone ? $"Redid: {description}" : "Nothing to redo");
        }

        // Records the duration only when the world actually changed, so rejected
        // or no-op actions don't overwrite the last real measurement.
        void Timed(Action action)
        {
            var before = Session.World;
            var clock = Stopwatch.StartNew();
            action();
            if (!ReferenceEquals(before, Session.World))
                LastEditMs = clock.Elapsed.TotalMilliseconds;
        }

        /// <summary>Saves the active plugin; failures are reported, not thrown, so any UI can call it.</summary>
        public bool Save()
        {
            try
            {
                _activeStore.Save(Session.BuildActivePlugin());
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is DataValidationException)
            {
                Report($"Save failed: {ex.Message}");
                return false;
            }

            Session.MarkSaved();
            Report($"Saved {Session.ActivePluginId} to {ActivePluginPath}");
            return true;
        }

        public string DisplayNameOf(RecordId id) =>
            Session.World.Records.TryGet(id, out var record) && record is BaseObjectRecord baseObject
                ? baseObject.DisplayName
                : id.ToString();
    }
}
