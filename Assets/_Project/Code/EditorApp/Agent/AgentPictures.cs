using Forge.Agent;
using UnityEngine;

namespace Forge.EditorApp.Agent
{
    /// <summary>
    /// Turns what the editor can show into pictures for the agent (<see cref="AgentImage"/>): the view, or any
    /// texture, shrunk to the size the model reads (<see cref="AgentImage.Fit"/>) and encoded as PNG, or JPEG when a
    /// PNG would be too heavy. Call on the main thread.
    /// </summary>
    public static class AgentPictures
    {
        const int JpegQuality = 90;

        /// <summary>
        /// What <paramref name="camera"/> sees, at the screen's shape: the world only, without the panel drawn over it.
        /// </summary>
        public static AgentImage Snapshot(Camera camera)
        {
            var (width, height) = AgentImage.Fit(Mathf.Max(1, camera.pixelWidth), Mathf.Max(1, camera.pixelHeight));
            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previous = camera.targetTexture;
            try
            {
                camera.targetTexture = target;
                camera.Render();
            }
            finally
            {
                camera.targetTexture = previous;
            }

            try
            {
                return Encode(target, width, height);
            }
            finally
            {
                RenderTexture.ReleaseTemporary(target);
            }
        }

        /// <summary><paramref name="source"/> as a picture, shrunk if it is larger than the model reads.</summary>
        public static AgentImage FromTexture(Texture source)
        {
            var (width, height) = AgentImage.Fit(source.width, source.height);
            var target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            try
            {
                Graphics.Blit(source, target);
                return Encode(target, width, height);
            }
            finally
            {
                RenderTexture.ReleaseTemporary(target);
            }
        }

        /// <summary>A texture of <paramref name="image"/> for the chat to show; the caller destroys it.</summary>
        public static Texture2D Thumbnail(AgentImage image)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "Agent picture", wrapMode = TextureWrapMode.Clamp };
            texture.LoadImage(image.Bytes, true);
            return texture;
        }

        static AgentImage Encode(RenderTexture target, int width, int height)
        {
            var previous = RenderTexture.active;
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                pixels.Apply(false);
                var png = pixels.EncodeToPNG();
                return png.Length <= AgentImage.MaxBytes
                    ? new AgentImage("image/png", png)
                    : new AgentImage("image/jpeg", pixels.EncodeToJPG(JpegQuality));
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(pixels);
            }
        }
    }
}
