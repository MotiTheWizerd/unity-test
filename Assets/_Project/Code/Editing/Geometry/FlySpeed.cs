using System;
using System.Collections.Generic;
using System.Globalization;

namespace Forge.Editing.Geometry
{
    /// <summary>
    /// How fast the editor camera flies, as a multiplier of its base speed: picked from <see cref="Presets"/> or
    /// stepped through <see cref="Steps"/> with the mouse wheel while flying, and optionally scaled with the camera's
    /// height above the ground (<see cref="FollowsHeight"/>), so crossing a big map from high up is quick while
    /// skimming the ground stays precise. Shared by the camera and the UI; engine-free.
    /// </summary>
    public sealed class FlySpeed
    {
        /// <summary>Every multiplier the wheel steps through, slowest first.</summary>
        public static readonly IReadOnlyList<float> Steps = new[] { 0.25f, 0.5f, 1f, 2f, 3f, 5f, 8f, 12f, 20f };

        /// <summary>The multipliers offered as buttons.</summary>
        public static readonly IReadOnlyList<float> Presets = new[] { 1f, 2f, 3f, 5f };

        /// <summary>Height above the ground, in meters, up to which height doesn't speed the camera up.</summary>
        public const float ReferenceHeight = 25f;

        /// <summary>Most the height can multiply the speed by.</summary>
        public const float MaxHeightFactor = 12f;

        const int NormalStep = 2;

        int _step = NormalStep;
        bool _followsHeight;

        /// <summary>Raised when the multiplier or <see cref="FollowsHeight"/> changes.</summary>
        public event Action Changed;

        /// <summary>The chosen multiplier, one of <see cref="Steps"/>.</summary>
        public float Multiplier => Steps[_step];

        /// <summary>Whether the camera speeds up with its height above the ground (<see cref="HeightFactor"/>).</summary>
        public bool FollowsHeight
        {
            get => _followsHeight;
            set
            {
                if (_followsHeight == value)
                    return;
                _followsHeight = value;
                Changed?.Invoke();
            }
        }

        /// <summary>Picks the step nearest to <paramref name="multiplier"/>.</summary>
        public void Set(float multiplier)
        {
            if (!(multiplier > 0f))
                throw new ArgumentOutOfRangeException(nameof(multiplier), multiplier, "The multiplier must be positive.");

            var nearest = 0;
            for (var i = 1; i < Steps.Count; i++)
            {
                if (Math.Abs(Math.Log(Steps[i] / multiplier)) < Math.Abs(Math.Log(Steps[nearest] / multiplier)))
                    nearest = i;
            }

            SetStep(nearest);
        }

        /// <summary>Moves <paramref name="notches"/> steps faster (negative: slower), stopping at either end.</summary>
        public void Step(int notches) => SetStep(Math.Max(0, Math.Min(Steps.Count - 1, _step + notches)));

        /// <summary>
        /// The speed multiplier to fly with at <paramref name="heightAboveGround"/> meters (null when unknown):
        /// <see cref="Multiplier"/>, times <see cref="HeightFactor"/> when <see cref="FollowsHeight"/> is on.
        /// </summary>
        public float FactorAt(float? heightAboveGround) =>
            Multiplier * (FollowsHeight && heightAboveGround.HasValue ? HeightFactor(heightAboveGround.Value) : 1f);

        /// <summary>
        /// 1 up to <see cref="ReferenceHeight"/>, then in proportion to the height (twice as high, twice as fast,
        /// so the ground seems to pass at the same pace), up to <see cref="MaxHeightFactor"/>.
        /// </summary>
        public static float HeightFactor(float heightAboveGround)
        {
            if (float.IsNaN(heightAboveGround) || heightAboveGround <= ReferenceHeight)
                return 1f;
            return Math.Min(MaxHeightFactor, heightAboveGround / ReferenceHeight);
        }

        /// <summary>"×2", "×0.5", as shown to the user.</summary>
        public static string Label(float multiplier) => "×" + multiplier.ToString("0.##", CultureInfo.InvariantCulture);

        void SetStep(int step)
        {
            if (step == _step)
                return;
            _step = step;
            Changed?.Invoke();
        }
    }
}
