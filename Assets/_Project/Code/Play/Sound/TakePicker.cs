using System;

namespace Forge.Play.Sound
{
    /// <summary>
    /// Which of a cue's takes plays next: any at random but the one just played, so a step or a blow never sounds
    /// twice alike in a row. One per cue. Pure, so testable.
    /// </summary>
    public sealed class TakePicker
    {
        readonly Random _random;
        int _last = -1;

        public TakePicker(Random random) => _random = random ?? throw new ArgumentNullException(nameof(random));

        /// <summary>The take to play of <paramref name="count"/>; -1 when there are none.</summary>
        public int Next(int count)
        {
            if (count <= 0)
                return -1;
            if (count == 1)
                return _last = 0;
            // One of the others: a draw over count - 1, stepping past the last one.
            var next = _random.Next(count - 1);
            if (_last >= 0 && _last < count && next >= _last)
                next++;
            return _last = next;
        }
    }
}
