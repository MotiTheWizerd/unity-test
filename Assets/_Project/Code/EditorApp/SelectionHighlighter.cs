using System;
using System.Collections.Generic;
using Forge.Editing;
using Forge.World;
using UnityEngine;

namespace Forge.EditorApp
{
    /// <summary>
    /// Tints the scene objects of selected records. Uses property blocks, so
    /// shared materials are never modified.
    /// </summary>
    public sealed class SelectionHighlighter
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly Color HighlightColor = new Color(1f, 0.75f, 0.2f);

        readonly Selection _selection;
        readonly WorldView _view;
        readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        readonly List<Renderer> _tinted = new List<Renderer>();

        public SelectionHighlighter(Selection selection, WorldView view)
        {
            _selection = selection ?? throw new ArgumentNullException(nameof(selection));
            _view = view ?? throw new ArgumentNullException(nameof(view));
        }

        /// <summary>Re-applies the tint; call after the selection or the presented objects change.</summary>
        public void Refresh()
        {
            foreach (var renderer in _tinted)
            {
                if (renderer != null)
                    renderer.SetPropertyBlock(null);
            }

            _tinted.Clear();
            _block.SetColor(BaseColorId, HighlightColor);
            _block.SetColor(ColorId, HighlightColor);

            foreach (var id in _selection.Ids)
            {
                if (!_view.TryGetObject(id, out var go))
                    continue;

                foreach (var renderer in go.GetComponentsInChildren<Renderer>())
                {
                    renderer.SetPropertyBlock(_block);
                    _tinted.Add(renderer);
                }
            }
        }
    }
}
