namespace MidiaScraper.Services.YtDlp
{
    public enum YtDlpLocateOutcome
    {
        FoundExisting,
        Downloaded,
        NotFound
    }

    public class YtDlpLocateResult
    {
        public required YtDlpLocateOutcome Outcome { get; init; }
        public string Path { get; init; } = string.Empty;
    }

    public interface IYtDlpLocator
    {
        /// <summary>
        /// Finds yt-dlp on PATH, next to the executable, or downloads it automatically.
        /// Logs progress through <paramref name="log"/> as it goes.
        /// </summary>
        Task<YtDlpLocateResult> EnsureAsync(Action<string> log);
    }
}
