using System.IO;

namespace MidiaScraper.Models
{
    public class AppSettings
    {
        public string OutputFolder { get; set; } =
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + Path.DirectorySeparatorChar + "Downloads";

        public DownloadFormat DefaultFormat { get; set; } = DownloadFormat.Best;
        public bool DefaultSubtitles { get; set; }
        public bool DefaultPlaylist { get; set; }
        public string? RateLimit { get; set; }

        /// <summary>Reservado para a Fase 4 (download paralelo configurável); não usado ainda.</summary>
        public int MaxConcurrentDownloads { get; set; } = 1;
    }
}
