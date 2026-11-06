using System;
using System.Collections.Generic;
using Forge.Data;
using Forge.Data.Records;

namespace Forge.Editing.Placement
{
    /// <summary>
    /// The user's placement settings and what they mean for each base object: whether new objects get their
    /// random turn and size, whether they tilt to the slope, and the gaps in rows (remembered per base object;
    /// the editor keeps them between sessions). The place tool and the world-building commands share one instance, so the agent
    /// places things the way the user has set placing up.
    /// </summary>
    public sealed class PlacementOptions
    {
        readonly Random _random;
        readonly Dictionary<RecordId, RowGap> _gaps = new Dictionary<RecordId, RowGap>();

        public PlacementOptions(Random random)
        {
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        /// <summary>Whether new objects get their base object's random variation (turn and size).</summary>
        public bool Randomize { get; set; } = true;

        /// <summary>Whether objects whose rules ask for it are tilted to the slope when placed, dropped or sculpted under.</summary>
        public bool AlignToSlope { get; set; } = true;

        /// <summary>A fresh variation for a new instance of <paramref name="baseObject"/>; none while <see cref="Randomize"/> is off.</summary>
        public PlacementRoll DrawRoll(BaseObjectRecord baseObject) =>
            Randomize ? PlacementRoll.Draw(baseObject?.Placement, _random) : PlacementRoll.Identity;

        /// <summary>Whether new instances get a random heading, so a row keeps it instead of following its path.</summary>
        public bool KeepsOwnHeading(BaseObjectRecord baseObject) => Randomize && (baseObject?.Placement?.RandomYaw ?? false);

        /// <summary>Whether instances are tilted to the slope: their rules ask for it and <see cref="AlignToSlope"/> is on.</summary>
        public bool TiltsToSlope(BaseObjectRecord baseObject) => AlignToSlope && (baseObject?.Placement?.AlignToSlope ?? false);

        /// <summary>The row gap set for <paramref name="baseObject"/>, or its default.</summary>
        public RowGap GapFor(BaseObjectRecord baseObject)
        {
            if (baseObject == null)
                throw new ArgumentNullException(nameof(baseObject));
            return _gaps.TryGetValue(baseObject.Id, out var gap) ? gap : RowGap.DefaultFor(baseObject.Placement);
        }

        public void SetGap(BaseObjectRecord baseObject, RowGap gap)
        {
            if (baseObject == null)
                throw new ArgumentNullException(nameof(baseObject));
            _gaps[baseObject.Id] = gap;
        }

        /// <summary>The gaps set so far, by base object id (objects never set keep their default and aren't listed).</summary>
        public IReadOnlyDictionary<RecordId, RowGap> Gaps => _gaps;

        /// <summary>Sets the gap of <paramref name="baseId"/>, e.g. as kept from an earlier session.</summary>
        public void SetGap(RecordId baseId, RowGap gap) => _gaps[baseId] = gap;

        /// <summary>One gap drawn from <paramref name="gap"/>.</summary>
        public float DrawGap(RowGap gap) => gap.Draw(_random);
    }
}
