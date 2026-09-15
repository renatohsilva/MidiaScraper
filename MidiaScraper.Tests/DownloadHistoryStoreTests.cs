using MidiaScraper.Models;
using MidiaScraper.Services.Downloads;

namespace MidiaScraper.Tests;

public class DownloadHistoryStoreTests
{
    private static DownloadHistoryEntry MakeEntry(string url, string status, string? mediaId = null) => new()
    {
        Url = url,
        Title = "Título",
        CompletedAt = DateTimeOffset.Now,
        Status = status,
        MediaId = mediaId
    };

    [Fact]
    public void IsAlreadyDownloaded_MatchingMediaIdAndCompleted_ReturnsTrue()
    {
        var history = new[] { MakeEntry("https://example.com/a", "Concluído", mediaId: "abc123") };

        bool result = DownloadHistoryStore.IsAlreadyDownloaded(history, "abc123", "https://example.com/a?different-query=1");

        Assert.True(result);
    }

    [Fact]
    public void IsAlreadyDownloaded_DifferentMediaId_ReturnsFalseEvenIfUrlMatches()
    {
        var history = new[] { MakeEntry("https://example.com/a", "Concluído", mediaId: "abc123") };

        bool result = DownloadHistoryStore.IsAlreadyDownloaded(history, "xyz789", "https://example.com/a");

        Assert.False(result);
    }

    [Fact]
    public void IsAlreadyDownloaded_NoMediaIdGiven_FallsBackToUrlComparison()
    {
        var history = new[] { MakeEntry("https://example.com/a", "Concluído") };

        Assert.True(DownloadHistoryStore.IsAlreadyDownloaded(history, null, "https://example.com/a"));
        Assert.True(DownloadHistoryStore.IsAlreadyDownloaded(history, null, "HTTPS://EXAMPLE.COM/A"));
        Assert.False(DownloadHistoryStore.IsAlreadyDownloaded(history, null, "https://example.com/b"));
    }

    [Fact]
    public void IsAlreadyDownloaded_OnlyFailedOrCancelledAttempts_ReturnsFalse()
    {
        var history = new[]
        {
            MakeEntry("https://example.com/a", "Falhou", mediaId: "abc123"),
            MakeEntry("https://example.com/a", "Cancelado", mediaId: "abc123")
        };

        bool result = DownloadHistoryStore.IsAlreadyDownloaded(history, "abc123", "https://example.com/a");

        Assert.False(result);
    }

    [Fact]
    public void IsAlreadyDownloaded_EmptyHistory_ReturnsFalse()
    {
        Assert.False(DownloadHistoryStore.IsAlreadyDownloaded(Array.Empty<DownloadHistoryEntry>(), "abc123", "https://example.com/a"));
    }
}
