using System;
using System.IO;
using System.Text;

namespace Forge.UnityEditor.Content
{
    /// <summary>Writes sound as a WAV file: 16-bit PCM, one channel, as Build Sounds bakes takes out of a recording.</summary>
    static class WavFile
    {
        /// <summary>Writes <paramref name="samples"/> (-1 to 1, one channel) at <paramref name="frequency"/> to <paramref name="path"/>.</summary>
        public static void WriteMono(string path, float[] samples, int frequency)
        {
            if (samples == null)
                throw new ArgumentNullException(nameof(samples));
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                const short bits = 16;
                var dataBytes = samples.Length * (bits / 8);
                writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + dataBytes);
                writer.Write(Encoding.ASCII.GetBytes("WAVE"));
                writer.Write(Encoding.ASCII.GetBytes("fmt "));
                writer.Write(16);
                writer.Write((short)1); // PCM
                writer.Write((short)1); // one channel
                writer.Write(frequency);
                writer.Write(frequency * (bits / 8));
                writer.Write((short)(bits / 8));
                writer.Write(bits);
                writer.Write(Encoding.ASCII.GetBytes("data"));
                writer.Write(dataBytes);
                foreach (var sample in samples)
                    writer.Write((short)Math.Round(Math.Max(-1f, Math.Min(1f, sample)) * short.MaxValue));
            }
        }
    }
}
