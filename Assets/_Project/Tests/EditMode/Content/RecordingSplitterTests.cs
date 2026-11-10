using System.Linq;
using Forge.UnityEditor.Content;
using NUnit.Framework;

namespace Forge.Tests.Content
{
    public class RecordingSplitterTests
    {
        // A loudness over frames: `quiet` everywhere, with a burst of `loud` falling away over `length` frames at each start.
        static float[] Bursts(int frames, float quiet, float loud, int length, params int[] starts)
        {
            var envelope = Enumerable.Repeat(quiet, frames).ToArray();
            foreach (var start in starts)
            {
                for (var i = 0; i < length && start + i < frames; i++)
                    envelope[start + i] = loud * (1f - (float)i / length) + quiet;
            }

            return envelope;
        }

        [Test]
        public void ARunOfSteps_SplitsIntoOneSoundAStep()
        {
            var sounds = RecordingSplitter.Split(Bursts(300, 0.001f, 0.2f, 20, 10, 60, 110, 160), minGap: 15, longest: 80);

            Assert.That(sounds.Select(s => s.Start), Is.EqualTo(new[] { 10, 60, 110, 160 }));
            Assert.That(sounds.All(s => s.End > s.Start + 5 && s.End <= s.Start + 25), Is.True);
        }

        [Test]
        public void ASteadyNoiseUnderneath_StillSplits()
        {
            var sounds = RecordingSplitter.Split(Bursts(200, 0.05f, 0.1f, 15, 20, 70, 120), minGap: 15, longest: 80);

            Assert.That(sounds.Count, Is.EqualTo(3));
        }

        [Test]
        public void SoundsCloserThanTheGap_AreOne()
        {
            var sounds = RecordingSplitter.Split(Bursts(200, 0.001f, 0.2f, 6, 20, 28), minGap: 15, longest: 80);

            Assert.That(sounds.Count, Is.EqualTo(1));
        }

        [Test]
        public void ASoundEnds_WhereTheNextBegins_AndIsNoLongerThanTheLongest()
        {
            var sounds = RecordingSplitter.Split(Bursts(300, 0.001f, 0.2f, 90, 10, 60), minGap: 15, longest: 40);

            Assert.That(sounds[0].End, Is.LessThanOrEqualTo(sounds[1].Start));
            Assert.That(sounds.All(s => s.End - s.Start <= 40), Is.True);
        }

        [Test]
        public void Silence_HasNoSounds()
        {
            Assert.That(RecordingSplitter.Split(new float[100], 15, 80), Is.Empty);
            Assert.That(RecordingSplitter.Split(new float[0], 15, 80), Is.Empty);
        }

        [Test]
        public void TheEnvelope_IsEachFramesLoudness()
        {
            var envelope = RecordingSplitter.Envelope(new[] { 1f, -1f, 0f, 0f, 0.5f, 0.5f }, channels: 2, frameSize: 1);

            Assert.That(envelope, Is.EqualTo(new[] { 1f, 0f, 0.5f }).Within(1e-6f));
        }
    }
}
