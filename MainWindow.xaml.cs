using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media.Animation;

namespace MidiaScraper
{
    public partial class MainWindow : Window
    {
        // ── State ────────────────────────────────────────────────────────────────
        private string _outputFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                                       + Path.DirectorySeparatorChar + "Downloads";
        private int _completedDownloads = 0;
        private bool _isDownloading = false;
        private CancellationTokenSource? _cts;
        private Process? _ytdlpProcess;
        private string _ytdlpPath = string.Empty;

        // ── Constructor ──────────────────────────────────────────────────────────
        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Garante que a janela aparece na frente, depois desabilita Topmost
            await Task.Delay(500);
            Topmost = false;
            Activate();

            UpdateFolderDisplay();
            AppendLog("🚀 MídiaScraper iniciado.");
            AppendLog($"📁 Pasta de saída: {_outputFolder}");
            AppendLog("");

            await EnsureYtDlpAsync();
        }

        // ── yt-dlp Management ────────────────────────────────────────────────────

        /// <summary>
        /// Tries to find yt-dlp on PATH or next to the exe.
        /// If not found, offers to download it automatically.
        /// </summary>
        private async Task EnsureYtDlpAsync()
        {
            // 1) Check PATH
            string? pathResult = FindOnPath("yt-dlp");
            if (pathResult != null)
            {
                _ytdlpPath = pathResult;
                AppendLog($"✅ yt-dlp encontrado: {_ytdlpPath}");
                await CheckYtDlpVersionAsync();
                return;
            }

            // 2) Check beside exe
            string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "yt-dlp.exe");
            if (File.Exists(localPath))
            {
                _ytdlpPath = localPath;
                AppendLog($"✅ yt-dlp encontrado: {_ytdlpPath}");
                await CheckYtDlpVersionAsync();
                return;
            }

            // 3) Offer download
            AppendLog("⚠️  yt-dlp não encontrado no sistema.");
            AppendLog("   Tentando baixar automaticamente...");

