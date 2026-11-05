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

        string _iconName = string.Empty;
        ForgeButtonVariant _variant = ForgeButtonVariant.Default;

        public ForgeButton() : this(null)
        {
        }

        public ForgeButton(Action clickEvent) : base(clickEvent)
        {
            AddToClassList(Class);
            // The text may be set after the icon (UXML applies attributes in its own order), so read both on arrival.
            RegisterCallback<AttachToPanelEvent>(_ => UpdateIconClasses());
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
