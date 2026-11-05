using System;
using System.Collections.Generic;
using Forge.EditorApp.UI.Controls;
using UnityEngine;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI.Gallery
{
    /// <summary>
    /// The UI gallery: a page showing every Forge component in every state (UI/Gallery/UiGallery.uxml), for judging
    /// the look in one place. Opened by Forge → UI Gallery. This fills what markup alone cannot: tooltips, a tab bar,
    /// tiles, a dropdown's choices and the disabled group.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class UiGallery : MonoBehaviour
    {
        [NonSerialized] TooltipLayer _tooltips;

        void Start()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            _tooltips = new TooltipLayer(root);

            root.Q("disabled")?.SetEnabled(false);

            var dropdown = root.Q<DropdownField>("dropdown");
            if (dropdown != null)
            {
                dropdown.choices = new List<string> { "Temperate", "Northern", "Desert" };
                dropdown.index = 0;
            }

            var tabs = new TabBar();
            tabs.SetTabs(new[] { ("architecture", "Architecture"), ("nature", "Nature"), ("props", "Props") });
            root.Q("tabs")?.Add(tabs);

            var tiles = root.Q("tiles");
            if (tiles != null)
            {
                tiles.Add(new AssetTile("Pine Tree", 1, () => { }) { tooltip = "Pine Tree  <color=#9EA4AE>1 · Nature / Trees</color>" });
                var chosen = new AssetTile("A House With A Long Name", 2, () => { }) { tooltip = "The chosen one" };
                chosen.AddToClassList(PanelElements.ActiveClass);
                tiles.Add(chosen);
                tiles.Add(new AssetTile("Rock", 0, () => { }));
            }
        }
    }
}
