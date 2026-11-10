using System;
using System.Collections.Generic;
using System.Linq;

namespace Forge.UnityEditor.Content
{
    /// <summary>
    /// Finds the separate sounds in a recording of several (a run of footsteps, a string of blows), so each plays as a
    /// take of its own: over the recording's loudness, frame by frame (<see cref="Envelope"/>), a sound is one that rises
    /// well above the recording's quiet, kept from its soft start to its faded tail, the next starting no sooner than
    /// <c>minGap</c> frames after. Measured against the recording's own quiet and loudest, so a hushed recording and a
    /// loud one split alike, and one with a steady noise under it (a run's) still does. Pure, so testable.
    /// </summary>
    static class RecordingSplitter
    {
        // A sound is heard past this share of the way from the recording's quiet up to its loudest, and kept from where it
        // first rose past the second (its soft start) to where it fell back under it (its fading tail).
        const float Rise = 0.4f;
        const float Edge = 0.05f;

        // The quiet is the loudness the recording stays above this share of the time.
        const float QuietShare = 0.2f;

        // Frames kept after a sound falls quiet (its ring).
        const int Tail = 3;

        /// <summary>
        /// How loud <paramref name="samples"/> (interleaved over <paramref name="channels"/>) are over each frame of
        /// <paramref name="frameSize"/> samples a channel: their root mean square.
        /// </summary>
        public static float[] Envelope(float[] samples, int channels, int frameSize)
        {
            if (samples == null)
                throw new ArgumentNullException(nameof(samples));
            if (channels < 1 || frameSize < 1)
                throw new ArgumentOutOfRangeException(channels < 1 ? nameof(channels) : nameof(frameSize));
            var frames = samples.Length / channels / frameSize;
            var envelope = new float[frames];
            for (var frame = 0; frame < frames; frame++)
            {
                var sum = 0.0;
                var from = frame * frameSize * channels;
                for (var i = from; i < from + frameSize * channels; i++)
                    sum += samples[i] * samples[i];
                envelope[frame] = (float)Math.Sqrt(sum / (frameSize * channels));
            }

            return envelope;
        }

        /// <summary>
        /// The sounds in <paramref name="envelope"/>, as frame ranges [Start, End): each starting at least
        /// <paramref name="minGap"/> frames after the last began, and lasting at most <paramref name="longest"/>; one
        /// shorter than <paramref name="shortest"/> is a scuff, not a sound, and is left out.
        /// </summary>
        public static IReadOnlyList<(int Start, int End)> Split(float[] envelope, int minGap, int longest, int shortest = 3)
        {
            if (envelope == null)
                throw new ArgumentNullException(nameof(envelope));
            var sounds = new List<(int Start, int End)>();
            if (envelope.Length == 0)
                return sounds;

            var loudest = envelope.Max();
            var quiet = envelope.OrderBy(x => x).ElementAt((int)(envelope.Length * QuietShare));
            var on = quiet + Rise * (loudest - quiet);
            var edge = quiet + Edge * (loudest - quiet);
            if (!(loudest > quiet))
                return sounds;

            var lastStart = int.MinValue / 2;
            var lastEnd = 0;
            for (var i = 0; i < envelope.Length; i++)
            {
                if (envelope[i] < on || i - lastStart < minGap)
                    continue;

                // It began where it first rose out of the quiet, no further back than the last one's end.
                var start = i;
                while (start > lastEnd && envelope[start - 1] > edge)
                    start--;
                var end = i;
                while (end < envelope.Length && envelope[end] >= edge)
                    end++;
                end = Math.Min(Math.Min(end + Tail, envelope.Length), start + longest);

                if (end - start >= shortest)
                    sounds.Add((start, end));
                lastStart = i;
                lastEnd = end;
                i = Math.Max(i, end - 1);
            }

            // A sound ends where the next begins.
            for (var k = 0; k < sounds.Count - 1; k++)
            {
                if (sounds[k].End > sounds[k + 1].Start)
                    sounds[k] = (sounds[k].Start, sounds[k + 1].Start);
            }

            return sounds;
        }
    }
}
