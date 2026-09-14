using MidiaScraper.Models;

namespace MidiaScraper.Services.YtDlp
{
    public class DownloadResult
    {
        public required bool Success { get; init; }
        public int ExitCode { get; init; }
    }

    public interface IMediaDownloader
    {
        Task<DownloadResult> DownloadAsync(
            string ytdlpPath,
            List<string> args,
            IProgress<DownloadProgressInfo> progress,
            CancellationToken ct);
    }
}
