using System.Diagnostics;
using System.Text;
using MidiaScraper.Models;

namespace MidiaScraper.Services.YtDlp
{
    public class YtDlpMediaDownloader : IMediaDownloader
    {
        public async Task<DownloadResult> DownloadAsync(
            string ytdlpPath,
            List<string> args,
            IProgress<DownloadProgressInfo> progress,
            CancellationToken ct)
        {
            var psi = new ProcessStartInfo
            {
                FileName = ytdlpPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            foreach (string arg in args)
                psi.ArgumentList.Add(arg);

            using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data == null) return;
                var info = YtDlpProgressParser.Parse(e.Data);
                if (info != null) progress.Report(info);
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data == null) return;
                progress.Report(new DownloadProgressInfo { Kind = DownloadLineKind.Warning, RawLine = e.Data });
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var registration = ct.Register(() =>
            {
                try { process.Kill(true); } catch { }
            });

            await process.WaitForExitAsync(ct);

            int exitCode = process.ExitCode;

            if (ct.IsCancellationRequested)
                throw new OperationCanceledException();

            return new DownloadResult { Success = exitCode == 0, ExitCode = exitCode };
        }
    }
}
