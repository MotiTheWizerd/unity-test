using System;
using System.Collections.Generic;
using System.Globalization;
using Forge.World.Assets;
using UnityEngine;
using UnityEngine.UIElements;

namespace Forge.Play.Hud
{
    /// <summary>
    /// The player's face, made before play, as Skyrim's race menu: a column of the face's parts on the left (Head, Eyes,
    /// Brows, Nose, Mouth, Jaw, Skin, Hair), the chosen part's sliders and choices beside it, the character standing large
    /// on the right (a <see cref="TurntableView"/>: drag to turn it, the wheel to zoom), and Randomize, Reset and Done
    /// along the bottom. Drawn by FaceEditorScreen.uss. Whatever hosts it ticks it while it is on screen.
    /// </summary>
    // SHORTCUT: the screen alone, its controls change nothing on the character yet (values live in the screen, Done
    // does nothing); the face's rules and its record come next.
    public sealed class FaceEditorScreen : IDisposable
    {
        public const string UssClass = "forge-face";
        public const string TitleClass = UssClass + "__title";
        public const string FrameClass = UssClass + "__frame";
        public const string PartsClass = UssClass + "__parts";
        public const string PartClass = UssClass + "__part";
        public const string PartActiveClass = PartClass + "--active";
        public const string ControlsClass = UssClass + "__controls";
        public const string ControlsTitleClass = UssClass + "__controls-title";
        public const string RowClass = UssClass + "__row";
        public const string RowNameClass = UssClass + "__row-name";
        public const string RowValueClass = UssClass + "__row-value";
        public const string SliderClass = UssClass + "__slider";
        public const string ChoiceClass = UssClass + "__choice";
        public const string ChoiceArrowClass = UssClass + "__choice-arrow";
        public const string ChoiceNameClass = UssClass + "__choice-name";
        public const string CharacterViewClass = UssClass + "__character-view";
        public const string FooterClass = UssClass + "__footer";
        public const string ButtonClass = UssClass + "__button";
        public const string ButtonPrimaryClass = ButtonClass + "--primary";
        public const string HintClass = UssClass + "__hint";

        /// <summary>A slider's middle, where every face starts.</summary>
        public const int Middle = 50;

        /// <summary>One thing to set on a part: a slider from 0 to 100, or one of a few named choices.</summary>
        public sealed class Control
        {
            public Control(string name, params string[] choices)
            {
                Name = name;
                Choices = choices;
            }

            public string Name { get; }

            /// <summary>The names to pick from; empty for a slider.</summary>
            public string[] Choices { get; }

            public bool IsChoice => Choices.Length > 0;
        }

        /// <summary>A part of the face and what can be set on it.</summary>
        public sealed class Part
        {
            public Part(string name, params Control[] controls)
            {
                Name = name;
                Controls = controls;
            }

            public string Name { get; }
            public Control[] Controls { get; }
        }

        /// <summary>The parts, top to bottom, and what each sets.</summary>
        public static readonly IReadOnlyList<Part> Parts = new[]
        {
            new Part("Head", new Control("Width"), new Control("Height"), new Control("Roundness"), new Control("Cheekbones"), new Control("Cheeks")),
            new Part("Eyes", new Control("Colour", "Brown", "Hazel", "Green", "Blue", "Grey"), new Control("Size"), new Control("Spacing"), new Control("Height"), new Control("Tilt"), new Control("Depth")),
            new Part("Brows", new Control("Height"), new Control("Angle"), new Control("Thickness"), new Control("Depth")),
            new Part("Nose", new Control("Length"), new Control("Width"), new Control("Height"), new Control("Bridge"), new Control("Tip")),
            new Part("Mouth", new Control("Width"), new Control("Height"), new Control("Upper Lip"), new Control("Lower Lip"), new Control("Corners")),
            new Part("Jaw", new Control("Width"), new Control("Length"), new Control("Forward"), new Control("Chin"), new Control("Chin Width")),
            new Part("Skin", new Control("Tone"), new Control("Age"), new Control("Freckles"), new Control("Dirt")),
            new Part("Hair", new Control("Style", "None", "Bob"), new Control("Colour"), new Control("Lightness")),
        };

        readonly VisualElement _root;
        readonly List<Label> _partViews = new List<Label>();
        readonly Label _controlsTitle;
        readonly ScrollView _controls;
        readonly TurntableView _characterView;
        readonly Dictionary<(string, string), int> _values = new Dictionary<(string, string), int>();
        readonly System.Random _random = new System.Random();
        Part _part;
        bool _dirty = true;

        /// <param name="parent">Where the screen goes: a full-screen element of the panel.</param>
        /// <param name="character">The character whose face is made, a prefab; null shows none.</param>
        public FaceEditorScreen(VisualElement parent, GameObject character)
        {
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));

            _root = Make<VisualElement>(parent, UssClass);
            _root.name = "face-editor";
            Make<Label>(_root, TitleClass).text = "Your Face";
            var frame = Make<VisualElement>(_root, FrameClass);

            var parts = Make<VisualElement>(frame, PartsClass);
            foreach (var part in Parts)
            {
                var view = Make<Label>(parts, PartClass);
                view.text = part.Name;
                view.RegisterCallback<ClickEvent>(_ => Choose(part.Name));
                _partViews.Add(view);
            }

            var controls = Make<VisualElement>(frame, ControlsClass);
            _controlsTitle = Make<Label>(controls, ControlsTitleClass);
            _controls = new ScrollView(ScrollViewMode.Vertical);
            controls.Add(_controls);

