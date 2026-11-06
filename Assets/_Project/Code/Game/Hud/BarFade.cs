using System;

namespace Forge.Game.Hud
{
    /// <summary>
    /// When a HUD bar shows, as Skyrim's do: always while not full, then for a moment after it fills or changes, then it
    /// fades out. A bar can be made to blink (<see cref="Flash"/>), when what it holds was too little for something, and
    /// it shows while it blinks. Engine-free; the HUD ticks it and draws <see cref="Opacity"/> and <see cref="FlashOn"/>.
    /// </summary>
    public sealed class BarFade
    {
        /// <summary>Seconds a full bar stays before fading.</summary>
        public const float Linger = 2.5f;

        /// <summary>Seconds a fade takes.</summary>
        public const float FadeTime = 0.6f;

        /// <summary>Seconds a flash lasts.</summary>
        public const float FlashTime = 0.9f;

        /// <summary>Seconds each blink of a flash lasts, on or off.</summary>
        public const float BlinkTime = 0.15f;

        float _sinceFull;
        float _flashLeft;

        /// <summary>A bar not yet seen starts hidden when full.</summary>
        public BarFade() => _sinceFull = Linger + FadeTime;

        /// <summary>How much the bar shows, 0 (hidden) to 1.</summary>
        public float Opacity { get; private set; }

        /// <summary>Whether a flash is on this moment (a blink lit).</summary>
        public bool FlashOn => _flashLeft > 0f && (int)Math.Floor((FlashTime - _flashLeft) / BlinkTime) % 2 == 0;

        /// <summary>Follows the bar for <paramref name="deltaTime"/> seconds, holding <paramref name="fraction"/> of its most.</summary>
        public void Tick(float fraction, float deltaTime)
        {
            if (deltaTime < 0f || float.IsNaN(deltaTime))
                deltaTime = 0f;
            _flashLeft = Math.Max(0f, _flashLeft - deltaTime);
            if (fraction < 1f || _flashLeft > 0f)
                _sinceFull = 0f;
            else
                _sinceFull += deltaTime;

            Opacity = _sinceFull <= Linger ? 1f : Math.Max(0f, 1f - (_sinceFull - Linger) / FadeTime);
        }

        /// <summary>What the bar holds changed: it shows again for a while, full or not.</summary>
        public void Show()
        {
            _sinceFull = 0f;
            Opacity = 1f;
        }

        /// <summary>What the bar holds was too little: it blinks for <see cref="FlashTime"/>, showing.</summary>
        public void Flash()
        {
            _flashLeft = FlashTime;
            Show();
        }
    }
}
