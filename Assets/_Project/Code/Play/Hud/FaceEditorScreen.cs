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
    /// Brows, Nose, Mouth, Jaw, Skin, Hair; on a body with heads to choose from, the Head part's Face first:
    /// <see cref="PartsFor"/>), the chosen part's sliders and choices beside it, the character standing large
    /// on the right (a <see cref="TurntableView"/>: drag to turn it, the wheel to zoom), and Randomize, Reset and Done
    /// along the bottom; each change shows on the head at once (<see cref="FaceLook"/>). Drawn by FaceEditorScreen.uss. Whatever hosts it ticks it while it is on screen.
    /// </summary>
    // SHORTCUT: the face lives in the screen (no record, Done does nothing), and only the controls the head has a shape
    // for change it (FaceLook); the face's record and the rest of its shapes come next.
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
            /// <summary>A slider starting at <see cref="Middle"/>, or, given choices, a choice starting at the first.</summary>
            public Control(string name, params string[] choices)
                : this(name, choices.Length > 0 ? 0 : Middle, choices)
            {
            }

            /// <summary>A choice starting at choice <paramref name="start"/>.</summary>
            public Control(string name, int start, string[] choices)
            {
                Name = name;
                Choices = choices;
                Start = start;
            }

            public string Name { get; }

            /// <summary>The names to pick from; empty for a slider.</summary>
            public string[] Choices { get; }

            public bool IsChoice => Choices.Length > 0;

            /// <summary>Its value until set, and again on Reset: a slider's 0 to 100, a choice's index.</summary>
            public int Start { get; }
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

        /// <summary>The parts, top to bottom, and what each sets on every face.</summary>
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

        /// <summary>
        /// The parts made on <paramref name="character"/>: <see cref="Parts"/>, and when its body can wear more than one
        /// head (<see cref="CharacterHeads"/>), the head's Face first, a choice of them starting at the one it wears.
        /// </summary>
        public static IReadOnlyList<Part> PartsFor(GameObject character)
        {
            var heads = character != null ? character.GetComponentInChildren<CharacterHeads>(true) : null;
            if (heads == null || heads.Count < 2)
                return Parts;

            var names = new string[heads.Count];
            for (var i = 0; i < names.Length; i++)
                names[i] = heads.NameOf(i);
            var parts = new List<Part>(Parts);
            var head = parts.FindIndex(p => p.Name == FaceLook.HeadPart);
            var controls = new List<Control> { new Control(FaceLook.FaceControl, Math.Max(heads.Worn, 0), names) };
            controls.AddRange(parts[head].Controls);
            parts[head] = new Part(FaceLook.HeadPart, controls.ToArray());
            return parts;
        }

        readonly IReadOnlyList<Part> _parts;
        readonly VisualElement _root;
        readonly List<Label> _partViews = new List<Label>();
        readonly Label _controlsTitle;
        readonly ScrollView _controls;
        readonly TurntableView _characterView;
        readonly Dictionary<(string, string), int> _values = new Dictionary<(string, string), int>();
        readonly System.Random _random = new System.Random();
        Part _part;
        GameObject _look;
        bool _dirty = true;

        /// <param name="parent">Where the screen goes: a full-screen element of the panel.</param>
        /// <param name="character">Whose face is made: a character prefab, or a head alone (Content/Heads); null shows none.</param>
        public FaceEditorScreen(VisualElement parent, GameObject character)
        {
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));

            _parts = PartsFor(character);
            _root = Make<VisualElement>(parent, UssClass);
            _root.name = "face-editor";
            Make<Label>(_root, TitleClass).text = "Your Face";
            var frame = Make<VisualElement>(_root, FrameClass);

            var parts = Make<VisualElement>(frame, PartsClass);
            foreach (var part in _parts)
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
                _characterView.Show(character, holder => _look = Build(holder, character));

            var footer = Make<VisualElement>(_root, FooterClass);
            Make<Label>(footer, HintClass).text = "Drag: Turn   ·   Wheel: Zoom   ·   Double-click: Face On";
            Button(footer, "Randomize", Randomize);
            Button(footer, "Reset", Reset);
            Button(footer, "Done", () => { }).AddToClassList(ButtonPrimaryClass);

            _part = _parts[0];
        }

        /// <summary>The part whose controls are shown.</summary>
        public string Chosen => _part.Name;

        /// <summary>How many controls the shown part has on screen, once drawn.</summary>
        public int ShownControls => _controls.contentContainer.childCount;

        /// <summary>A control's value: a slider's 0 to 100, a choice's index; its <see cref="Control.Start"/> until set.</summary>
        public int Value(string part, string control) =>
            _values.TryGetValue((part, control), out var value) ? value : Find(part, control).Start;

        /// <summary>Shows <paramref name="part"/>'s controls from the next tick.</summary>
        public void Choose(string part)
        {
            foreach (var candidate in _parts)
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
            foreach (var part in _parts)
            {
                foreach (var control in part.Controls)
                    _values[(part.Name, control.Name)] = control.IsChoice ? _random.Next(control.Choices.Length) : _random.Next(101);
            }

            ShowAll();

            _dirty = true;
        }

        void Reset()
        {
            _values.Clear();
            ShowAll();
            _dirty = true;
        }

        // Every control's value on the head: the head chosen first (the Face leads the Head part, the first), so the rest
        // shape the head it wears.
        void ShowAll()
        {
            foreach (var part in _parts)
            {
                foreach (var control in part.Controls)
                    FaceLook.Apply(_look, part.Name, control.Name, Value(part.Name, control.Name));
            }
        }

        void Set(Part part, Control control, int value)
        {
            _values[(part.Name, control.Name)] = value;
            // Another head takes every value the face has.
            if (part.Name == FaceLook.HeadPart && control.Name == FaceLook.FaceControl)
                ShowAll();
            else
                FaceLook.Apply(_look, part.Name, control.Name, value);
        }

        // Drawn at the next tick, never inside the click that asked.
        void Draw()
        {
            _dirty = false;
            for (var i = 0; i < _parts.Count; i++)
                _partViews[i].EnableInClassList(PartActiveClass, _parts[i] == _part);

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
                Set(part, control, e.newValue);
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
                Set(part, control, (Value(part.Name, control.Name) + by + length) % length);
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

        Control Find(string part, string control)
        {
            foreach (var candidate in _parts)
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

        // A rigged character stands still in its idle; a head alone (a bust, no rig) is shown as it is.
        static GameObject Build(Transform holder, GameObject prefab) =>
            prefab.GetComponentInChildren<Animator>(true) != null
                ? CharacterModel.StandingStill(holder, prefab).Root
                : UnityEngine.Object.Instantiate(prefab, holder, false);

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
