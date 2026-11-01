using System;

namespace Forge.Editing.Terrain
{
    /// <summary>Commits terrain work to the session.</summary>
    public static class TerrainEdits
    {
        /// <summary>Writes every chunk the stroke changed as one undo step. Returns the number of chunks written.</summary>
        public static int Commit(EditSession session, SculptStroke stroke)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (stroke == null)
                throw new ArgumentNullException(nameof(stroke));

            var records = stroke.ChangedRecords();
            if (records.Count == 0)
                return 0;

            session.Batch($"Sculpt ({stroke.Brush.Mode})", () =>
            {
                foreach (var record in records)
                    session.Put(record);
            });
            return records.Count;
        }
    }
}
