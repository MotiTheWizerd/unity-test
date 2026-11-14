using System;
using System.Collections.Generic;
using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// A character's eyes blinking: now and then (<see cref="LeastGap"/> to <see cref="MostGap"/> seconds apart, by
    /// chance, now and then twice running, <see cref="TwiceChance"/>), its lids come down and up again
    /// (<see cref="ShutAt"/>: quick to shut, a moment shut, slower to open), drawn by its head's own
    /// <see cref="Shape"/> blend shape (blender-tests eyes.py: the lids turned about each eyeball's middle until they
    /// meet), on each frame's pose (<see cref="CharacterModel.Posed"/>). The head is the worn one
    /// (<see cref="CharacterHeads"/>, found again when another is put on), else whatever it draws with that shape; a
    /// character without one doesn't blink, nor one that isn't awake (a body, dead). Bound while the look exists.
    /// </summary>
    public sealed class EyeBlink : IDisposable
    {
        /// <summary>The blend shape that shuts the eyes, 0 open to 100 shut.</summary>
        public const string Shape = "Blink";

        /// <summary>Seconds the lids take to come down.</summary>
        public const float Closing = 0.07f;

        /// <summary>Seconds they stay down.</summary>
        public const float Held = 0.04f;

        /// <summary>Seconds they take to open again.</summary>
        public const float Opening = 0.13f;

        /// <summary>Seconds from one blink to the next, at least and at most.</summary>
        public const float LeastGap = 2f;

        public const float MostGap = 6f;

        /// <summary>How often a blink comes again at once, a share of them.</summary>
        public const float TwiceChance = 0.12f;

        /// <summary>Seconds between the two of a double blink.</summary>
        public const float TwiceGap = 0.12f;

        /// <summary>A blink, from its start to its end.</summary>
        public const float Length = Closing + Held + Opening;

        readonly CharacterModel _model;
        readonly CharacterHeads _heads;
        readonly System.Random _random;
        readonly Func<bool> _awake;
        readonly List<(SkinnedMeshRenderer Renderer, int Index)> _lids = new List<(SkinnedMeshRenderer, int)>();
        int _headWorn = -2;
        // Seconds to the next blink's start; into the one under way, while it lasts.
        float _wait;
        float _into = -1f;

        /// <param name="seed">Its chances' seed (each character its own, so they don't blink together).</param>
        /// <param name="awake">Whether it blinks now (null: always); not, its eyes stay open as they are.</param>
        public EyeBlink(CharacterModel model, int seed, Func<bool> awake = null)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _awake = awake;
            _heads = model.Root.GetComponentInChildren<CharacterHeads>(true);
            _random = new System.Random(seed);
            _wait = Gap();
            _model.Posed += OnPosed;
        }

        /// <summary>How shut its eyes are now: 0 open, 1 shut.</summary>
        public float Shut { get; private set; }

        /// <summary>The renderers it shuts the eyes of (the worn head's); none: it doesn't blink.</summary>
        public int LidCount
        {
            get
            {
                FindLids();
                return _lids.Count;
            }
        }

        /// <summary>How shut the eyes are <paramref name="seconds"/> into a blink: 0 at its start and from its end on, 1 held shut.</summary>
        public static float ShutAt(float seconds)
        {
            if (seconds <= 0f || seconds >= Length)
                return 0f;
            if (seconds < Closing)
                return Mathf.SmoothStep(0f, 1f, seconds / Closing);
            if (seconds < Closing + Held)
                return 1f;
            return Mathf.SmoothStep(1f, 0f, (seconds - Closing - Held) / Opening);
        }

        public void Dispose()
        {
            _model.Posed -= OnPosed;
            if (_model.Root != null)
                Show(0f);
        }

        void OnPosed() => Blink(Time.deltaTime);

        /// <summary>Lets <paramref name="deltaTime"/> seconds pass, blinking when it is time, and shows the lids.</summary>
        public void Blink(float deltaTime)
        {
            if (_awake != null && !_awake())
            {
                _into = -1f;
                Shut = 0f;
                Show(0f);
                return;
            }

            if (_into >= 0f)
            {
                _into += deltaTime;
                if (_into >= Length)
                {
                    _into = -1f;
                    _wait = _random.NextDouble() < TwiceChance ? TwiceGap : Gap();
                }
            }
            else
            {
                _wait -= deltaTime;
                if (_wait <= 0f)
                    _into = -_wait;
            }

            Shut = _into >= 0f ? ShutAt(_into) : 0f;
            Show(Shut);
        }

        float Gap() => LeastGap + (float)_random.NextDouble() * (MostGap - LeastGap);

        void Show(float shut)
        {
            FindLids();
            foreach (var (renderer, index) in _lids)
                renderer.SetBlendShapeWeight(index, shut * 100f);
        }

        // The worn head's renderers with the shape: a head put on brings its own.
        void FindLids()
        {
            var worn = _heads != null ? _heads.Worn : -1;
            if (worn == _headWorn && _lids.TrueForAll(l => l.Renderer != null))
                return;
            _headWorn = worn;
            _lids.Clear();
            IEnumerable<SkinnedMeshRenderer> drawn = _heads != null
                ? (IEnumerable<SkinnedMeshRenderer>)_heads.WornRenderers
                : _model.Root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var renderer in drawn)
            {
                var index = renderer != null && renderer.sharedMesh != null ? renderer.sharedMesh.GetBlendShapeIndex(Shape) : -1;
                if (index >= 0)
                    _lids.Add((renderer, index));
            }
        }
    }
}
