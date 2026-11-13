using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Forge.Play.Hud
{
    /// <summary>
    /// Shows the <see cref="FaceEditorScreen"/> alone, on a character of its own, so the screen is seen without a world
    /// to load (Scenes/FaceEditor.unity, opened by Forge → Characters → Face Editor). The game's start hosts the screen
    /// once a new game makes its player there.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class FaceEditorStage : MonoBehaviour
    {
        [SerializeField, Tooltip("Whose face is made: a character prefab, or a head alone (Content/Heads).")]
        GameObject _character;

        [NonSerialized] FaceEditorScreen _screen;

        void OnEnable() => _screen = new FaceEditorScreen(GetComponent<UIDocument>().rootVisualElement, _character);

        void Update() => _screen?.Tick(Time.unscaledDeltaTime);

        void OnDisable()
        {
            _screen?.Dispose();
            _screen = null;
        }
    }
}
