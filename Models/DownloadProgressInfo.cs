namespace MidiaScraper.Models
{
    public enum DownloadLineKind
    {
        Progress,
        Destination,
        Info,
        Warning,
        Raw,
        Retry
    }

    public class DownloadProgressInfo
    {
        public required DownloadLineKind Kind { get; init; }
        public required string RawLine { get; init; }
        public double? Percent { get; init; }
        public string? SizeText { get; init; }
        public string? SpeedText { get; init; }
        public string? Eta { get; init; }
    }
}
