using System;
using System.Runtime.InteropServices;
using Forge.Agent;
using Forge.Core.Imaging;
using UnityEngine;

namespace Forge.EditorApp.Agent
{
    /// <summary>
    /// A picture on the system clipboard, for pasting into the agent's message. Unity's own clipboard
    /// (<see cref="GUIUtility.systemCopyBuffer"/>) carries text only, so this reads the system's.
    /// </summary>
    public interface IImageClipboard
    {
        /// <summary>
        /// The clipboard's picture, or null when it holds none, or holds words too (pasting those is the text box's).
        /// </summary>
        AgentImage ReadPicture();
    }

    public static class ImageClipboard
    {
        /// <summary>The clipboard of the platform Forge runs on.</summary>
        public static IImageClipboard ForThisPlatform()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            return new WindowsImageClipboard();
#else
            // SHORTCUT: pasting pictures works on Windows only; elsewhere the clipboard has no picture.
            return new NoImageClipboard();
#endif
        }
    }

    public sealed class NoImageClipboard : IImageClipboard
    {
        public AgentImage ReadPicture() => null;
    }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    /// <summary>
    /// The Windows clipboard: a "PNG" entry as browsers and editors put it (sent as it is when it fits), else the
    /// bitmap every screenshot tool puts there (CF_DIBV5, or CF_DIB), decoded by <see cref="DeviceIndependentBitmap"/>.
    /// </summary>
    public sealed class WindowsImageClipboard : IImageClipboard
    {
        const uint UnicodeText = 13;
        const uint Dib = 8;
        const uint DibV5 = 17;

        public AgentImage ReadPicture()
        {
            byte[] png = null, dib = null;
            if (!OpenClipboard(IntPtr.Zero))
                return null;
            try
            {
                if (IsClipboardFormatAvailable(UnicodeText))
                    return null;
                var pngFormat = RegisterClipboardFormat("PNG");
                if (pngFormat != 0 && IsClipboardFormatAvailable(pngFormat))
                    png = Read(pngFormat);
                if (png == null)
                    dib = IsClipboardFormatAvailable(DibV5) ? Read(DibV5) : IsClipboardFormatAvailable(Dib) ? Read(Dib) : null;
            }
            finally
            {
                CloseClipboard();
            }

            return png != null ? FromPng(png) : dib != null ? FromDib(dib) : null;
        }

        static AgentImage FromPng(byte[] png)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(png))
                    return null;
                // Sent as it came when it already fits; otherwise shrunk.
                var (width, height) = AgentImage.Fit(texture.width, texture.height);
                return width == texture.width && height == texture.height && png.Length <= AgentImage.MaxBytes
                    ? new AgentImage("image/png", png)
                    : AgentPictures.FromTexture(texture);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        static AgentImage FromDib(byte[] dib)
        {
            if (!DeviceIndependentBitmap.TryDecode(dib, out var width, out var height, out var rgba))
                return null;

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                texture.LoadRawTextureData(rgba);
                texture.Apply(false);
                return AgentPictures.FromTexture(texture);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        static byte[] Read(uint format)
        {
            var handle = GetClipboardData(format);
            if (handle == IntPtr.Zero)
                return null;
            var size = (long)GlobalSize(handle);
            var data = GlobalLock(handle);
            if (data == IntPtr.Zero || size <= 0 || size > int.MaxValue)
                return null;
            try
            {
                var bytes = new byte[size];
                Marshal.Copy(data, bytes, 0, (int)size);
                return bytes;
            }
            finally
            {
                GlobalUnlock(handle);
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool OpenClipboard(IntPtr owner);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool CloseClipboard();

        [DllImport("user32.dll")]
        static extern bool IsClipboardFormatAvailable(uint format);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr GetClipboardData(uint format);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern uint RegisterClipboardFormat(string name);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr GlobalLock(IntPtr memory);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GlobalUnlock(IntPtr memory);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern UIntPtr GlobalSize(IntPtr memory);
    }
#endif
}
