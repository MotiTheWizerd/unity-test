using System;
using System.Collections.Generic;
using System.Linq;
using Forge.Data;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Game.Items;
using Forge.World.Assets;
using UnityEngine;
using UnityEngine.UIElements;

namespace Forge.Play.Hud
{
    /// <summary>
    /// The player's inventory while it is open (<see cref="PlayerSession.InventoryOpen"/>), as Skyrim's item menu: the
    /// player's character standing full length on the far left (a <see cref="TurntableView"/> that a drag spins), a tab
    /// per kind of item beside it (All first; a new <see cref="ItemKind"/> gets its own), what the player carries in
    /// that tab (<see cref="InventoryList"/>), each by its picture (<see cref="IAssetThumbnails"/>) and name, the chosen item turning large in 3D (a <see cref="TurntableView"/>:
    /// drag to turn it, the wheel to zoom) over its card with a Drop button, and along the bottom what it all weighs
    /// against the carry weight, red when overburdened. Drawn by InventoryScreen.uss. The list is read again only when the
    /// inventory changes, the tab does, or the screen opens; the character and the item are drawn only while the screen
    /// is open. Whatever
    /// plays the world hosts it beside the HUD.
    /// </summary>
    public sealed class InventoryScreen : IDisposable
    {
        public const string UssClass = "forge-inventory";
        public const string FrameClass = UssClass + "__frame";
        public const string CharacterViewClass = UssClass + "__character-view";
        public const string TabsClass = UssClass + "__tabs";
        public const string TabClass = UssClass + "__tab";
        public const string TabActiveClass = TabClass + "--active";
        public const string ListClass = UssClass + "__list";
        public const string RowClass = UssClass + "__row";
        public const string RowSelectedClass = RowClass + "--selected";
        public const string RowIconClass = UssClass + "__row-icon";
        public const string RowNameClass = UssClass + "__row-name";
        public const string RowCountClass = UssClass + "__row-count";
        public const string EmptyClass = UssClass + "__empty";
        public const string AsideClass = UssClass + "__aside";
        public const string ItemViewClass = UssClass + "__item-view";
        public const string CardClass = UssClass + "__card";
        public const string CardNameClass = UssClass + "__card-name";
        public const string CardKindClass = UssClass + "__card-kind";
        public const string StatClass = UssClass + "__stat";
        public const string StatLabelClass = UssClass + "__stat-label";
        public const string StatValueClass = UssClass + "__stat-value";
        public const string DropClass = UssClass + "__drop";
        public const string FooterClass = UssClass + "__footer";
        public const string WeightClass = UssClass + "__weight";
        public const string OverburdenedClass = WeightClass + "--over";
        public const string HintClass = UssClass + "__hint";

        sealed class Tab
        {
            public ItemKind? Kind;
            public Label View;
        }

        readonly GameSession _game;
        readonly IAssetThumbnails _thumbnails;
        readonly VisualElement _root;
        readonly List<Tab> _tabs = new List<Tab>();
        readonly ScrollView _list;
        readonly TurntableView _characterView;
        readonly TurntableView _itemView;
        readonly VisualElement _card;
        readonly Label _cardName;
        readonly Label _cardKind;
        readonly Label _weight;
        readonly Label _value;
        readonly Label _count;
        readonly Button _drop;
        readonly Label _carried;
        readonly Label _hint;
        Tab _tab;
        RecordId _selected;
        bool _shown;
        bool _dirty = true;
        int _dressed;

