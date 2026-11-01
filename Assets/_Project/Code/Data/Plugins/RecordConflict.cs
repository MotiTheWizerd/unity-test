namespace Forge.Data.Plugins
{
    /// <summary>
    /// A record that <see cref="WinningPluginId"/> overwrote without declaring
    /// <see cref="OverwrittenPluginId"/> as a master, so the earlier change was
    /// discarded by a plugin that did not know about it.
    /// </summary>
    public sealed class RecordConflict
    {
        public RecordConflict(RecordId recordId, string overwrittenPluginId, string winningPluginId)
        {
            RecordId = recordId;
            OverwrittenPluginId = overwrittenPluginId;
            WinningPluginId = winningPluginId;
        }

        public RecordId RecordId { get; }

        public string OverwrittenPluginId { get; }

        public string WinningPluginId { get; }

        public override string ToString() =>
            $"'{RecordId}': '{WinningPluginId}' overwrites '{OverwrittenPluginId}' without depending on it.";
    }
}
