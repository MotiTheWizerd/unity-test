using System;
using System.Collections.Generic;
using Forge.Editing;
using Forge.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Forge.EditorApp
{
    /// <summary>
    /// Highlights the selected objects by drawing them again with the selection overlay (<c>Forge/SelectionOverlay</c>):
    /// a faint glow over the whole object, and a stronger one over the parts of the material slot a material ball would
    /// dress, so it is clear what is selected and which part a material goes on. The objects' own materials are never
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
        readonly MaterialPropertyBlock _object = Emphasis(new Color(1f, 0.72f, 0.2f, 1f), fill: 0.08f, rim: 0.55f);
        readonly MaterialPropertyBlock _slot = Emphasis(new Color(1f, 0.85f, 0.35f, 1f), fill: 0.35f, rim: 0.9f);
        readonly List<MaterialSlots.Part> _parts = new List<MaterialSlots.Part>();

        /// <param name="overlay">What the highlight is drawn with (Materials/SelectionOverlay).</param>
        /// <param name="emphasizedSlot">The slot whose parts stand out more, read each frame; null for none.</param>
        public SelectionHighlighter(Selection selection, WorldView view, Material overlay, Func<string> emphasizedSlot)
        {
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
            if (_parts.Count == 0)
                return;

            var slot = _emphasizedSlot();
            foreach (var part in _parts)
            {
                // A part can vanish between refreshes (an object rebuilt or unloaded mid-frame).
                if (part.Renderer == null || !part.Renderer.enabled || !part.Renderer.TryGetComponent<MeshFilter>(out var filter) || filter.sharedMesh == null)
                    continue;

                var emphasis = slot != null && part.Slot == slot ? _slot : _object;
                var parameters = new RenderParams(_overlay)
                {
                    matProps = emphasis,
                    shadowCastingMode = ShadowCastingMode.Off,
                    receiveShadows = false,
                    layer = part.Renderer.gameObject.layer,
                };
                Graphics.RenderMesh(parameters, filter.sharedMesh, Math.Min(part.SubMesh, filter.sharedMesh.subMeshCount - 1), part.Renderer.localToWorldMatrix);
            }
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
