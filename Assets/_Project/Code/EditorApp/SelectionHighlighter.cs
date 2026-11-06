using System;
using System.Collections.Generic;
using Forge.Data;
using Forge.Editing;
using Forge.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Forge.EditorApp
{
    /// <summary>
    /// Highlights the selected objects by drawing them again with the selection overlay (<c>Forge/SelectionOverlay</c>):
    /// a faint glow over the whole object, and a stronger one over the parts of the material slot a material ball would
    /// dress, so it is clear what is selected and which part a material goes on; while a material is dragged over an object,
    /// the part it would land on lights up brightest. The objects' own materials are never
    /// touched, so what they wear shows through.
    /// </summary>
    public sealed class SelectionHighlighter
    {
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int FillId = Shader.PropertyToID("_Fill");
        static readonly int RimId = Shader.PropertyToID("_Rim");

        readonly Selection _selection;
        readonly WorldView _view;
        readonly Material _overlay;
        readonly Func<string> _emphasizedSlot;
        readonly Func<(RecordId Id, string Slot)?> _dropTarget;
        readonly MaterialPropertyBlock _object = Emphasis(new Color(1f, 0.72f, 0.2f, 1f), fill: 0.08f, rim: 0.55f);
        readonly MaterialPropertyBlock _slot = Emphasis(new Color(1f, 0.85f, 0.35f, 1f), fill: 0.35f, rim: 0.9f);
        readonly MaterialPropertyBlock _drop = Emphasis(new Color(1f, 0.95f, 0.75f, 1f), fill: 0.45f, rim: 1f);
        readonly List<MaterialSlots.Part> _parts = new List<MaterialSlots.Part>();

        /// <param name="overlay">What the highlight is drawn with (Materials/SelectionOverlay).</param>
        /// <param name="emphasizedSlot">The slot whose parts stand out more, read each frame; null for none.</param>
        /// <param name="dropTarget">The object part a dragged material would land on, read each frame; null for none.</param>
        public SelectionHighlighter(Selection selection, WorldView view, Material overlay, Func<string> emphasizedSlot, Func<(RecordId Id, string Slot)?> dropTarget)
        {
            _dropTarget = dropTarget ?? throw new ArgumentNullException(nameof(dropTarget));
            _selection = selection ?? throw new ArgumentNullException(nameof(selection));
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _overlay = overlay != null ? overlay : throw new ArgumentNullException(nameof(overlay));
            _emphasizedSlot = emphasizedSlot ?? throw new ArgumentNullException(nameof(emphasizedSlot));
        }

        /// <summary>Collects the selected objects' parts again; call after the selection or the presented objects change.</summary>
        public void Refresh()
        {
            _parts.Clear();
            foreach (var id in _selection.Ids)
            {
                if (_view.TryGetObject(id, out var go) && go.TryGetComponent<MaterialSlots>(out var slots))
                    _parts.AddRange(slots.Parts);
            }
        }

        /// <summary>Draws the highlight for this frame (the overlay is drawn like grass: queued each frame, no scene objects).</summary>
        public void Draw()
        {
            var slot = _emphasizedSlot();
            foreach (var part in _parts)
                DrawPart(part, slot != null && part.Slot == slot ? _slot : _object);

            // While a material is carried over an object, the part it would dress lights up.
            var drop = _dropTarget();
            if (drop.HasValue && _view.TryGetObject(drop.Value.Id, out var target) && target.TryGetComponent<MaterialSlots>(out var slots))
            {
                foreach (var part in slots.Parts)
                {
                    if (part.Slot == drop.Value.Slot)
                        DrawPart(part, _drop);
                }
            }
        }

        void DrawPart(MaterialSlots.Part part, MaterialPropertyBlock emphasis)
        {
            // A part can vanish between refreshes (an object rebuilt or unloaded mid-frame).
            if (part.Renderer == null || !part.Renderer.enabled || !part.Renderer.TryGetComponent<MeshFilter>(out var filter) || filter.sharedMesh == null)
                return;

            var parameters = new RenderParams(_overlay)
            {
                matProps = emphasis,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                layer = part.Renderer.gameObject.layer,
            };
            Graphics.RenderMesh(parameters, filter.sharedMesh, Math.Min(part.SubMesh, filter.sharedMesh.subMeshCount - 1), part.Renderer.localToWorldMatrix);
        }

        static MaterialPropertyBlock Emphasis(Color color, float fill, float rim)
        {
            var block = new MaterialPropertyBlock();
            block.SetColor(ColorId, color);
            block.SetFloat(FillId, fill);
            block.SetFloat(RimId, rim);
            return block;
        }
    }
}
