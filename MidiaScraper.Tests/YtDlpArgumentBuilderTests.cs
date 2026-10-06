using MidiaScraper.Models;
using MidiaScraper.Services.YtDlp;

namespace MidiaScraper.Tests;

public class YtDlpArgumentBuilderTests
{
    private static DownloadOptions MakeOptions(
        string url = "https://example.com/video",
        DownloadFormat format = DownloadFormat.Best,
        bool subtitles = false,
        bool playlist = false,
        string? rateLimit = null) => new()
    {
        Url = url,
        OutputFolder = @"C:\Downloads",
        Format = format,
        DownloadSubtitles = subtitles,
        DownloadPlaylist = playlist,
        RateLimit = rateLimit
    };

    [Fact]
    public void Build_UrlContainingQuoteAndFlagLikeText_KeptAsSingleUnmodifiedToken()
    {
        // Regression test for the argument-injection fix (Passo 1.1): before, the URL was
        // interpolated into a single command-line string, so a `"` inside it could terminate
        // the string early and let the rest be parsed as new yt-dlp flags (e.g. --exec).
        // ArgumentList passes each element as one atomic OS-level argument, so the whole
        // string — quotes included — must survive as exactly one token, never split.
        const string maliciousUrl = "https://example.com/video\" --exec \"calc.exe";
        var options = MakeOptions(url: maliciousUrl);

        var args = YtDlpArgumentBuilder.Build(options);

        Assert.Equal(maliciousUrl, args[^1]);
        Assert.DoesNotContain(args.Take(args.Count - 1), a => a.Contains("--exec") || a.Contains("calc.exe"));
    }

    [Fact]
    public void Build_BestFormat_UsesBestVideoAudioAndMp4Merge()
    {
        var args = YtDlpArgumentBuilder.Build(MakeOptions(format: DownloadFormat.Best));

        Assert.Contains("-f", args);
        Assert.Contains("bestvideo+bestaudio/best", args);
        Assert.Contains("--merge-output-format", args);
        Assert.Contains("mp4", args);
    }

    [Fact]
    public void Build_AudioOnly_UsesExtractAudioFlags()
    {
        var args = YtDlpArgumentBuilder.Build(MakeOptions(format: DownloadFormat.AudioOnly));

        Assert.Contains("-x", args);
        Assert.Contains("--audio-format", args);
        Assert.Contains("mp3", args);
        Assert.DoesNotContain("--merge-output-format", args);
    }

    [Fact]
    public void Build_AudioOnly_RestrictsFormatSelectorToAudioTrack()
    {
        // Regression test: sem "-f bestaudio/best", o yt-dlp baixava vídeo+áudio completo
        // e só descartava o vídeo depois da extração, desperdiçando banda e tempo.
        var args = YtDlpArgumentBuilder.Build(MakeOptions(format: DownloadFormat.AudioOnly));

        int formatIndex = args.IndexOf("-f");
        Assert.True(formatIndex >= 0);
        Assert.Equal("bestaudio/best", args[formatIndex + 1]);
    }

    [Theory]
    [InlineData(DownloadFormat.Video1080, "1080")]
    [InlineData(DownloadFormat.Video720, "720")]
    [InlineData(DownloadFormat.Video480, "480")]
    public void Build_HeightConstrainedFormats_EmbedsHeightInFormatSelector(DownloadFormat format, string expectedHeight)
    {
        var args = YtDlpArgumentBuilder.Build(MakeOptions(format: format));

        int formatIndex = args.IndexOf("-f");
        Assert.True(formatIndex >= 0);
        Assert.Contains($"height<={expectedHeight}", args[formatIndex + 1]);
    }

    [Fact]
    public void Build_WithSubtitles_AddsSubtitleFlags()
    {
        var args = YtDlpArgumentBuilder.Build(MakeOptions(subtitles: true));

        Assert.Contains("--write-auto-sub", args);
        Assert.Contains("--sub-lang", args);
        Assert.Contains("pt,en", args);
        Assert.Contains("--convert-subs", args);
        Assert.Contains("srt", args);
    }

    [Fact]
    public void Build_WithoutSubtitles_OmitsSubtitleFlags()
    {
        var args = YtDlpArgumentBuilder.Build(MakeOptions(subtitles: false));

        Assert.DoesNotContain("--write-auto-sub", args);
    }

    [Fact]
    public void Build_PlaylistFalse_AddsNoPlaylistFlag()
    {
        var args = YtDlpArgumentBuilder.Build(MakeOptions(playlist: false));

        Assert.Contains("--no-playlist", args);
    }

    [Fact]
    public void Build_PlaylistTrue_OmitsNoPlaylistFlag()
    {
        var args = YtDlpArgumentBuilder.Build(MakeOptions(playlist: true));

        Assert.DoesNotContain("--no-playlist", args);
    }

    [Fact]
    public void Build_OutputTemplate_UsesGivenOutputFolder()
    {
        var args = YtDlpArgumentBuilder.Build(MakeOptions());

        int outputIndex = args.IndexOf("-o");
        Assert.True(outputIndex >= 0);
        Assert.StartsWith(@"C:\Downloads", args[outputIndex + 1]);
    }

    [Fact]
    public void Build_WithRateLimit_AddsLimitRateFlag()
    {
        var args = YtDlpArgumentBuilder.Build(MakeOptions(rateLimit: "1M"));

        int limitIndex = args.IndexOf("--limit-rate");
        Assert.True(limitIndex >= 0);
        Assert.Equal("1M", args[limitIndex + 1]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Build_WithoutRateLimit_OmitsLimitRateFlag(string? rateLimit)
    {
        var args = YtDlpArgumentBuilder.Build(MakeOptions(rateLimit: rateLimit));

        Assert.DoesNotContain("--limit-rate", args);
    }

    [Fact]
    public void Build_AlwaysEndsWithUrlAsLastToken()
    {
        const string url = "https://example.com/video";
        var args = YtDlpArgumentBuilder.Build(MakeOptions(url: url));

        Assert.Contains("--newline", args);
        Assert.Contains("--progress", args);
        Assert.Equal(url, args[^1]);
    }

    [Fact]
    public void Build_ProgressTemplate_UsesSharedMarkerAndSeparatorFromParser()
    {
        var args = YtDlpArgumentBuilder.Build(MakeOptions());

        int templateIndex = args.IndexOf("--progress-template");
        Assert.True(templateIndex >= 0);

        string template = args[templateIndex + 1];
        Assert.StartsWith($"download:{YtDlpProgressParser.ProgressMarker}{YtDlpProgressParser.FieldSeparator}", template);
        Assert.Contains("%(progress._percent_str)s", template);
        Assert.Contains("%(progress._total_bytes_str)s", template);
        Assert.Contains("%(progress._speed_str)s", template);
        Assert.Contains("%(progress._eta_str)s", template);
    }
}
