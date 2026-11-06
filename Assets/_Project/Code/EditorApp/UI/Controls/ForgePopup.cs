using System;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI.Controls
{
    /// <summary>
    /// A large <see cref="ForgePanel"/> card centred over the editor, for content that needs room (settings and the
    /// like). UXML children are its content, as in a panel; give it a ScrollView where content may outgrow it, so
    /// whatever sits above (tabs, a header) stays put. Hidden until
    /// <see cref="Open"/>; a press anywhere else in the panel closes it, except on the element that opened it, which
    /// toggles it itself. Put it at the root of the document, so no panel clips it. Presses on the world never reach
    /// the panel, so the host closes popups on a world click (and on Esc) with <see cref="CloseAll"/>. Its size is
    /// set by tokens (ForgePopup.uss).
    /// </summary>
    [UxmlElement]
    public partial class ForgePopup : ForgePanel
    {
        public const string PopupClass = "forge-popup";

        VisualElement _opener;
        VisualElement _listening;
        bool _open;

        public ForgePopup()
        {
            AddToClassList(PopupClass);
            style.display = DisplayStyle.None;
            RegisterCallback<DetachFromPanelEvent>(_ => Close());
        }

        public bool IsOpen => _open;

        /// <summary>Raised with true on opening and false on closing.</summary>
        public event Action<bool> OpenChanged;

        /// <param name="opener">The element that toggles it (a button); presses on it don't count as outside.</param>
        public void Toggle(VisualElement opener = null)
        {
            if (_open)
                Close();
            else
                Open(opener);
        }

        /// <param name="opener">The element that toggles it (a button); presses on it don't count as outside.</param>
        public void Open(VisualElement opener = null)
        {
            _opener = opener;
            if (_open)
                return;

            _open = true;
            style.display = DisplayStyle.Flex;
            BringToFront();
            _listening = panel?.visualTree;
            _listening?.RegisterCallback<PointerDownEvent>(OnPressAnywhere, TrickleDown.TrickleDown);
            OpenChanged?.Invoke(true);
        }

        public void Close()
        {
            if (!_open)
                return;
            _open = false;
            style.display = DisplayStyle.None;
            _listening?.UnregisterCallback<PointerDownEvent>(OnPressAnywhere, TrickleDown.TrickleDown);
            _listening = null;
            OpenChanged?.Invoke(false);
        }

        /// <summary>Closes every open popup under <paramref name="root"/>.</summary>
        public static void CloseAll(VisualElement root) => root?.Query<ForgePopup>().ForEach(popup => popup.Close());

        void OnPressAnywhere(PointerDownEvent evt)
        {
            if (evt.target is VisualElement target && (Holds(this, target) || Holds(_opener, target)))
                return;
            Close();
        }

        static bool Holds(VisualElement element, VisualElement target) =>
            element != null && (element == target || element.Contains(target));
    }
}
