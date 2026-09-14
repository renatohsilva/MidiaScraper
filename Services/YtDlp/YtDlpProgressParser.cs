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
                return new DownloadProgressInfo { Kind = DownloadLineKind.Destination, RawLine = line };
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
    }
}
