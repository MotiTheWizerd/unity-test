using System;
using Forge.Play;
using Forge.Play.Hud;
using UnityEngine;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI
{
    /// <summary>
    /// Hosts the game's HUD (<see cref="PlayerHud"/>) in the editor's panel, so playing from here shows what the game
    /// will: the HUD itself is the game's, and shows only while a player is spawned.
    /// </summary>
    public sealed class HudSection : IPanelSection
    {
        readonly PlayerHud _hud;

        public HudSection(VisualElement root, PlayerSession player)
        {
            if (player == null)
                throw new ArgumentNullException(nameof(player));
            _hud = new PlayerHud(player, PanelElements.Required<VisualElement>(root, "root"));
        }

        // The panel lives as long as the app, and the HUD with it; nothing to let go of sooner.
        public void Refresh() => _hud.Tick(Time.deltaTime);
    }
}
