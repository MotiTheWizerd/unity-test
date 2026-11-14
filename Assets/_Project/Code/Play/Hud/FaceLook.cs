using UnityEngine;

namespace Forge.Play.Hud
{
    /// <summary>
    /// What the face editor's controls do to the head shown: a slider drives the blend shape named by its part and its
    /// name without spaces ("Nose" "Width": NoseWidth; "Jaw" "Chin": JawChin), from the other way at 0 through none at
    /// <see cref="FaceEditorScreen.Middle"/> to all of it at 100, so a shape made in Blender is a working slider with no
    /// code; Skin's Tone tints the skin, pale to deep brown; Head's Face puts on another of the body's heads
    /// (<see cref="CharacterHeads"/>). A control the head has no shape for changes nothing.
    /// </summary>
    public static class FaceLook
    {
        /// <summary>The part whose <see cref="FaceControl"/> chooses the head.</summary>
        public const string HeadPart = "Head";

        /// <summary>The choice of the body's heads (<see cref="CharacterHeads"/>), on the <see cref="HeadPart"/>.</summary>
        public const string FaceControl = "Face";

        /// <summary>The tint's colour property in URP Lit, multiplied into its base map.</summary>
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        static readonly Color Pale = new Color(1.06f, 1.03f, 1f);
        static readonly Color Deep = new Color(0.42f, 0.3f, 0.24f);

        /// <summary>The blend shape a control drives: its part and its name, spaces gone ("JawChin").</summary>
        public static string ShapeName(string part, string control) => (part + control).Replace(" ", string.Empty);

        /// <summary>A slider's value (0 to 100) as a blend shape's weight: -100 at 0, none at the middle, 100 at 100.</summary>
        public static float Weight(int value) => (value - FaceEditorScreen.Middle) * 100f / (100 - FaceEditorScreen.Middle);

        /// <summary>Skin's Tone (0 to 100) as the tint on the skin's base map: a touch paler at 0, as made at the middle, deep brown at 100.</summary>
        public static Color SkinTint(int value) =>
            value <= FaceEditorScreen.Middle
                ? Color.LerpUnclamped(Pale, Color.white, value / (float)FaceEditorScreen.Middle)
                : Color.LerpUnclamped(Color.white, Deep, (value - FaceEditorScreen.Middle) / (100f - FaceEditorScreen.Middle));

        /// <summary>Gives <paramref name="look"/> the value <paramref name="value"/> of <paramref name="part"/>'s <paramref name="control"/>.</summary>
        public static void Apply(GameObject look, string part, string control, int value)
        {
            if (look == null)
                return;

            if (part == HeadPart && control == FaceControl)
            {
                var heads = look.GetComponentInChildren<CharacterHeads>(true);
                if (heads != null)
                    heads.Wear(value);
                return;
            }

            if (part == "Skin" && control == "Tone")
            {
                var block = new MaterialPropertyBlock();
                foreach (var renderer in look.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.GetPropertyBlock(block);
                    block.SetColor(BaseColor, SkinTint(value));
                    renderer.SetPropertyBlock(block);
                }

                return;
            }

            var name = ShapeName(part, control);
            foreach (var skin in look.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = skin.sharedMesh;
                var index = mesh != null ? mesh.GetBlendShapeIndex(name) : -1;
                if (index >= 0)
                    skin.SetBlendShapeWeight(index, Weight(value));
            }
        }
    }
}
