using System;
using Forge.EditorApp.UI.Controls;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI
{
    /// <summary>
    /// Playing the world from the editor: the top bar's play button starts it (<see cref="EditorContext.Play"/>). While
    /// playing, the layout root wears <see cref="PlayingClass"/>, which hides the editor's panels (every
    /// <see cref="ChromeClass"/> element) and shows the play bar instead (EditorPanel.uss), so the world fills the screen;
    /// popups close and typing ends as play begins. Esc goes back (the controller owns the key).
    /// </summary>
    public sealed class PlaySection : IPanelSection
    {
        public const string PlayingClass = "is-playing";

        /// <summary>Marks the editor's own panels, which make way while playing.</summary>
        public const string ChromeClass = "forge-editor-chrome";

        readonly VisualElement _root;
        readonly VisualElement _layout;
        readonly EditorContext _context;
        bool _playing;

        public PlaySection(VisualElement root, EditorContext context)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _layout = PanelElements.Required<VisualElement>(root, "root");
            PanelElements.Required<ForgeButton>(root, "play").clicked += () => _context.Play();
        }

        public void Refresh()
        {
            var playing = _context.IsPlaying;
            if (playing == _playing)
                return;

            _playing = playing;
            _layout.EnableInClassList(PlayingClass, playing);
            if (!playing)
                return;
            ForgePopup.CloseAll(_root);
            _root.panel?.focusController?.focusedElement?.Blur();
        }
    }
}
