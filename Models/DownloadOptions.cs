namespace MidiaScraper.Models
{
    public enum DownloadFormat
    {
        Best,
        Video1080,
        Video720,
        Video480,
        AudioOnly
    }

    public class DownloadOptions
    {
        public required string Url { get; init; }
        public required string OutputFolder { get; init; }
        public DownloadFormat Format { get; init; } = DownloadFormat.Best;
        public bool DownloadSubtitles { get; init; }
        public bool DownloadPlaylist { get; init; }
    }
}
