using System;

namespace Forge.Core.Imaging
{
    /// <summary>
    /// Reads a Windows device-independent bitmap (the clipboard's CF_DIB / CF_DIBV5: a BITMAPINFOHEADER or a later
    /// header, then the pixels, with no file header) into RGBA bytes, bottom row first as Unity's textures take them.
    /// Uncompressed 24-bit and 32-bit pictures only, the kinds screenshots and copied images arrive as.
    /// </summary>
    public static class DeviceIndependentBitmap
    {
        const int InfoHeaderSize = 40;
        const int Rgb = 0;
        const int BitFields = 3;

        /// <summary>Decodes <paramref name="dib"/>; false (and nulls) for anything malformed or not supported.</summary>
        public static bool TryDecode(byte[] dib, out int width, out int height, out byte[] rgba)
        {
            width = height = 0;
            rgba = null;
            if (dib == null || dib.Length < InfoHeaderSize)
                return false;

            var headerSize = ReadInt(dib, 0);
            var signedHeight = ReadInt(dib, 8);
            var bitCount = ReadUShort(dib, 14);
            var compression = ReadInt(dib, 16);
            var paletteSize = ReadInt(dib, 32);
            var bitmapWidth = ReadInt(dib, 4);
            if (headerSize < InfoHeaderSize || headerSize > dib.Length || bitmapWidth <= 0 || signedHeight == 0 || signedHeight == int.MinValue)
                return false;
            if (!(bitCount == 24 && compression == Rgb) && !(bitCount == 32 && (compression == Rgb || compression == BitFields)))
                return false;

            // The channel masks: inside a V4/V5 header, after a plain one when they are bit fields, else BGRX.
            uint red = 0x00FF0000, green = 0x0000FF00, blue = 0x000000FF, alpha = 0;
            var pixelsAt = (long)headerSize + Math.Max(0, paletteSize) * 4L;
            if (compression == BitFields)
            {
                var masksAt = headerSize > InfoHeaderSize ? InfoHeaderSize : headerSize;
                if (masksAt + 12 > dib.Length)
                    return false;
                red = (uint)ReadInt(dib, masksAt);
                green = (uint)ReadInt(dib, masksAt + 4);
                blue = (uint)ReadInt(dib, masksAt + 8);
                if (headerSize >= InfoHeaderSize + 16)
                    alpha = (uint)ReadInt(dib, InfoHeaderSize + 12);
                if (headerSize == InfoHeaderSize)
                    pixelsAt += 12;
            }
            else if (bitCount == 32 && headerSize >= InfoHeaderSize + 16)
            {
                // A V4/V5 header names the alpha channel even when the pixels are plain BGRA.
                alpha = (uint)ReadInt(dib, InfoHeaderSize + 12);
            }

            var rows = Math.Abs(signedHeight);
            var topDown = signedHeight < 0;
            var bytesPerPixel = bitCount / 8;
            var stride = ((long)bitmapWidth * bitCount + 31) / 32 * 4;
            if (pixelsAt + stride * rows > dib.Length || (long)bitmapWidth * rows > int.MaxValue / 4)
                return false;

            var output = new byte[bitmapWidth * rows * 4];
            var anyAlpha = false;
            for (var row = 0; row < rows; row++)
            {
                // Output is bottom row first; a bottom-up bitmap is stored that way already.
                var source = pixelsAt + stride * (topDown ? rows - 1 - row : row);
                var target = row * bitmapWidth * 4;
                for (var x = 0; x < bitmapWidth; x++, target += 4)
                {
                    var at = source + x * bytesPerPixel;
                    if (bitCount == 24)
                    {
                        output[target] = dib[at + 2];
                        output[target + 1] = dib[at + 1];
                        output[target + 2] = dib[at];
                        output[target + 3] = 255;
                        continue;
                    }

                    var pixel = (uint)ReadInt(dib, at);
                    output[target] = Channel(pixel, red);
                    output[target + 1] = Channel(pixel, green);
                    output[target + 2] = Channel(pixel, blue);
                    output[target + 3] = alpha == 0 ? (byte)255 : Channel(pixel, alpha);
                    anyAlpha |= alpha != 0 && output[target + 3] != 0;
                }
            }

            // Many programs leave a 32-bit picture's alpha all zero; that means opaque, not invisible.
            if (alpha != 0 && !anyAlpha)
            {
                for (var i = 3; i < output.Length; i += 4)
                    output[i] = 255;
            }

            width = bitmapWidth;
            height = rows;
            rgba = output;
            return true;
        }

        static byte Channel(uint pixel, uint mask)
        {
            if (mask == 0)
                return 0;
            var shift = 0;
            while ((mask >> shift & 1) == 0)
                shift++;
            var bits = 0;
            while (shift + bits < 32 && (mask >> (shift + bits) & 1) == 1)
                bits++;
            var value = (pixel & mask) >> shift;
            var max = bits >= 32 ? uint.MaxValue : (1u << bits) - 1;
            return (byte)((ulong)value * 255 / max);
        }

        static int ReadInt(byte[] bytes, long at) =>
            bytes[at] | bytes[at + 1] << 8 | bytes[at + 2] << 16 | bytes[at + 3] << 24;

        static int ReadUShort(byte[] bytes, long at) => bytes[at] | bytes[at + 1] << 8;
    }
}
