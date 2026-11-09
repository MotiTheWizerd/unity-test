using UnityEngine.UIElements;

namespace Forge.Play.Hud
{
    /// <summary>
    /// The middle of the view: a faint dot, lit while something in sight can be used. While a bow is drawn it becomes a
    /// target cross, four ticks round the dot that close in as the string is pulled and light up at full draw. Drawn by
    /// PlayerHud.uss (the ticks' size and look); only how far out the ticks sit is set here.
    /// </summary>
    public sealed class Crosshair : VisualElement
    {
        public const string UssClass = "forge-hud-crosshair";
        public const string DotClass = UssClass + "__dot";
        public const string TickClass = UssClass + "__tick";
        public const string OnClass = UssClass + "--on";
        public const string AimClass = UssClass + "--aim";
        public const string FullDrawClass = UssClass + "--full";

        /// <summary>Pixels from the middle to the ticks with the arrow just on the string...</summary>
        public const float LooseSpread = 16f;

        /// <summary>...and at full draw.</summary>
        public const float DrawnSpread = 5f;

        readonly VisualElement _up;
        readonly VisualElement _down;
        readonly VisualElement _left;
        readonly VisualElement _right;
        float _spread = float.NaN;

        public Crosshair()
        {
            AddToClassList(UssClass);
            pickingMode = PickingMode.Ignore;
            Part(DotClass);
            _up = Part(TickClass, "up");
            _down = Part(TickClass, "down");
            _left = Part(TickClass, "left");
            _right = Part(TickClass, "right");
        }

        /// <summary>
        /// Draws it lit or not (<paramref name="on"/>: something in sight can be used), and as a target cross while
        /// <paramref name="aiming"/>, a bow drawn <paramref name="pull"/> of the way (0 to 1).
        /// </summary>
        public void Draw(bool on, bool aiming, float pull)
        {
            EnableInClassList(OnClass, on);
            EnableInClassList(AimClass, aiming);
            EnableInClassList(FullDrawClass, aiming && pull >= 1f);
            if (aiming)
                Spread(LooseSpread + (DrawnSpread - LooseSpread) * (pull > 0f ? (pull < 1f ? pull : 1f) : 0f));
        }

        // The ticks sit `pixels` out from the middle, each along its own way; set only when it changes.
        void Spread(float pixels)
        {
            if (pixels == _spread)
                return;
            _spread = pixels;
            _up.style.top = -pixels;
            _down.style.top = pixels;
            _left.style.left = -pixels;
            _right.style.left = pixels;
        }

        VisualElement Part(string ussClass, string way = null)
        {
            var part = new VisualElement { pickingMode = PickingMode.Ignore };
            part.AddToClassList(ussClass);
            if (way != null)
                part.AddToClassList(ussClass + "--" + way);
            Add(part);
            return part;
        }
    }
}