        /// <param name="parent">Where the screen goes: a full-screen element of the panel that plays.</param>
        /// <param name="thumbnails">Pictures of the items for the rows, made once each and kept by whoever hosts the game.</param>
        public InventoryScreen(GameSession game, VisualElement parent, IAssetThumbnails thumbnails)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _thumbnails = thumbnails ?? throw new ArgumentNullException(nameof(thumbnails));
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));

            _root = Part<VisualElement>(parent, UssClass);
            _root.name = "inventory-screen";
            var frame = Part<VisualElement>(_root, FrameClass);

            _characterView = new TurntableView(PreviewTurn.Character());
            _characterView.AddToClassList(CharacterViewClass);
            frame.Add(_characterView);

            var tabs = Part<VisualElement>(frame, TabsClass);
            foreach (var kind in TabOrder())
            {
                var tab = new Tab { Kind = kind, View = Part<Label>(tabs, TabClass) };
                tab.View.text = TabName(kind);
                tab.View.RegisterCallback<ClickEvent>(_ => Choose(tab));
                _tabs.Add(tab);
            }

            _list = new ScrollView(ScrollViewMode.Vertical);
            _list.AddToClassList(ListClass);
            frame.Add(_list);

            var aside = Part<VisualElement>(frame, AsideClass);
            _itemView = new TurntableView();
            _itemView.AddToClassList(ItemViewClass);
            aside.Add(_itemView);
            _card = Part<VisualElement>(aside, CardClass);
            _cardName = Part<Label>(_card, CardNameClass);
            _cardKind = Part<Label>(_card, CardKindClass);
            _weight = Stat(_card, "Weight");
            _value = Stat(_card, "Value");
            _count = Stat(_card, "Carried");
            _drop = new Button(DropSelected) { text = "Drop" };
            _drop.AddToClassList(DropClass);
            _card.Add(_drop);

            var footer = Part<VisualElement>(_root, FooterClass);
            _carried = Part<Label>(footer, WeightClass);
            _hint = Part<Label>(footer, HintClass);

            _tab = _tabs[0];
            _root.style.display = DisplayStyle.None;
            _game.Player.Inventory.Changed += MarkDirty;
            _game.Player.Equipment.Changed += Redress;
        }

        /// <summary>
        /// Shows the screen while the inventory is open, brings it up to date when something changed, and turns the
        /// character and the chosen item <paramref name="deltaTime"/> seconds' worth.
        /// </summary>
        public void Tick(float deltaTime)
        {
            var open = _game.Player.InventoryOpen;
            if (open != _shown)
            {
                _shown = open;
                _root.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
                _dirty |= open;
            }

            if (!open)
                return;
            if (_dirty)
                Draw();
            _characterView.Tick(deltaTime);
            _itemView.Tick(deltaTime);
        }

        public void Dispose()
        {
            _game.Player.Inventory.Changed -= MarkDirty;
            _game.Player.Equipment.Changed -= Redress;
            _characterView.Dispose();
            _itemView.Dispose();
            _root.RemoveFromHierarchy();
        }

        /// <summary>The tabs, top to bottom, as Skyrim orders them: All, then every kind as declared, Misc last.</summary>
        public static IEnumerable<ItemKind?> TabOrder()
        {
            yield return null;
            foreach (ItemKind kind in Enum.GetValues(typeof(ItemKind)))
            {
                if (kind != ItemKind.Misc)
                    yield return kind;
            }

            yield return ItemKind.Misc;
        }

        /// <summary>A tab's name: All for every kind, else the kind's, as a list of them ("Weapons").</summary>
        public static string TabName(ItemKind? kind)
        {
            switch (kind)
            {
                case null: return "All";
                case ItemKind.Weapon: return "Weapons";
                case ItemKind.Potion: return "Potions";
                default: return kind.Value.ToString();
            }
        }

        void MarkDirty() => _dirty = true;

        // What the player holds changed: the character is dressed anew at the next draw.
        void Redress()
        {
            _dressed++;
            _dirty = true;
        }

        // Drawn at the next tick, never inside the click that asked: the row clicked is one the drawing replaces.
        void Choose(Tab tab)
        {
            _tab = tab;
            _dirty = true;
        }

        void Select(RecordId item)
        {
            _selected = item;
            _dirty = true;
        }

        void DropSelected()
        {
            if (!_selected.IsDefault)
                _game.Drop(_selected);
        }

        void Draw()
        {
            _dirty = false;
            foreach (var tab in _tabs)
                tab.View.EnableInClassList(TabActiveClass, tab == _tab);

            var world = _game.Player.World;
            var entries = world != null ? InventoryList.Of(_game.Player.Inventory, world, _tab.Kind) : Array.Empty<InventoryEntry>();
            if (!entries.Any(e => e.Item == _selected))
                _selected = entries.Count > 0 ? entries[0].Item : default;

            _list.Clear();
            foreach (var entry in entries)
                _list.Add(Row(entry));
            if (entries.Count == 0)
                Part<Label>(_list.contentContainer, EmptyClass).text = _tab.Kind == null ? "You carry nothing." : $"No {TabName(_tab.Kind).ToLowerInvariant()}.";

            DrawCharacter();
            DrawCard(entries.FirstOrDefault(e => e.Item == _selected));
            DrawFooter();
        }

        // The player as it looks, standing still with what it holds; built again only when that changes, turned as it was.
        void DrawCharacter()
        {
            var model = _game.Player.Model;
            var world = _game.Player.World;
            var subject = model != null ? (object)(model, _dressed) : null;
            _characterView.Show(subject, parent => Dressed(parent, model, world), keepTurn: true);
        }

        // The looks of what it holds hang from the character's model and go with it.
        GameObject Dressed(Transform parent, GameObject prefab, IRecordStore world)
        {
            var character = CharacterModel.StandingStill(parent, prefab);
            if (world != null)
            {
                var held = new HeldItems(character, world, _game.Assets);
                foreach (var equipped in _game.Player.Equipment.All)
                    held.Hold(equipped);
            }

            return character.Root;
        }

        VisualElement Row(InventoryEntry entry)
        {
            var row = new VisualElement();
            row.AddToClassList(RowClass);
            row.EnableInClassList(RowSelectedClass, entry.Item == _selected);
            var icon = Part<VisualElement>(row, RowIconClass);
            var look = entry.Record?.AssetKey;
            if (!string.IsNullOrEmpty(look))
                icon.style.backgroundImage = _thumbnails.GetIcon(look);
            Part<Label>(row, RowNameClass).text = entry.Name;
            Part<Label>(row, RowCountClass).text = entry.Count > 1 ? $"({entry.Count})" : string.Empty;
            var item = entry.Item;
            row.RegisterCallback<ClickEvent>(_ => Select(item));
            return row;
        }

        void DrawCard(InventoryEntry entry)
        {
            var any = entry.Record != null;
            _card.style.visibility = any ? Visibility.Visible : Visibility.Hidden;
            _itemView.Show(_game.Assets, any ? entry.Record.AssetKey : null);
            if (!any)
                return;

            _cardName.text = entry.Name;
            _cardKind.text = entry.Kind.ToString();
            _weight.text = Number(entry.Weight);
            _value.text = entry.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            _count.text = entry.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        void DrawFooter()
        {
            var burden = _game.Player.Burden;
            var world = _game.Player.World;
            if (burden != null)
                _carried.text = $"Weight  {Number(burden.Carried)} / {Number(burden.Capacity)}";
            else
                _carried.text = $"Weight  {Number(world != null ? _game.Player.Inventory.Weight(world) : 0f)}";
            _carried.EnableInClassList(OverburdenedClass, burden != null && burden.Overburdened);
            _hint.text = $"{_game.Player.InventoryKey}  Close";
        }

        static Label Stat(VisualElement parent, string name)
        {
            var stat = Part<VisualElement>(parent, StatClass);
            Part<Label>(stat, StatLabelClass).text = name;
            return Part<Label>(stat, StatValueClass);
        }

        // To a tenth, as the prompt shows weights.
        static string Number(float value) => FormattableString.Invariant($"{value:0.#}");

        static T Part<T>(VisualElement parent, string ussClass) where T : VisualElement, new()
        {
            var part = new T();
            part.AddToClassList(ussClass);
            parent.Add(part);
            return part;
        }
    }
}