            bool ok = await DownloadYtDlpAsync(localPath);
            if (ok)
            {
                _ytdlpPath = localPath;
                AppendLog("✅ yt-dlp baixado com sucesso!");
            }
            else
            {
                AppendLog("❌ Falha ao baixar yt-dlp.");
                AppendLog("   Instale manualmente: https://github.com/yt-dlp/yt-dlp/releases");
                SetStatus("yt-dlp não encontrado", false);
            }
        }

        private async Task<bool> DownloadYtDlpAsync(string savePath)
        {
            const string url = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";
            try
            {
                using var http = new System.Net.Http.HttpClient();
                http.Timeout = TimeSpan.FromMinutes(3);
                var bytes = await http.GetByteArrayAsync(url);
                await File.WriteAllBytesAsync(savePath, bytes);
                return true;
            }
            catch (Exception ex)
            {
                AppendLog($"   Erro: {ex.Message}");
                return false;
            }
        }

        private async Task CheckYtDlpVersionAsync()
        {
            try
            {
                var psi = new ProcessStartInfo(_ytdlpPath, "--version")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var p = Process.Start(psi)!;
                string ver = await p.StandardOutput.ReadToEndAsync();
                await p.WaitForExitAsync();
                AppendLog($"   Versão: yt-dlp {ver.Trim()}");
                SetStatus("Pronto", true);
            }
            catch
            {
                SetStatus("Pronto", true);
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

        // ── Button Handlers ──────────────────────────────────────────────────────

        private void UrlTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            UrlBorder.BorderBrush = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(124, 58, 237));
        }

        private void UrlTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            UrlBorder.BorderBrush = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(45, 45, 66));
        }

        private void PasteButton_Click(object sender, RoutedEventArgs e)
        {
            if (System.Windows.Clipboard.ContainsText())
            {
                UrlTextBox.Text = System.Windows.Clipboard.GetText().Trim();
                UrlTextBox.Focus();
                UrlTextBox.SelectionStart = UrlTextBox.Text.Length;
            }
        }

        private void UrlTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
                DownloadButton_Click(sender, e);
        }

        private async void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isDownloading)
            {
                StopDownload();
                return;
            }

            string url = UrlTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(url))
            {
                AppendLog("⚠️  Por favor insira uma URL.");
                return;
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out _))
            {
                AppendLog("❌ URL inválida. Verifique e tente novamente.");
                return;
            }

            if (string.IsNullOrEmpty(_ytdlpPath) || !File.Exists(_ytdlpPath))
            {
                AppendLog("❌ yt-dlp não está disponível. Aguarde o download ou instale manualmente.");
                return;
            }

            await StartDownloadAsync(url);
        }

        private void StopButton_Click(object sender, RoutedEventArgs e) => StopDownload();

        private void FolderButton_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Escolha a pasta de destino",
                SelectedPath = _outputFolder,
                ShowNewFolderButton = true
            };
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _outputFolder = dialog.SelectedPath;
                UpdateFolderDisplay();
                AppendLog($"📁 Pasta alterada para: {_outputFolder}");
            }
        }

        private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (Directory.Exists(_outputFolder))
                Process.Start("explorer.exe", _outputFolder);
        }

        private void ClearLogButton_Click(object sender, RoutedEventArgs e)
        {
            LogTextBox.Clear();
        }

        private void FormatCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // Could update UI hints here
        }

        // ── Download Logic ───────────────────────────────────────────────────────

        private async Task StartDownloadAsync(string url)
        {
            _isDownloading = true;
            _cts = new CancellationTokenSource();
            SetDownloadingState(true);
            SetProgress(0, "Iniciando download...");

            AppendLog("");
            AppendLog($"🔗 URL: {url}");
            AppendLog($"📁 Destino: {_outputFolder}");

            string args = BuildYtDlpArgs(url);
            AppendLog($"⚙️  Argumentos: {args}");
            AppendLog("─────────────────────────────────────────────");

            try
            {
                await RunYtDlpAsync(args, _cts.Token);
            }
            catch (OperationCanceledException)
            {
                AppendLog("");
                AppendLog("⛔ Download cancelado pelo usuário.");
                SetProgress(0, "Download cancelado");
                SetStatus("Cancelado", false);
            }
            catch (Exception ex)
            {
                AppendLog($"❌ Erro inesperado: {ex.Message}");
                SetProgress(0, "Erro no download");
                SetStatus("Erro", false);
            }
            finally
            {
                _isDownloading = false;
                SetDownloadingState(false);
                _cts?.Dispose();
                _cts = null;
            }
        }

        private string BuildYtDlpArgs(string url)
        {
            var selectedItem = FormatCombo.SelectedItem as System.Windows.Controls.ComboBoxItem;
            string tag = (string?)selectedItem?.Tag ?? "best";

            string formatArg = tag switch
            {
                "audio" => "-x --audio-format mp3 --audio-quality 0",
                "1080"  => "-f \"bestvideo[height<=1080]+bestaudio/best[height<=1080]\" --merge-output-format mp4",
                "720"   => "-f \"bestvideo[height<=720]+bestaudio/best[height<=720]\" --merge-output-format mp4",
                "480"   => "-f \"bestvideo[height<=480]+bestaudio/best[height<=480]\" --merge-output-format mp4",
                _       => "-f \"bestvideo+bestaudio/best\" --merge-output-format mp4"
            };

            string subtitleArg = SubtitleCheck.IsChecked == true
                ? "--write-auto-sub --sub-lang pt,en --convert-subs srt"
                : "";

            string playlistArg = PlaylistCheck.IsChecked == true ? "" : "--no-playlist";

            // Output template
            string output = $"-o \"{_outputFolder}{Path.DirectorySeparatorChar}%(title)s.%(ext)s\"";

            return $"{formatArg} {subtitleArg} {playlistArg} {output} --newline --progress \"{url}\"";
        }

        private async Task RunYtDlpAsync(string args, CancellationToken ct)
        {
            var psi = new ProcessStartInfo
            {
                FileName = _ytdlpPath,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8
            };

            _ytdlpProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };

            _ytdlpProcess.OutputDataReceived += (_, e) =>
            {
                if (e.Data == null) return;
                Dispatcher.Invoke(() => ProcessYtDlpLine(e.Data));
            };
            _ytdlpProcess.ErrorDataReceived += (_, e) =>
            {
                if (e.Data == null) return;
                Dispatcher.Invoke(() =>
                {
                    AppendLog($"⚠️  {e.Data}");
                });
            };

            _ytdlpProcess.Start();
            _ytdlpProcess.BeginOutputReadLine();
            _ytdlpProcess.BeginErrorReadLine();

            // Register cancellation
            ct.Register(() =>
            {
                try { _ytdlpProcess?.Kill(true); } catch { }
            });

            await _ytdlpProcess.WaitForExitAsync(ct);

            int exitCode = _ytdlpProcess.ExitCode;
            _ytdlpProcess.Dispose();
            _ytdlpProcess = null;

            if (ct.IsCancellationRequested) throw new OperationCanceledException();

            if (exitCode == 0)
            {
                _completedDownloads++;
                UpdateDownloadCount();
                SetProgress(100, "Download concluído com sucesso! ✅");
                SetStatus("Concluído", true);
                AppendLog("─────────────────────────────────────────────");
                AppendLog("✅ Download concluído!");
            }
            else
            {
                SetProgress(0, $"yt-dlp terminou com código {exitCode}");
                SetStatus("Erro", false);
                AppendLog($"⚠️  yt-dlp encerrou com código de saída: {exitCode}");
            }
        }

        // ── yt-dlp Output Parser ─────────────────────────────────────────────────

        private static readonly Regex ProgressRegex =
            new(@"\[download\]\s+([\d.]+)%\s+of\s+~?\s*([\d.]+\w+)\s+at\s+([\d.]+\s*\w+/s)", RegexOptions.Compiled);

        private void ProcessYtDlpLine(string line)
        {
            // Parse progress lines
            var m = ProgressRegex.Match(line);
            if (m.Success)
            {
                if (double.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double pct))
                {
                    string size  = m.Groups[2].Value;
                    string speed = m.Groups[3].Value;
                    SetProgress((int)pct, $"{pct:F1}%  –  {size}  –  {speed}");
                }
                return; // Don't spam log with raw progress lines
            }

            // Destination file lines
            if (line.StartsWith("[download] Destination:") || line.StartsWith("[Merger]") ||
                line.Contains("has already been downloaded") || line.StartsWith("[ExtractAudio]"))
            {
                AppendLog($"📄 {line}");
                SetProgress(0, line.Length > 90 ? line[..90] + "…" : line);
                return;
            }

            // Info/metadata lines
            if (line.StartsWith("[youtube]") || line.StartsWith("[twitter]") ||
                line.StartsWith("[generic]") || line.StartsWith("[info]") ||
                line.StartsWith("[VideoConvertor]") || line.StartsWith("[ffmpeg]"))
            {
                AppendLog($"ℹ️  {line}");
                return;
            }

            // WARNING / ERROR
            if (line.StartsWith("WARNING:") || line.StartsWith("ERROR:"))
            {
                AppendLog($"⚠️  {line}");
                return;
            }

            // Catch-all
            if (!string.IsNullOrWhiteSpace(line))
                AppendLog(line);
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private void StopDownload()
        {
            if (!_isDownloading) return;
            _cts?.Cancel();
        }

        private void SetDownloadingState(bool downloading)
        {
            DownloadButtonText.Text = downloading ? "Parar" : "Baixar";
            DownloadIcon.Text       = downloading ? "⏹" : "⬇";
            StopButton.IsEnabled    = downloading;
            UrlTextBox.IsEnabled    = !downloading;
            FormatCombo.IsEnabled   = !downloading;
        }

        private void SetProgress(int percent, string statusText)
        {
            ProgressPercent.Text = $"{percent}%";
            ProgressStatus.Text  = statusText;

            // Animate progress bar width
            double totalWidth = ((System.Windows.FrameworkElement)ProgressFill.Parent).ActualWidth;
            double targetWidth = totalWidth * percent / 100.0;

            var anim = new DoubleAnimation(targetWidth, TimeSpan.FromMilliseconds(300))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            ProgressFill.BeginAnimation(WidthProperty, anim);
        }

        private void SetStatus(string text, bool ok)
        {
            StatusText.Text = text;
            StatusDot.Fill  = ok
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 185, 129))
                : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68));
        }

        private void AppendLog(string line)
        {
            LogTextBox.AppendText(line + Environment.NewLine);
            LogTextBox.ScrollToEnd();
        }

        private void UpdateFolderDisplay()
        {
            // Truncate if too long
            string display = _outputFolder.Length > 45
                ? "…" + _outputFolder[^44..]
                : _outputFolder;
            FolderButtonText.Text  = display;
            OutputFolderText.Text  = $"Pasta: {display}";
        }

        private void UpdateDownloadCount()
        {
            DownloadCountText.Text = _completedDownloads == 1
                ? "1 download concluído"
                : $"{_completedDownloads} downloads concluídos";
        }
    }
}