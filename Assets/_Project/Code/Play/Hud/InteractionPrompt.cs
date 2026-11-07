using Forge.Game.Interaction;
using UnityEngine.UIElements;

namespace Forge.Play.Hud
{
    /// <summary>
    /// The use key's prompt under the middle of the view, Skyrim's way: what is in sight ("Viking Sword"), the key and what
    /// it does ("E  Take"), and a line about it ("Weight 9   Value 75"), drawn by PlayerHud.uss; hidden while nothing in
    /// sight can be used.
    /// </summary>
    public sealed class InteractionPrompt : VisualElement
    {
        public const string UssClass = "forge-hud-prompt";
        public const string NameClass = UssClass + "__name";
        public const string ActionClass = UssClass + "__action";
        public const string KeyClass = UssClass + "__key";
        public const string VerbClass = UssClass + "__verb";
        public const string DetailClass = UssClass + "__detail";

        readonly Label _name;
        readonly Label _key;
        readonly Label _verb;
        readonly Label _detail;
        InteractionOffer _shown;
        bool _drawn;

        public InteractionPrompt()
        {
            AddToClassList(UssClass);
            pickingMode = PickingMode.Ignore;

            _name = Part<Label>(this, NameClass);
            var action = Part<VisualElement>(this, ActionClass);
            _key = Part<Label>(action, KeyClass);
            _verb = Part<Label>(action, VerbClass);
            _detail = Part<Label>(this, DetailClass);
            style.display = DisplayStyle.None;
        }

        /// <summary>Shows <paramref name="offer"/> with the use key named <paramref name="key"/>; null hides the prompt.</summary>
        public void Show(InteractionOffer offer, string key)
        {
            // Offers are immutable and kept while what is in sight stays: the same one is already drawn.
            if (_drawn && ReferenceEquals(offer, _shown))
                return;
            _drawn = true;
            _shown = offer;
            style.display = offer != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (offer == null)
                return;

            _name.text = offer.Name;
            _key.text = key;
            _verb.text = offer.Verb;
            _detail.text = offer.Detail ?? string.Empty;
            _detail.style.display = offer.Detail != null ? DisplayStyle.Flex : DisplayStyle.None;
        }

        static T Part<T>(VisualElement parent, string ussClass) where T : VisualElement, new()
        {
            var part = new T { pickingMode = PickingMode.Ignore };
            part.AddToClassList(ussClass);
            parent.Add(part);
            return part;
        }
    }
}
