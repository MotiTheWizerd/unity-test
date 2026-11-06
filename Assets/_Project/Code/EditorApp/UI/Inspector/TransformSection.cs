using System;
using System.Collections.Generic;
using System.Numerics;
using Forge.Data.Records;
using Forge.EditorApp.UI.Controls;
using Forge.Editing.Inspection;
using Forge.Editing.Placement;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI.Inspector
{
    /// <summary>
    /// Where the selected placed objects stand, as numbers to type: position (world metres), heading (degrees, the turn
    /// seen from above; a tilt from a slope is kept) and scale. With several selected, a number they share shows, one
    /// they differ in shows as mixed, and typing a value gives it to all of them. Each change is one undo step
    /// (<see cref="EditorCommands.TransformSelection"/>). A box being typed in is never overwritten.
    /// </summary>
    public sealed class TransformSection : IInspectorSection
    {
        const float Tolerance = 1e-4f;
        const float MinScale = 0.01f;
        const int Digits = 8;

        readonly EditorCommands _commands;
        readonly List<Box> _boxes = new List<Box>();

        public TransformSection(EditorCommands commands)
        {
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
            Content = new VisualElement();

            // One row per number, so each box has the room it needs in a narrow dock.
            AddRow("X", "0.##", p => p.Position.X, (p, v) => p.WithPosition(new Vector3(v, p.Position.Y, p.Position.Z)), "Move", "m");
            AddRow("Y", "0.##", p => p.Position.Y, (p, v) => p.WithPosition(new Vector3(p.Position.X, v, p.Position.Z)), "Move", "m");
            AddRow("Z", "0.##", p => p.Position.Z, (p, v) => p.WithPosition(new Vector3(p.Position.X, p.Position.Y, v)), "Move", "m");
            AddRow("Heading", "0.#", p => PlacementMath.HeadingOf(p.Rotation),
                (p, v) => p.WithRotation(PlacementMath.WithHeading(p.Rotation, PlacementMath.NormalizeDegrees(v))), "Turn", "°", SameAngle);
            AddRow("Scale", "0.###", p => p.Scale, (p, v) => p.WithScale(Math.Max(MinScale, v)), "Resize", "×");
        }

        public string Title => "TRANSFORM";

        public VisualElement Content { get; }

        public bool Shows(Inspection inspection) => inspection.All<PlacedObjectRecord>().Count > 0;

        public void Show(Inspection inspection)
        {
            var placed = inspection.All<PlacedObjectRecord>();
            foreach (var box in _boxes)
                box.Show(placed);
        }

        public void Refresh()
        {
        }

        void AddRow(string label, string format, Func<PlacedObjectRecord, float> read,
            Func<PlacedObjectRecord, float, PlacedObjectRecord> write, string verb, string unit, Func<float, float, bool> same = null)
        {
            var row = new ForgePropertyRow(label);
            var field = new ForgeNumberField { digits = Digits, formatString = format, unit = unit };
            row.Add(field);
            Content.Add(row);

            var box = new Box(field, read, same ?? SameNumber);
            field.RegisterValueChangedCallback(e =>
            {
                if (box.Showing)
                    return;
                var value = e.newValue;
                if (float.IsNaN(value) || float.IsInfinity(value))
                    return;
                _commands.TransformSelection(verb, p => write(p, value));
            });
            _boxes.Add(box);
        }

        static bool SameNumber(float a, float b) => Math.Abs(a - b) <= Tolerance;

        static bool SameAngle(float a, float b)
        {
            var d = Math.Abs(PlacementMath.NormalizeDegrees(a) - PlacementMath.NormalizeDegrees(b));
            return Math.Min(d, 360f - d) <= 0.01f;
        }

        /// <summary>One number box and how it reads the inspected objects.</summary>
        sealed class Box
        {
            readonly ForgeNumberField _field;
            readonly Func<PlacedObjectRecord, float> _read;
            readonly Func<float, float, bool> _same;

            public Box(ForgeNumberField field, Func<PlacedObjectRecord, float> read, Func<float, float, bool> same)
            {
                _field = field;
                _read = read;
                _same = same;
            }

            /// <summary>True while the box is being given a value by <see cref="Show"/>, not by the user.</summary>
            public bool Showing { get; private set; }

            public void Show(IReadOnlyList<PlacedObjectRecord> placed)
            {
                // The user's typing wins until they commit it.
                if (_field.focusController?.focusedElement is VisualElement focused && (focused == _field || _field.Contains(focused)))
                    return;

                var shared = Inspection.Shared(placed, _read, _same);
                Showing = true;
                try
                {
                    _field.showMixedValue = shared.Mixed;
                    if (!shared.Mixed)
                        _field.SetValueWithoutNotify(shared.Value);
                }
                finally
                {
                    Showing = false;
                }
            }
        }
    }
}
