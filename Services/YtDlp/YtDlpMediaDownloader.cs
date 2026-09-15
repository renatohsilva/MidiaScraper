using System.Diagnostics;
using System.Text;
using MidiaScraper.Models;

namespace MidiaScraper.Services.YtDlp
{
    public class YtDlpMediaDownloader : IMediaDownloader
    {
        private const int MaxAttempts = 3;

        private static readonly TimeSpan[] BackoffDelays =
        {
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(5)
        };

        /// <summary>
        /// Trechos de mensagem que indicam uma falha transitória de rede (vale tentar de novo).
        /// Qualquer outra falha (URL não suportada, conteúdo indisponível/privado, etc.) é tratada
        /// como definitiva — repetir só mascararia o erro real atrás de tentativas inúteis.
        /// </summary>
        private static readonly string[] RetryableErrorHints =
        {
            "timed out", "timeout", "connection reset", "temporary failure",
            "http error 429", "http error 500", "http error 502", "http error 503", "http error 504",
            "unable to download webpage", "name or service not known", "network is unreachable",
            "connection refused", "connection aborted"
        };

        public async Task<DownloadResult> DownloadAsync(
            string ytdlpPath,
            List<string> args,
            IProgress<DownloadProgressInfo> progress,
            CancellationToken ct)
        {
            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                var (result, lastErrorLine) = await RunOnceAsync(ytdlpPath, args, progress, ct);
                if (result.Success || attempt == MaxAttempts)
                    return result;

                bool retryable = lastErrorLine != null &&
                    RetryableErrorHints.Any(hint => lastErrorLine.Contains(hint, StringComparison.OrdinalIgnoreCase));
                if (!retryable)
                    return result;

                TimeSpan delay = BackoffDelays[Math.Min(attempt - 1, BackoffDelays.Length - 1)];
                progress.Report(new DownloadProgressInfo
                {
                    Kind = DownloadLineKind.Retry,
                    RawLine = $"Falha de rede detectada. Tentativa {attempt + 1} de {MaxAttempts} em {delay.TotalSeconds:0}s..."
                });
                await Task.Delay(delay, ct);
            }

            // Inatingível: o laço acima sempre retorna no último attempt.
            throw new InvalidOperationException("DownloadAsync retry loop exited without a result.");
        }

        private static async Task<(DownloadResult Result, string? LastErrorLine)> RunOnceAsync(
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
            string? lastErrorLine = null;
            string? lastFilePath = null;

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data == null) return;
                var info = YtDlpProgressParser.Parse(e.Data);
                if (info == null) return;
                if (info.Kind == DownloadLineKind.Warning) lastErrorLine = info.RawLine;
                if (info.Kind == DownloadLineKind.Destination && info.FilePath != null) lastFilePath = info.FilePath;
                progress.Report(info);
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data == null) return;
                lastErrorLine = e.Data;
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

            var result = new DownloadResult { Success = exitCode == 0, ExitCode = exitCode, FilePath = lastFilePath };
            return (result, lastErrorLine);
        }
    }
}
