using System.Text.Json.Serialization;

namespace MidiaScraper.Models
{
    public class DownloadHistoryEntry
    {
        public required string Url { get; init; }
        public required string Title { get; init; }
        public required DateTimeOffset CompletedAt { get; init; }
        public required string Status { get; init; }

        /// <summary>
        /// Identificador estável da mídia (ex.: id do vídeo no YouTube), quando disponível via
        /// metadados — usado para detecção de duplicados em vez de comparar por URL/nome de
        /// arquivo, que pode variar (parâmetros de query, playlist embutida, etc.).
        /// </summary>
        public string? MediaId { get; init; }

        [JsonIgnore]
        public string CompletedAtDisplay => CompletedAt.ToLocalTime().ToString("dd/MM HH:mm");
    }
}
