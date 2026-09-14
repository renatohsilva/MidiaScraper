using System.IO;
using MidiaScraper.Models;

namespace MidiaScraper.Services.YtDlp
{
    public static class YtDlpArgumentBuilder
    {
        public static List<string> Build(DownloadOptions options)
        {
            var args = new List<string>();

            switch (options.Format)
            {
                case DownloadFormat.AudioOnly:
                    args.Add("-x");
                    args.Add("--audio-format");
                    args.Add("mp3");
                    args.Add("--audio-quality");
                    args.Add("0");
                    break;
                case DownloadFormat.Video1080:
                case DownloadFormat.Video720:
                case DownloadFormat.Video480:
                    string height = options.Format switch
                    {
                        DownloadFormat.Video1080 => "1080",
                        DownloadFormat.Video720 => "720",
                        _ => "480"
                    };
                    args.Add("-f");
                    args.Add($"bestvideo[height<={height}]+bestaudio/best[height<={height}]");
                    args.Add("--merge-output-format");
                    args.Add("mp4");
                    break;
                default:
                    args.Add("-f");
                    args.Add("bestvideo+bestaudio/best");
                    args.Add("--merge-output-format");
                    args.Add("mp4");
                    break;
            }

            if (options.DownloadSubtitles)
            {
                args.Add("--write-auto-sub");
                args.Add("--sub-lang");
                args.Add("pt,en");
                args.Add("--convert-subs");
                args.Add("srt");
            }

            if (!options.DownloadPlaylist)
                args.Add("--no-playlist");

            args.Add("-o");
            args.Add($"{options.OutputFolder}{Path.DirectorySeparatorChar}%(title)s.%(ext)s");

            args.Add("--newline");
            args.Add("--progress");
            args.Add("--progress-template");
            args.Add(
                $"download:{YtDlpProgressParser.ProgressMarker}{YtDlpProgressParser.FieldSeparator}" +
                $"%(progress._percent_str)s{YtDlpProgressParser.FieldSeparator}" +
                $"%(progress._total_bytes_str)s{YtDlpProgressParser.FieldSeparator}" +
                $"%(progress._speed_str)s{YtDlpProgressParser.FieldSeparator}" +
                $"%(progress._eta_str)s");
            args.Add(options.Url);

            return args;
        }
    }
}
