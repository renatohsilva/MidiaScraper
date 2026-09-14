using MidiaScraper.Models;

namespace MidiaScraper.Services.YtDlp
{
    public interface IMediaMetadataProvider
    {
        /// <summary>
        /// Fetches title/thumbnail/duration (and playlist entries, if any) without downloading
        /// anything. Returns null if metadata could not be obtained — callers should fall back to
        /// downloading directly rather than blocking on a preview that may not be available for
        /// every site.
        /// </summary>
        /// <param name="includePlaylist">
        /// Must mirror the same flag passed to <see cref="YtDlpArgumentBuilder"/> for the actual
        /// download. When false, a URL that also carries a playlist/mix id (e.g. a YouTube "Radio"
        /// autoplay mix) is queried as a single video only — otherwise yt-dlp expands it into the
        /// full (possibly hundreds of entries) playlist just to preview a video that will be
        /// downloaded alone anyway, which can be slow enough to blow past the fetch timeout.
        /// </param>
        Task<MediaMetadata?> FetchAsync(string ytdlpPath, string url, bool includePlaylist, CancellationToken ct);
    }
}
