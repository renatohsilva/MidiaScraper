using MidiaScraper.Models;
using MidiaScraper.Services.YtDlp;

namespace MidiaScraper.Tests;

public class YtDlpProgressParserTests
{
    private static string MakeProgressLine(string percent, string size, string speed, string eta)
    {
        char sep = YtDlpProgressParser.FieldSeparator;
        return $"{YtDlpProgressParser.ProgressMarker}{sep}{percent}{sep}{size}{sep}{speed}{sep}{eta}";
    }

    [Fact]
    public void Parse_StructuredProgressLine_ExtractsPercentSizeSpeedAndEta()
    {
        var info = YtDlpProgressParser.Parse(MakeProgressLine(" 42.5%", "10.00MiB", "1.23MiB/s", "00:10"));

        Assert.NotNull(info);
        Assert.Equal(DownloadLineKind.Progress, info!.Kind);
        Assert.Equal(42.5, info.Percent);
        Assert.Equal("10.00MiB", info.SizeText);
        Assert.Equal("1.23MiB/s", info.SpeedText);
        Assert.Equal("00:10", info.Eta);
    }

    [Fact]
    public void Parse_StructuredProgressLine_WithUnparsablePercent_ReturnsNullPercentButKeepsOtherFields()
    {
        var info = YtDlpProgressParser.Parse(MakeProgressLine("N/A", "Unknown", "N/A", "Unknown ETA"));

        Assert.NotNull(info);
        Assert.Equal(DownloadLineKind.Progress, info!.Kind);
        Assert.Null(info.Percent);
        Assert.Equal("Unknown", info.SizeText);
    }

    [Fact]
    public void Parse_LineResemblingOldFreeTextFormat_NoLongerRecognizedAsProgress()
    {
        // Regression guard for Passo 1.5: the old "[download] X% of Y at Z" free-text format
        // must NOT be picked up anymore now that parsing relies solely on --progress-template.
        var info = YtDlpProgressParser.Parse("[download]  42.5% of ~10.00MiB at 1.23MiB/s");

        Assert.NotNull(info);
        Assert.Equal(DownloadLineKind.Raw, info!.Kind);
    }

    [Theory]
    [InlineData("[download] Destination: video.mp4")]
    [InlineData("[Merger] Merging formats into \"video.mp4\"")]
    [InlineData("[video.mp4] has already been downloaded")]
    [InlineData("[ExtractAudio] Destination: audio.mp3")]
    public void Parse_DestinationLines_ReturnDestinationKind(string line)
    {
        var info = YtDlpProgressParser.Parse(line);

        Assert.NotNull(info);
        Assert.Equal(DownloadLineKind.Destination, info!.Kind);
        Assert.Equal(line, info.RawLine);
    }

    [Theory]
    [InlineData("[youtube] mILYtp4UHIQ: Downloading webpage")]
    [InlineData("[info] mILYtp4UHIQ: Downloading 1 format(s)")]
    [InlineData("[ffmpeg] Merging formats")]
    public void Parse_InfoLines_ReturnInfoKind(string line)
    {
        var info = YtDlpProgressParser.Parse(line);

        Assert.NotNull(info);
        Assert.Equal(DownloadLineKind.Info, info!.Kind);
    }

    [Theory]
    [InlineData("WARNING: Some formats were skipped")]
    [InlineData("ERROR: Requested content is not available")]
    public void Parse_WarningAndErrorLines_ReturnWarningKind(string line)
    {
        var info = YtDlpProgressParser.Parse(line);

        Assert.NotNull(info);
        Assert.Equal(DownloadLineKind.Warning, info!.Kind);
    }

    [Fact]
    public void Parse_UnrecognizedNonEmptyLine_ReturnsRawKind()
    {
        var info = YtDlpProgressParser.Parse("some unstructured yt-dlp output");

        Assert.NotNull(info);
        Assert.Equal(DownloadLineKind.Raw, info!.Kind);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_WhitespaceOnlyLine_ReturnsNull(string line)
    {
        var info = YtDlpProgressParser.Parse(line);

        Assert.Null(info);
    }
}
