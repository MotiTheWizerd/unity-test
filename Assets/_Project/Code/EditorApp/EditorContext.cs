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
using Forge.Editing.Geometry;
using Forge.Editing.Palette;
using Forge.Editing.Placement;
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

        /// <summary>Placement settings, shared by the place tool and the world-building commands.</summary>
        public PlacementOptions PlacementOptions { get; } = new PlacementOptions(new Random());

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

        /// <summary>The tool the user picked (<see cref="SelectTool"/>).</summary>
        public ToolKind ActiveTool { get; private set; } = ToolKind.Select;

        /// <summary>
        /// A tool held for the moment (Space: the hand), standing in for <see cref="ActiveTool"/> until it is let go;
        /// null when none is. Set by the controller, which owns the keys.
        /// </summary>
        public ToolKind? HeldTool { get; set; }

        /// <summary>The tool the mouse drives right now: the held one, else the picked one.</summary>
        public ToolKind CurrentTool => HeldTool ?? ActiveTool;

        /// <summary>
        /// Picks <paramref name="kind"/> (its key, the tool strip, Esc back to Select). Tools that change the world
        /// wait while the agent's build is open, and the place tool needs an object from the palette.
        /// </summary>
        public bool SelectTool(ToolKind kind)
        {
            var tool = ToolCatalog.Of(kind);
            if (tool.EditsWorld && RefusedForDraft(tool.Name))
                return false;
            if (tool.LaysPaletteObject && SelectedBaseObject == null)
            {
                Report($"{tool.Name}: pick an object in the palette first");
                return false;
            }

            ActiveTool = kind;
            return true;
        }

        /// <summary>The space shown and edited: tools, commands and the agent all work in it.</summary>
        public RecordId ActiveSpace { get; private set; }

        /// <summary>Raised after <see cref="ActiveSpace"/> changes, so the view can switch over.</summary>
        public event Action<RecordId> SpaceChanged;

        /// <summary>
        /// Shows and edits <paramref name="space"/> from now on. What belonged to the old space (the selection, the
        /// mark) is cleared. Refused while the agent's build is open, which belongs to the current space.
        /// </summary>
        public bool ShowSpace(RecordId space)
        {
            if (space == ActiveSpace)
                return true;
            if (RefusedForDraft("Switching space"))
                return false;
            if (!(Session.World.Records.TryGet(space, out var record) && record is SpaceRecord spaceRecord))
            {
                Report($"There is no space '{space}'.");
                return false;
            }

            ActiveSpace = space;
            Selection.Clear();
            Mark = null;
            SpaceChanged?.Invoke(space);
            Report($"Showing {spaceRecord.DisplayName}");
            return true;
        }

        /// <summary>The record of <see cref="ActiveSpace"/>, or null if it no longer exists.</summary>
        public SpaceRecord ActiveSpaceRecord =>
            Session.World.Records.TryGet(ActiveSpace, out var record) ? record as SpaceRecord : null;

        /// <summary>The box being dragged to select what is inside it (screen pixels, bottom-left origin), or null.</summary>
        public ScreenBox? SelectionBox { get; set; }

        /// <summary>The spot or area the user marked for the agent (M), or null. Editor state: never saved or undone.</summary>
        public GroundMark Mark { get; set; }

        public string ActivePluginPath => _activeStore.PathFor(Session.ActivePluginId);

        /// <summary>Base object the place tool creates.</summary>
        public RecordId SelectedBaseId { get; set; }

        /// <summary>The record of <see cref="SelectedBaseId"/>, or null if it names no base object.</summary>
        public BaseObjectRecord SelectedBaseObject =>
            Session.World.Records.TryGet(SelectedBaseId, out var record) ? record as BaseObjectRecord : null;

        /// <summary>
        /// Chooses <paramref name="baseId"/> to lay (1-9 and the palette buttons). A tool that lays the palette's
        /// object (place, outline) keeps going with it; any other switches to the place tool.
        /// </summary>
        public void SelectPlacement(RecordId baseId)
        {
            SelectedBaseId = baseId;
            RerollPlacement();
            if (!ToolCatalog.Of(ActiveTool).LaysPaletteObject)
                SelectTool(ToolKind.Place);
        }

        /// <summary>Whether the outline and fill tools size their rectangle to whole pieces, so every side ends cleanly.</summary>
        public bool RectangleSnapToPieces { get; set; } = true;

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

        /// <summary>Whether new objects get their base object's random variation (turn and scale).</summary>
        public bool RandomizePlacement
        {
            get => PlacementOptions.Randomize;
            set
            {
                PlacementOptions.Randomize = value;
                RerollPlacement();
            }
        }

        /// <summary>The variation the next placed object gets; the place tool previews it.</summary>
        public PlacementRoll PlacementRoll { get; private set; } = PlacementRoll.Identity;

        /// <summary>Draws a new variation for the next placement (after each placement and on a new object).</summary>
        public void RerollPlacement() => PlacementRoll = DrawPlacementRoll();

        /// <summary>A fresh variation for the selected object (identity when randomizing is off), e.g. for each object of a painted row.</summary>
        public PlacementRoll DrawPlacementRoll() => PlacementOptions.DrawRoll(SelectedBaseObject);

        /// <summary>Whether the selected object gets a random heading, so a painted row keeps it instead of following the drag.</summary>
        public bool PlacementHasRandomYaw => PlacementOptions.KeepsOwnHeading(SelectedBaseObject);

        /// <summary>Whether the selected object can vary at all (the "Random turn &amp; size" toggle matters).</summary>
        public bool PlacementCanVary => PlacementRoll.CanVary(SelectedPlacementRules);

        /// <summary>Whether placing, dropping and sculpting tilt objects to the slope when their base object asks for it (fences, rocks).</summary>
        public bool AlignPlacementToSlope
        {
            get => PlacementOptions.AlignToSlope;
            set => PlacementOptions.AlignToSlope = value;
        }

        /// <summary>Whether objects of <paramref name="baseId"/> are tilted to the slope: their rules ask for it and the toggle is on.</summary>
        public bool AlignsToSlope(RecordId baseId) =>
            Session.World.Records.TryGet(baseId, out var record) && PlacementOptions.TiltsToSlope(record as BaseObjectRecord);

        /// <summary>Whether the selected object's rules tilt it to the slope at all (the toggle matters).</summary>
        public bool PlacementCanAlignToSlope => SelectedPlacementRules?.AlignToSlope ?? false;

        /// <summary>Meters left between painted objects; remembered per base object (fences 0, trees roomier).</summary>
        public float PlacementGap
        {
            get => SelectedGap.Gap;
            set => SetSelectedGap(RowGap.Clamped(value, SelectedGap.RandomExtra));
        }

        /// <summary>Up to this many extra meters, drawn per gap, so painted rows don't look regular.</summary>
        public float PlacementGapRandom
        {
            get => SelectedGap.RandomExtra;
            set => SetSelectedGap(RowGap.Clamped(SelectedGap.Gap, value));
        }

        /// <summary>The gap before the next painted object: <see cref="PlacementGap"/> plus a random part of <see cref="PlacementGapRandom"/>.</summary>
        public float DrawPlacementGap() => PlacementOptions.DrawGap(SelectedGap);

        RowGap SelectedGap
        {
            get
            {
                var baseObject = SelectedBaseObject;
                return baseObject != null ? PlacementOptions.GapFor(baseObject) : default;
            }
        }

        void SetSelectedGap(RowGap gap)
        {
            var baseObject = SelectedBaseObject;
            if (baseObject != null)
                PlacementOptions.SetGap(baseObject, gap);
        }

        /// <summary>The selected base object's placement rules, or null if it has none.</summary>
        public PlacementRules SelectedPlacementRules => SelectedBaseObject?.Placement;

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
        /// Whether the agent's draft is open (being built, or waiting for review). Until it is accepted or
        /// discarded, edits, undo, redo and saving wait, so nothing else gets mixed into it.
        /// </summary>
        public bool IsDraftOpen => Session.Draft != null;

        /// <summary>
        /// Runs an edit; rejected edits (invalid data) are reported instead of
        /// thrown, so tools and UI never crash on a bad action.
        /// </summary>
        public bool TryEdit(string action, Action edit)
        {
            if (RefusedForDraft(action))
                return false;

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
            if (RefusedForDraft("Undo"))
                return;

            var description = Session.UndoDescription;
            var undone = false;
            Timed(() => undone = Session.Undo());
            Report(undone ? $"Undid: {description}" : "Nothing to undo");
        }

        public void Redo()
        {
            if (RefusedForDraft("Redo"))
                return;

            var description = Session.RedoDescription;
            var redone = false;
            Timed(() => redone = Session.Redo());
            Report(redone ? $"Redid: {description}" : "Nothing to redo");
        }

        bool RefusedForDraft(string action)
        {
            if (!IsDraftOpen)
                return false;
            Report($"{action} waits: the agent's build is open. Accept or discard it first.");
            return true;
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
            if (RefusedForDraft("Save"))
                return false;

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
