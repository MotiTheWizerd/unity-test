using System;
using Forge.Play;
using Forge.Play.Hud;
using UnityEngine;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI
{
    /// <summary>
    /// Hosts the game's HUD (<see cref="PlayerHud"/>) and its inventory screen (<see cref="InventoryScreen"/>) in the
    /// editor's panel, so playing from here shows what the game will: both are the game's, and show only while a player
    /// is spawned.
    /// </summary>
    public sealed class HudSection : IPanelSection
    {
        readonly PlayerHud _hud;
        readonly InventoryScreen _inventory;

        public HudSection(VisualElement root, GameSession game)
        {
            if (game == null)
                throw new ArgumentNullException(nameof(game));
            var parent = PanelElements.Required<VisualElement>(root, "root");
            _hud = new PlayerHud(game.Player, parent);
            _inventory = new InventoryScreen(game, parent);
        }

        // The panel lives as long as the app, and the HUD with it; nothing to let go of sooner.
        public void Refresh()
        {
            _hud.Tick(Time.deltaTime);
            _inventory.Tick(Time.deltaTime);
        }
    }
}
