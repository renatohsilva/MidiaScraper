using System.Text.Json.Serialization;

namespace MidiaScraper.Models
{
    public class DownloadHistoryEntry
    {
        public required string Url { get; init; }
        public required string Title { get; init; }
        public required DateTimeOffset CompletedAt { get; init; }
        public required string Status { get; init; }

        [JsonIgnore]
        public string CompletedAtDisplay => CompletedAt.ToLocalTime().ToString("dd/MM HH:mm");
    }
}
