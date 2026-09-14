namespace MidiaScraper.Models
{
    public class MediaEntry
    {
        public required string Id { get; init; }
        public required string Title { get; init; }
        public string? ThumbnailUrl { get; init; }
        public double? DurationSeconds { get; init; }
        public string? WatchUrl { get; init; }
    }

    public class MediaMetadata
    {
        public required string Id { get; init; }
        public required string Title { get; init; }
        public string? ThumbnailUrl { get; init; }
        public double? DurationSeconds { get; init; }
        public bool IsPlaylist { get; init; }
        public IReadOnlyList<MediaEntry> Entries { get; init; } = Array.Empty<MediaEntry>();
    }
}
