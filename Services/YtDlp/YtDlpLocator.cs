using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;

namespace MidiaScraper.Services.YtDlp
{
    public class YtDlpLocator : IYtDlpLocator
    {
        private const string DownloadUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";
        private const string ChecksumsUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/SHA2-256SUMS";

        public async Task<YtDlpLocateResult> EnsureAsync(Action<string> log)
        {
            string? pathResult = FindOnPath("yt-dlp");
            if (pathResult != null)
            {
                log($"✅ yt-dlp encontrado: {pathResult}");
                await LogVersionAsync(pathResult, log);
                return new YtDlpLocateResult { Outcome = YtDlpLocateOutcome.FoundExisting, Path = pathResult };
            }

            string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "yt-dlp.exe");
            if (File.Exists(localPath))
            {
                log($"✅ yt-dlp encontrado: {localPath}");
                await LogVersionAsync(localPath, log);
                return new YtDlpLocateResult { Outcome = YtDlpLocateOutcome.FoundExisting, Path = localPath };
            }

            log("⚠️  yt-dlp não encontrado no sistema.");
            log("   Tentando baixar automaticamente...");

            bool ok = await DownloadYtDlpAsync(localPath, log);
            if (ok)
            {
                log("✅ yt-dlp baixado com sucesso!");
                return new YtDlpLocateResult { Outcome = YtDlpLocateOutcome.Downloaded, Path = localPath };
            }

            log("❌ Falha ao baixar yt-dlp.");
            log("   Instale manualmente: https://github.com/yt-dlp/yt-dlp/releases");
            return new YtDlpLocateResult { Outcome = YtDlpLocateOutcome.NotFound };
        }

        private static async Task<bool> DownloadYtDlpAsync(string savePath, Action<string> log)
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };

                byte[] bytes = await http.GetByteArrayAsync(DownloadUrl);

                string checksums = await http.GetStringAsync(ChecksumsUrl);
                string? expectedHash = ParseChecksum(checksums, "yt-dlp.exe");
                if (expectedHash == null)
                {
                    log("   Erro: não foi possível localizar o checksum de yt-dlp.exe em SHA2-256SUMS.");
                    return false;
                }

                string actualHash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
                if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                {
                    log($"   Erro: hash SHA-256 não confere (esperado {expectedHash}, obtido {actualHash}). Download descartado.");
                    return false;
                }

                await File.WriteAllBytesAsync(savePath, bytes);
                log("   Integridade verificada (SHA-256).");
                return true;
            }
            catch (Exception ex)
            {
                log($"   Erro: {ex.Message}");
                return false;
            }
        }

        private static string? ParseChecksum(string checksumFileContent, string fileName)
        {
            foreach (string rawLine in checksumFileContent.Split('\n'))
            {
                string trimmed = rawLine.Trim();
                if (trimmed.Length == 0) continue;

                int sep = trimmed.IndexOf(' ');
                if (sep <= 0) continue;

                string hash = trimmed[..sep];
                string name = trimmed[sep..].TrimStart(' ', '*');
                if (string.Equals(name, fileName, StringComparison.OrdinalIgnoreCase))
                    return hash.ToLowerInvariant();
            }
            return null;
        }

        private static async Task LogVersionAsync(string ytdlpPath, Action<string> log)
        {
            try
            {
                var psi = new ProcessStartInfo(ytdlpPath, "--version")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var p = Process.Start(psi)!;
                string ver = await p.StandardOutput.ReadToEndAsync();
                await p.WaitForExitAsync();
                log($"   Versão: yt-dlp {ver.Trim()}");
            }
            catch
            {
                // Versão não pôde ser determinada; segue mesmo assim.
            }
        }

        private static string? FindOnPath(string exe)
        {
            foreach (string dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'))
            {
                string full = Path.Combine(dir.Trim(), exe + ".exe");
                if (File.Exists(full)) return full;
            }
            return null;
        }
    }
}
