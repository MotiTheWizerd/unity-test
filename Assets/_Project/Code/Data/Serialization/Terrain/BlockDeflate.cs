using System;
using System.IO;
using System.IO.Compression;

namespace Forge.Data.Serialization
{
    /// <summary>Deflate for terrain file blocks, which know exactly how many bytes they unpack to.</summary>
    static class BlockDeflate
    {
        public static byte[] Pack(byte[] bytes)
        {
            using (var packed = new MemoryStream())
            {
                using (var deflate = new DeflateStream(packed, CompressionLevel.Optimal, leaveOpen: true))
                    deflate.Write(bytes, 0, bytes.Length);
                return packed.ToArray();
            }
        }

        /// <summary>Unpacks <paramref name="block"/> into exactly <paramref name="length"/> bytes.</summary>
        /// <param name="what">What the block holds, for the error message ("heights", "paint").</param>
        /// <exception cref="DataValidationException">The block is damaged, or holds more or fewer bytes.</exception>
        public static byte[] Unpack(byte[] block, int length, string what)
        {
            var bytes = new byte[length];
            try
            {
                using (var deflate = new DeflateStream(new MemoryStream(block, writable: false), CompressionMode.Decompress))
                {
                    var read = 0;
                    while (read < bytes.Length)
                    {
                        var n = deflate.Read(bytes, read, bytes.Length - read);
                        if (n == 0)
                            throw new DataValidationException(new[] { $"A terrain block holds {read} of its {bytes.Length} bytes of {what}." });
                        read += n;
                    }

                    if (deflate.ReadByte() != -1)
                        throw new DataValidationException(new[] { $"A terrain block holds more than its {what}." });
                }
            }
            // Runtimes differ: .NET reports bad Deflate data as InvalidDataException, Mono (Unity) as IOException.
            catch (Exception ex) when (ex is InvalidDataException || ex is IOException)
            {
                throw new DataValidationException(new[] { $"A terrain block is damaged: {ex.Message}" }, ex);
            }

            return bytes;
        }
    }
}
