using System;
using System.IO;
using System.Text;

namespace Forge.Data.Serialization
{
    /// <summary>
    /// Writes a text file so that a crash at any point leaves either the old file or the new one, never half of one:
    /// the text goes to a temporary file next to it, flushed to disk, which then replaces the old file in one step.
    /// </summary>
    public static class AtomicFile
    {
        /// <summary>UTF-8 without a byte order mark, how every Forge text file is written and read.</summary>
        public static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        /// <summary>Writes <paramref name="path"/> through <paramref name="write"/>, creating its folder if needed.</summary>
        public static void WriteText(string path, Action<TextWriter> write)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));
            if (write == null)
                throw new ArgumentNullException(nameof(write));

            var folder = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(folder))
                Directory.CreateDirectory(folder);

            var temp = path + ".tmp";
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                using (var writer = new StreamWriter(stream, Utf8NoBom, 4096, leaveOpen: true))
                    write(writer);
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(path))
                File.Replace(temp, path, null);
            else
                File.Move(temp, path);
        }
    }
}
