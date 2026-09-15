using System.Globalization;
using MidiaScraper.Models;

namespace MidiaScraper.Services.YtDlp
{
    public static class YtDlpProgressParser
    {
        /// <summary>
        /// Marks a structured progress line emitted via our own --progress-template (see
        /// <see cref="YtDlpArgumentBuilder"/>), so it can never be confused with yt-dlp's normal
        /// textual output — replaces the free-text regex that used to parse "[download] X% of Y at Z".
        /// </summary>
        public const string ProgressMarker = "MSPROGRESS";

        /// <summary>ASCII Unit Separator — won't appear in yt-dlp's formatted progress fields.</summary>
        public const char FieldSeparator = '';

        private static readonly string ProgressPrefix = ProgressMarker + FieldSeparator;

        /// <summary>
        /// Parses one line of yt-dlp stdout. Returns null for lines that produce no visible output
        /// (matches the original catch-all whitespace guard).
        /// </summary>
        public static DownloadProgressInfo? Parse(string line)
        {
            if (line.StartsWith(ProgressPrefix))
            {
                string[] parts = line.Split(FieldSeparator);
                if (parts.Length >= 5)
                {
                    string percentText = parts[1].Trim();
                    string sizeText = parts[2].Trim();
                    string speedText = parts[3].Trim();
                    string etaText = parts[4].Trim();

                    double? percent = double.TryParse(
                        percentText.TrimEnd('%').Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double pct)
                        ? pct
                        : null;

                    return new DownloadProgressInfo
                    {
                        Kind = DownloadLineKind.Progress,
                        RawLine = line,
                        Percent = percent,
                        SizeText = sizeText,
                        SpeedText = speedText,
                        Eta = etaText
                    };
                }
            }

            if (line.StartsWith("[download] Destination:") || line.StartsWith("[Merger]") ||
                line.Contains("has already been downloaded") || line.StartsWith("[ExtractAudio]"))
            {
                return new DownloadProgressInfo
                {
                    Kind = DownloadLineKind.Destination,
                    RawLine = line,
                    FilePath = ExtractFilePath(line)
                };
            }

            if (line.StartsWith("[youtube]") || line.StartsWith("[twitter]") ||
                line.StartsWith("[generic]") || line.StartsWith("[info]") ||
                line.StartsWith("[VideoConvertor]") || line.StartsWith("[ffmpeg]"))
            {
                return new DownloadProgressInfo { Kind = DownloadLineKind.Info, RawLine = line };
            }

            if (line.StartsWith("WARNING:") || line.StartsWith("ERROR:"))
            {
                return new DownloadProgressInfo { Kind = DownloadLineKind.Warning, RawLine = line };
            }

            if (!string.IsNullOrWhiteSpace(line))
                return new DownloadProgressInfo { Kind = DownloadLineKind.Raw, RawLine = line };

            return null;
        }

        /// <summary>
        /// Best-effort extraction of the destination file path from a "[download] Destination:",
        /// "[Merger] Merging formats into ...", "[ExtractAudio] Destination:" or "... has already
        /// been downloaded" line. Returns null if the line doesn't match a known shape.
        /// </summary>
        private static string? ExtractFilePath(string line)
        {
            const string destinationPrefix = "Destination: ";
            int destinationIndex = line.IndexOf(destinationPrefix, StringComparison.Ordinal);
            if (destinationIndex >= 0)
                return line[(destinationIndex + destinationPrefix.Length)..].Trim();

            if (line.StartsWith("[Merger]"))
            {
                int firstQuote = line.IndexOf('"');
                int lastQuote = line.LastIndexOf('"');
                if (firstQuote >= 0 && lastQuote > firstQuote)
                    return line[(firstQuote + 1)..lastQuote];
            }

            const string alreadyDownloadedSuffix = " has already been downloaded";
            int suffixIndex = line.IndexOf(alreadyDownloadedSuffix, StringComparison.Ordinal);
            if (suffixIndex > 0)
            {
                string candidate = line[..suffixIndex];
                const string downloadPrefix = "[download] ";
                if (candidate.StartsWith(downloadPrefix))
                    candidate = candidate[downloadPrefix.Length..];
                return candidate.Trim();
            }

            return null;
        }
    }
}
