using System.Diagnostics;
using System.Text;
using System.Text.Json;
using MidiaScraper.Models;

namespace MidiaScraper.Services.YtDlp
{
    public class YtDlpMetadataProvider : IMediaMetadataProvider
    {
        private static readonly TimeSpan FetchTimeout = TimeSpan.FromSeconds(15);

        public async Task<MediaMetadata?> FetchAsync(string ytdlpPath, string url, bool includePlaylist, CancellationToken ct)
        {
            var psi = new ProcessStartInfo
            {
                FileName = ytdlpPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            psi.ArgumentList.Add("--dump-json");
            psi.ArgumentList.Add("--flat-playlist");
            psi.ArgumentList.Add("--simulate");
            psi.ArgumentList.Add("--no-warnings");
            if (!includePlaylist)
                psi.ArgumentList.Add("--no-playlist");
            psi.ArgumentList.Add(url);

            using var process = new Process { StartInfo = psi };
            var lines = new List<string>();
            process.OutputDataReceived += (_, e) => { if (e.Data != null) lines.Add(e.Data); };

            using var timeoutCts = new CancellationTokenSource(FetchTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            try
            {
                process.Start();
                process.BeginOutputReadLine();
                await process.WaitForExitAsync(linkedCts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // Só o timeout interno de busca de metadados estourou (não o cancelamento do
                // usuário) — trata como falha de metadados, não de download.
                return null;
            }
            finally
            {
                if (!process.HasExited)
                {
                    try { process.Kill(true); } catch { }
                }
            }

            if (process.ExitCode != 0 || lines.Count == 0)
                return null;

            var entries = new List<MediaEntry>();
            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    using var doc = JsonDocument.Parse(line);
                    entries.Add(ParseEntry(doc.RootElement));
                }
                catch (JsonException)
                {
                    // Linha não-JSON eventualmente misturada ao stdout — ignora.
                }
            }

            if (entries.Count == 0)
                return null;

            if (entries.Count == 1)
            {
                var single = entries[0];
                return new MediaMetadata
                {
                    Id = single.Id,
                    Title = single.Title,
                    ThumbnailUrl = single.ThumbnailUrl,
                    DurationSeconds = single.DurationSeconds,
                    IsPlaylist = false,
                    Entries = entries
                };
            }

            string playlistTitle = TryGetPlaylistTitle(lines[0]) ?? $"Playlist ({entries.Count} itens)";
            return new MediaMetadata
            {
                Id = playlistTitle,
                Title = playlistTitle,
                ThumbnailUrl = entries[0].ThumbnailUrl,
                DurationSeconds = null,
                IsPlaylist = true,
                Entries = entries
            };
        }

        private static MediaEntry ParseEntry(JsonElement root) => new()
        {
            Id = GetString(root, "id") ?? Guid.NewGuid().ToString(),
            Title = GetString(root, "title") ?? "(sem título)",
            ThumbnailUrl = GetString(root, "thumbnail") ?? GetBestThumbnailFromArray(root),
            DurationSeconds = GetDouble(root, "duration"),
            WatchUrl = GetString(root, "webpage_url") ?? GetString(root, "url")
        };

        /// <summary>
        /// Alguns extractors só populam o array "thumbnails" (várias resoluções), sem duplicar o
        /// campo "thumbnail" singular usado acima. yt-dlp ordena esse array da menor para a maior
        /// resolução, então a última entrada é normalmente a de melhor qualidade disponível.
        /// </summary>
        private static string? GetBestThumbnailFromArray(JsonElement root)
        {
            if (!root.TryGetProperty("thumbnails", out var thumbnails) || thumbnails.ValueKind != JsonValueKind.Array)
                return null;

            string? last = null;
            foreach (var item in thumbnails.EnumerateArray())
            {
                string? url = GetString(item, "url");
                if (url != null) last = url;
            }
            return last;
        }

        private static string? TryGetPlaylistTitle(string firstLine)
        {
            try
            {
                using var doc = JsonDocument.Parse(firstLine);
                return GetString(doc.RootElement, "playlist_title") ?? GetString(doc.RootElement, "playlist");
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static string? GetString(JsonElement root, string property) =>
            root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

        private static double? GetDouble(JsonElement root, string property) =>
            root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
                ? value.GetDouble()
                : null;
    }
}
