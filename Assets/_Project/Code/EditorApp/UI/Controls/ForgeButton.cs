using System;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI.Controls
{
    /// <summary>
    /// Forge's button: text, an <see cref="icon"/>, or both, in one of a few <see cref="variant"/>s. Styled by
    /// ForgeButton.uss on top of the themed Unity button; the chosen one gets the accent through .is-active.
    /// </summary>
    /// <remarks>
    /// The icon is the button's own background image (the .forge-icon--{name} class), not a child element: a button
    /// with children is no longer measured by its text, so its text would get no room.
    /// </remarks>
    [UxmlElement]
    public partial class ForgeButton : Button
    {
        public const string Class = "forge-button";
        public const string IconOnlyClass = "forge-button--icon-only";
        public const string WithIconClass = "forge-button--with-icon";
        public const string SmallClass = "forge-button--small";
        public const string BusyClass = "forge-button--busy";
        public const string PulseClass = "forge-button--pulse";

        // Used until the sheet's pulse transition has resolved (ForgeTokens --forge-motion-pulse is the real source).
        const long FallbackPulseMilliseconds = 800;

        string _iconName = string.Empty;
        bool _small;
        bool _busy;
        IVisualElementScheduledItem _pulse;
        ForgeButtonVariant _variant = ForgeButtonVariant.Default;

        public ForgeButton() : this(null)
        {
        }

        public ForgeButton(Action clickEvent) : base(clickEvent)
        {
            AddToClassList(Class);
            AddToClassList(VariantClass(_variant));
            // The text may be set after the icon (UXML applies attributes in its own order), so read both on arrival.
            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                UpdateIconClasses();
                UpdatePulse();
            });
            RegisterCallback<DetachFromPanelEvent>(_ => StopPulse());
        }

        /// <summary>An icon name from the set (UI/Icons); empty for none. Sits left of the text, or alone.</summary>
        [UxmlAttribute]
        public string icon
        {
            get => _iconName;
            set
            {
                var next = value ?? string.Empty;
                if (_iconName == next)
                    return;
                Icons.Set(this, _iconName, next);
                _iconName = next;
                UpdateIconClasses();
            }
        }

        [UxmlAttribute]
        public ForgeButtonVariant variant
        {
            get => _variant;
            set
            {
                RemoveFromClassList(VariantClass(_variant));
                _variant = value;
                AddToClassList(VariantClass(_variant));
            }
        }

        /// <summary>A smaller box and icon, for toolbars.</summary>
        [UxmlAttribute]
        public bool small
        {
            get => _small;
            set
            {
                _small = value;
                EnableInClassList(SmallClass, value);
            }
        }

        /// <summary>
        /// Something this button stands for is at work (an agent thinking, a job running): it pulses until cleared.
        /// UI Toolkit has no keyframes, so the button flips <see cref="PulseClass"/> on a timer and ForgeButton.uss
        /// fades between the two looks; the timer's period is that transition's duration, so the tokens decide both.
        /// </summary>
        [UxmlAttribute]
        public bool busy
        {
            get => _busy;
            set
            {
                if (_busy == value)
                    return;
                _busy = value;
                EnableInClassList(BusyClass, value);
                UpdatePulse();
            }
        }

        void UpdatePulse()
        {
            if (!_busy || panel == null)
            {
                StopPulse();
                return;
            }

            if (_pulse == null)
                SchedulePulse();
        }

        // One beat at a time, each reading the period afresh: the busy class's own transition resolves a frame late.
        void SchedulePulse() => _pulse = schedule.Execute(() =>
        {
            ToggleInClassList(PulseClass);
            SchedulePulse();
        }).StartingIn(PulseMilliseconds());

        void StopPulse()
        {
            _pulse?.Pause();
            _pulse = null;
            RemoveFromClassList(PulseClass);
        }

        long PulseMilliseconds()
        {
            foreach (var duration in resolvedStyle.transitionDuration)
            {
                var ms = duration.unit == TimeUnit.Second ? duration.value * 1000f : duration.value;
                if (ms > 0f)
                    return (long)ms;
            }

            return FallbackPulseMilliseconds;
        }

        void UpdateIconClasses()
        {
            var hasIcon = _iconName.Length > 0;
            var hasText = !string.IsNullOrEmpty(text);
            EnableInClassList(IconOnlyClass, hasIcon && !hasText);
            EnableInClassList(WithIconClass, hasIcon && hasText);
        }

        static string VariantClass(ForgeButtonVariant variant) => $"{Class}--{variant.ToString().ToLowerInvariant()}";
    }

    public enum ForgeButtonVariant
    {
        /// <summary>A quiet filled button.</summary>
        Default,

        /// <summary>The one action of a form, on the accent.</summary>
        Primary,

        /// <summary>No fill until hovered: toolbars, title bars.</summary>
        Ghost,
    }
}
