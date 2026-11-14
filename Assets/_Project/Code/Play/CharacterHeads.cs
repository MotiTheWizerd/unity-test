using System;
using System.Collections.Generic;
using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// The heads a replaceable body can wear, and the one it wears: each a skinned piece made for its neck (blender-tests
    /// pieces.py: the body's own head, and each face editor head seated on its neck seam), put on by
    /// <see cref="SkinnedPiece.Stitch"/>. Build Characters lists a character's heads (Content/Characters/{Name}/Heads) and
    /// puts on the one it is made with, so its prefab stands whole; the face editor's Face choice changes it
    /// (<see cref="Hud.FaceLook"/>).
    /// </summary>
    public sealed class CharacterHeads : MonoBehaviour
    {
        [SerializeField, Tooltip("The heads it can wear: prefabs of skinned pieces made for its neck (Build Characters lists them).")]
        GameObject[] _heads = Array.Empty<GameObject>();

        [SerializeField, Tooltip("Which of the heads it wears; -1 for none.")]
        int _worn = -1;

        [SerializeField, Tooltip("What the worn head draws with, under this object.")]
        List<SkinnedMeshRenderer> _wornRenderers = new List<SkinnedMeshRenderer>();

        /// <summary>How many heads it can wear.</summary>
        public int Count => _heads.Length;

        /// <summary>Which of the heads it wears; -1 for none.</summary>
        public int Worn => _worn;

        /// <summary>What the worn head draws with, on the body's bones.</summary>
        public IReadOnlyList<SkinnedMeshRenderer> WornRenderers => _wornRenderers;

        /// <summary>The name of head <paramref name="index"/> (its prefab's).</summary>
        public string NameOf(int index) => _heads[index].name;

        /// <summary>Gives it <paramref name="heads"/> to wear (Build Characters), wearing none of them yet.</summary>
        public void SetHeads(IReadOnlyList<GameObject> heads)
        {
            TakeOff();
            _heads = new GameObject[heads.Count];
            for (var i = 0; i < heads.Count; i++)
                _heads[i] = heads[i] != null ? heads[i] : throw new ArgumentException($"Head {i} is missing.", nameof(heads));
        }

        /// <summary>Wears head <paramref name="index"/> in place of the one it wears; wearing it already changes nothing.</summary>
        public void Wear(int index)
        {
            if (index < 0 || index >= _heads.Length)
                throw new ArgumentOutOfRangeException(nameof(index), index, $"It has {_heads.Length} heads.");
            if (index == _worn)
                return;

            TakeOff();
            foreach (var piece in _heads[index].GetComponentsInChildren<SkinnedMeshRenderer>(true))
                _wornRenderers.Add(SkinnedPiece.Stitch(transform, piece));
            _worn = index;
        }

        void TakeOff()
        {
            foreach (var renderer in _wornRenderers)
            {
                if (renderer == null)
                    continue;
                // Gone at once while it plays, where Destroy waits for the frame's end.
                renderer.gameObject.SetActive(false);
                if (Application.isPlaying)
                    Destroy(renderer.gameObject);
                else
                    DestroyImmediate(renderer.gameObject);
            }

            _wornRenderers.Clear();
            _worn = -1;
        }
    }
}
