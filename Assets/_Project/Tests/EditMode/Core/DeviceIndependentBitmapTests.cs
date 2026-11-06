using System;
using System.Collections.Generic;
using Forge.Core.Imaging;
using NUnit.Framework;

namespace Forge.Tests.Core
{
    public class DeviceIndependentBitmapTests
    {
        // A 2×2 picture, top row red then green, bottom row blue then white.
        static readonly byte[][] TopRow = { new byte[] { 255, 0, 0 }, new byte[] { 0, 255, 0 } };
        static readonly byte[][] BottomRow = { new byte[] { 0, 0, 255 }, new byte[] { 255, 255, 255 } };

        // Unity's order: bottom row first, RGBA.
        static readonly byte[] Expected =
        {
            0, 0, 255, 255, 255, 255, 255, 255,
            255, 0, 0, 255, 0, 255, 0, 255,
        };

        [Test]
        public void A24BitBottomUpBitmap_ReadsBottomRowFirst_WithPaddedRows()
        {
            var dib = Bitmap(40, 2, 24, 0, rows: new[] { BottomRow, TopRow }, pixel: (rgb, row) => new[] { rgb[2], rgb[1], rgb[0] });

            Assert.That(DeviceIndependentBitmap.TryDecode(dib, out var width, out var height, out var rgba), Is.True);
            Assert.That((width, height), Is.EqualTo((2, 2)));
            Assert.That(rgba, Is.EqualTo(Expected));
        }

        [Test]
        public void ATopDownBitmap_IsTurnedTheRightWayUp()
        {
            var dib = Bitmap(40, -2, 32, 0, rows: new[] { TopRow, BottomRow }, pixel: (rgb, row) => new byte[] { rgb[2], rgb[1], rgb[0], 0 });

            Assert.That(DeviceIndependentBitmap.TryDecode(dib, out _, out _, out var rgba), Is.True);
            Assert.That(rgba, Is.EqualTo(Expected), "A 32-bit plain bitmap's spare byte is not alpha: the picture is opaque.");
        }

        [Test]
        public void BitFieldsAfterAPlainHeader_AreTheChannelMasks()
        {
            // Masks for an RGBX layout (red in the low byte), unlike the usual BGRX.
            var masks = new List<byte>();
            masks.AddRange(BitConverter.GetBytes(0x000000FFu));
            masks.AddRange(BitConverter.GetBytes(0x0000FF00u));
            masks.AddRange(BitConverter.GetBytes(0x00FF0000u));
            var dib = Bitmap(40, 2, 32, 3, rows: new[] { BottomRow, TopRow }, pixel: (rgb, row) => new byte[] { rgb[0], rgb[1], rgb[2], 0 }, afterHeader: masks.ToArray());

            Assert.That(DeviceIndependentBitmap.TryDecode(dib, out _, out _, out var rgba), Is.True);
            Assert.That(rgba, Is.EqualTo(Expected));
        }

        [Test]
        public void AV5Header_KeepsItsAlpha_UnlessItIsAllZero()
        {
            byte[] V5(byte alpha) => Bitmap(124, 2, 32, 3, rows: new[] { BottomRow, TopRow }, pixel: (rgb, row) => new[] { rgb[2], rgb[1], rgb[0], alpha },
                header: header =>
                {
                    Write(header, 40, 0x00FF0000);
                    Write(header, 44, 0x0000FF00);
                    Write(header, 48, 0x000000FF);
                    Write(header, 52, unchecked((int)0xFF000000));
                });

            Assert.That(DeviceIndependentBitmap.TryDecode(V5(128), out _, out _, out var halfSeen), Is.True);
            Assert.That(halfSeen[3], Is.EqualTo(128));
            Assert.That(DeviceIndependentBitmap.TryDecode(V5(0), out _, out _, out var opaque), Is.True);
            Assert.That(opaque, Is.EqualTo(Expected), "All-zero alpha means the program never wrote it.");
        }

        [Test]
        public void WhatItCantRead_IsRefused()
        {
            Assert.That(DeviceIndependentBitmap.TryDecode(null, out _, out _, out _), Is.False);
            Assert.That(DeviceIndependentBitmap.TryDecode(new byte[12], out _, out _, out _), Is.False);
            var eightBit = Bitmap(40, 2, 8, 0, rows: new[] { new[] { new byte[] { 1 }, new byte[] { 1 } }, new[] { new byte[] { 1 }, new byte[] { 1 } } }, pixel: (v, row) => v);
            Assert.That(DeviceIndependentBitmap.TryDecode(eightBit, out _, out _, out _), Is.False, "Palette pictures aren't read.");
            var cut = Bitmap(40, 2, 24, 0, rows: new[] { BottomRow, TopRow }, pixel: (rgb, row) => new[] { rgb[2], rgb[1], rgb[0] });
            Array.Resize(ref cut, cut.Length - 4);
            Assert.That(DeviceIndependentBitmap.TryDecode(cut, out _, out _, out _), Is.False, "Too few pixels for the size it claims.");
        }

        static byte[] Bitmap(int headerSize, int height, int bitCount, int compression, byte[][][] rows, Func<byte[], int, byte[]> pixel,
            byte[] afterHeader = null, Action<byte[]> header = null)
        {
            var width = rows[0].Length;
            var head = new byte[headerSize];
            Write(head, 0, headerSize);
            Write(head, 4, width);
            Write(head, 8, height);
            head[12] = 1;
            head[14] = (byte)bitCount;
            Write(head, 16, compression);
            header?.Invoke(head);

            var bytes = new List<byte>(head);
            if (afterHeader != null)
                bytes.AddRange(afterHeader);
            var stride = (width * bitCount + 31) / 32 * 4;
            for (var r = 0; r < rows.Length; r++)
            {
                var row = new List<byte>();
                foreach (var value in rows[r])
                    row.AddRange(pixel(value, r));
                while (row.Count < stride)
                    row.Add(0);
                bytes.AddRange(row);
            }

            return bytes.ToArray();
        }

        static void Write(byte[] bytes, int at, int value) => Array.Copy(BitConverter.GetBytes(value), 0, bytes, at, 4);
    }
}
