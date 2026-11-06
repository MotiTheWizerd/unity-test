using System;
using System.Collections.Generic;

namespace Forge.Agent
{
    /// <summary>
    /// A picture sent to the agent with a message: the encoded file (PNG, JPEG, GIF or WebP) as it is. Whoever
    /// makes it keeps it small: the model sees no more than about 1568 px on the long edge anyway.
    /// </summary>
    public sealed class AgentImage
    {
        /// <summary>The most one picture may weigh; the model refuses more than 5 MB once it is base64.</summary>
        public const int MaxBytes = 3_750_000;

        /// <summary>The longest edge worth sending: the model scales anything larger down to about this.</summary>
        public const int MaxEdge = 1568;

        /// <summary>The size to send a <paramref name="width"/> × <paramref name="height"/> picture at: as it is, or shrunk to <see cref="MaxEdge"/>.</summary>
        public static (int Width, int Height) Fit(int width, int height)
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), "A picture has a size.");
            var longest = Math.Max(width, height);
            if (longest <= MaxEdge)
                return (width, height);
            var scale = (double)MaxEdge / longest;
            return (Math.Max(1, (int)Math.Round(width * scale)), Math.Max(1, (int)Math.Round(height * scale)));
        }

        static readonly HashSet<string> MediaTypes = new HashSet<string> { "image/png", "image/jpeg", "image/gif", "image/webp" };

        public AgentImage(string mediaType, byte[] bytes)
        {
            if (!MediaTypes.Contains(mediaType ?? string.Empty))
                throw new ArgumentException($"The agent can't see '{mediaType}' pictures; send PNG, JPEG, GIF or WebP.", nameof(mediaType));
            if (bytes == null || bytes.Length == 0)
                throw new ArgumentException("The picture is empty.", nameof(bytes));
            if (bytes.Length > MaxBytes)
                throw new ArgumentException($"The picture is {bytes.Length / 1_000_000f:0.0} MB; the agent takes up to {MaxBytes / 1_000_000f:0.0} MB.", nameof(bytes));

            MediaType = mediaType;
            Bytes = bytes;
        }

        public string MediaType { get; }

        public byte[] Bytes { get; }

        public static readonly IReadOnlyList<AgentImage> None = Array.Empty<AgentImage>();
    }
}