            _characterView = new TurntableView(PreviewTurn.Character());
            _characterView.AddToClassList(CharacterViewClass);
            frame.Add(_characterView);
            if (character != null)
                _characterView.Show(character, holder => CharacterModel.StandingStill(holder, character).Root);

            var footer = Make<VisualElement>(_root, FooterClass);
            Make<Label>(footer, HintClass).text = "Drag: Turn   ·   Wheel: Zoom   ·   Double-click: Face On";
            Button(footer, "Randomize", Randomize);
            Button(footer, "Reset", Reset);
            Button(footer, "Done", () => { }).AddToClassList(ButtonPrimaryClass);

            _part = Parts[0];
        }

        /// <summary>The part whose controls are shown.</summary>
        public string Chosen => _part.Name;

        /// <summary>How many controls the shown part has on screen, once drawn.</summary>
        public int ShownControls => _controls.contentContainer.childCount;

        /// <summary>A control's value: a slider's 0 to 100, a choice's index; <see cref="Middle"/> or the first choice until set.</summary>
        public int Value(string part, string control) =>
            _values.TryGetValue((part, control), out var value) ? value : Start(Find(part, control));

        /// <summary>Shows <paramref name="part"/>'s controls from the next tick.</summary>
        public void Choose(string part)
        {
            foreach (var candidate in Parts)
            {
                if (candidate.Name == part)
                {
                    _part = candidate;
                    _dirty = true;
                    return;
                }
            }

            throw new ArgumentException($"No face part '{part}'.", nameof(part));
        }

        /// <summary>Brings the screen up to date when something changed and turns the character <paramref name="deltaTime"/> seconds' worth.</summary>
        public void Tick(float deltaTime)
        {
            if (_dirty)
                Draw();
            _characterView.Tick(deltaTime);
        }

        public void Dispose()
        {
            _characterView.Dispose();
            _root.RemoveFromHierarchy();
        }

        void Randomize()
        {
            foreach (var part in Parts)
            {
                foreach (var control in part.Controls)
                    _values[(part.Name, control.Name)] = control.IsChoice ? _random.Next(control.Choices.Length) : _random.Next(101);
            }

            _dirty = true;
        }

        void Reset()
        {
            _values.Clear();
            _dirty = true;
        }

        // Drawn at the next tick, never inside the click that asked.
        void Draw()
        {
            _dirty = false;
            for (var i = 0; i < Parts.Count; i++)
                _partViews[i].EnableInClassList(PartActiveClass, Parts[i] == _part);

            _controlsTitle.text = _part.Name.ToUpperInvariant();
            _controls.Clear();
            foreach (var control in _part.Controls)
                _controls.Add(control.IsChoice ? ChoiceRow(_part, control) : SliderRow(_part, control));
        }

        // Its name over a slider, the value beside the name.
        VisualElement SliderRow(Part part, Control control)
        {
            var row = Row(control.Name, out var value);
            value.text = Number(Value(part.Name, control.Name));
            var slider = new SliderInt(0, 100) { value = Value(part.Name, control.Name) };
            slider.AddToClassList(SliderClass);
            slider.RegisterValueChangedCallback(e =>
            {
                _values[(part.Name, control.Name)] = e.newValue;
                value.text = Number(e.newValue);
            });
            row.Add(slider);
            return row;
        }

        // Its name over the choice between two arrows that step through the choices, round.
        VisualElement ChoiceRow(Part part, Control control)
        {
            var row = Row(control.Name, out var count);
            var choice = Make<VisualElement>(row, ChoiceClass);
            var name = new Label();
            name.AddToClassList(ChoiceNameClass);

            void Show()
            {
                var index = Value(part.Name, control.Name);
                name.text = control.Choices[index];
                count.text = $"{index + 1} / {control.Choices.Length}";
            }

            void Step(int by)
            {
                var length = control.Choices.Length;
                _values[(part.Name, control.Name)] = (Value(part.Name, control.Name) + by + length) % length;
                Show();
            }

            Button(choice, "‹", () => Step(-1)).AddToClassList(ChoiceArrowClass);
            choice.Add(name);
            Button(choice, "›", () => Step(1)).AddToClassList(ChoiceArrowClass);
            Show();
            return row;
        }

        VisualElement Row(string name, out Label value)
        {
            var row = new VisualElement();
            row.AddToClassList(RowClass);
            var head = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween } };
            row.Add(head);
            Make<Label>(head, RowNameClass).text = name;
            value = Make<Label>(head, RowValueClass);
            return row;
        }

        static Control Find(string part, string control)
        {
            foreach (var candidate in Parts)
            {
                if (candidate.Name != part)
                    continue;
                foreach (var c in candidate.Controls)
                {
                    if (c.Name == control)
                        return c;
                }
            }

            throw new ArgumentException($"No control '{control}' on face part '{part}'.", nameof(control));
        }

        static int Start(Control control) => control.IsChoice ? 0 : Middle;

        static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

        static Button Button(VisualElement parent, string text, Action clicked)
        {
            var button = new Button(clicked) { text = text };
            button.AddToClassList(ButtonClass);
            parent.Add(button);
            return button;
        }

        static T Make<T>(VisualElement parent, string ussClass) where T : VisualElement, new()
        {
            var part = new T();
            part.AddToClassList(ussClass);
            parent.Add(part);
            return part;
        }
    }
}
