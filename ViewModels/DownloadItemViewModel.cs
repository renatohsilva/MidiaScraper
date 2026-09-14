using CommunityToolkit.Mvvm.ComponentModel;

namespace MidiaScraper.ViewModels
{
    public partial class DownloadItemViewModel : ObservableObject
    {
        public required string Id { get; init; }
        public required string Title { get; init; }
        public string? ThumbnailUrl { get; init; }
        public double? DurationSeconds { get; init; }
        public string? WatchUrl { get; init; }

        [ObservableProperty]
        private bool isSelected = true;

        [ObservableProperty]
        private string status = "Pendente";

        [ObservableProperty]
        private double progressPercent;
    }
}
