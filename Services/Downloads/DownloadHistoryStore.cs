using System.IO;
using System.Linq;
using System.Text.Json;
using MidiaScraper.Models;

namespace MidiaScraper.Services.Downloads
{
    public class DownloadHistoryStore
    {
        private readonly string _filePath;

        public DownloadHistoryStore()
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MidiaScraper");
            Directory.CreateDirectory(dir);
            _filePath = Path.Combine(dir, "history.json");
        }

        /// <summary>
        /// Loads the persisted history. A missing file (first run) returns an empty list silently;
        /// a corrupted/unreadable file also returns an empty list, but is reported via <paramref name="log"/>
        /// instead of crashing the app.
        /// </summary>
        public async Task<List<DownloadHistoryEntry>> LoadAsync(Action<string>? log = null)
        {
            if (!File.Exists(_filePath))
                return new List<DownloadHistoryEntry>();

            try
            {
                string json = await File.ReadAllTextAsync(_filePath);
                var entries = JsonSerializer.Deserialize<List<DownloadHistoryEntry>>(json);
                return entries ?? new List<DownloadHistoryEntry>();
            }
            catch (Exception ex)
            {
                log?.Invoke($"⚠️  Histórico de downloads corrompido ou ilegível ({ex.Message}); iniciando com histórico vazio.");
                return new List<DownloadHistoryEntry>();
            }
        }

        public async Task SaveAsync(IReadOnlyList<DownloadHistoryEntry> entries)
        {
            string json = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_filePath, json);
        }

        /// <summary>
        /// Verifica se uma mídia já foi baixada com sucesso antes. Prioriza o identificador estável
        /// (<paramref name="mediaId"/>, ex.: id do vídeo) quando disponível — comparar por URL é
        /// frágil (parâmetros de query variam, a mesma mídia pode aparecer em playlists diferentes).
        /// Cai para comparação por URL apenas quando o id não está disponível (ex.: fila manual, sem
        /// metadados buscados).
        /// </summary>
        public static bool IsAlreadyDownloaded(IEnumerable<DownloadHistoryEntry> history, string? mediaId, string url) =>
            history.Any(entry =>
                entry.Status == "Concluído" &&
                (mediaId != null
                    ? entry.MediaId == mediaId
                    : string.Equals(entry.Url, url, StringComparison.OrdinalIgnoreCase)));
    }
}
