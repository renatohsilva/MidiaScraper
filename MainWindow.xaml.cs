using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media.Animation;
using MidiaScraper.Models;
using MidiaScraper.Services.YtDlp;

namespace MidiaScraper
{
    public partial class MainWindow : Window
    {
        // ── Services ─────────────────────────────────────────────────────────────
        private readonly IYtDlpLocator _ytdlpLocator = new YtDlpLocator();
        private readonly IMediaDownloader _mediaDownloader = new YtDlpMediaDownloader();

        // ── State ────────────────────────────────────────────────────────────────
        private string _outputFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                                       + Path.DirectorySeparatorChar + "Downloads";
        private int _completedDownloads = 0;
        private bool _isDownloading = false;
        private CancellationTokenSource? _cts;
        private string _ytdlpPath = string.Empty;
        private double _maxProgressPercent;

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

            var result = await _ytdlpLocator.EnsureAsync(AppendLog);
            switch (result.Outcome)
            {
                case YtDlpLocateOutcome.FoundExisting:
                    _ytdlpPath = result.Path;
                    SetStatus("Pronto", true);
                    break;
                case YtDlpLocateOutcome.Downloaded:
                    _ytdlpPath = result.Path;
                    break;
                case YtDlpLocateOutcome.NotFound:
                    SetStatus("yt-dlp não encontrado", false);
                    break;
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (_isDownloading)
            {
                AppendLog("⛔ Encerrando yt-dlp em andamento antes de fechar...");
                StopDownload();
            }
            base.OnClosing(e);
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

            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? parsedUrl) ||
                (parsedUrl.Scheme != Uri.UriSchemeHttp && parsedUrl.Scheme != Uri.UriSchemeHttps))
            {
                AppendLog("❌ URL inválida. Use um endereço http:// ou https:// completo.");
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
            _maxProgressPercent = 0;
            SetDownloadingState(true);
            SetProgress(0, "Iniciando download...");

            AppendLog("");
            AppendLog($"🔗 URL: {url}");
            AppendLog($"📁 Destino: {_outputFolder}");

            var options = BuildDownloadOptions(url);
            var args = YtDlpArgumentBuilder.Build(options);
            AppendLog($"⚙️  Argumentos: {string.Join(" ", args)}");
            AppendLog("─────────────────────────────────────────────");

            var progress = new Progress<DownloadProgressInfo>(RenderProgressInfo);

            try
            {
                var result = await _mediaDownloader.DownloadAsync(_ytdlpPath, args, progress, _cts.Token);
                if (result.Success)
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
                    SetProgress(0, $"yt-dlp terminou com código {result.ExitCode}");
                    SetStatus("Erro", false);
                    AppendLog($"⚠️  yt-dlp encerrou com código de saída: {result.ExitCode}");
                }
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

        private DownloadOptions BuildDownloadOptions(string url)
        {
            var selectedItem = FormatCombo.SelectedItem as System.Windows.Controls.ComboBoxItem;
            string? tag = selectedItem?.Tag as string;

            return new DownloadOptions
            {
                Url = url,
                OutputFolder = _outputFolder,
                Format = ParseFormatTag(tag),
                DownloadSubtitles = SubtitleCheck.IsChecked == true,
                DownloadPlaylist = PlaylistCheck.IsChecked == true
            };
        }

        private static DownloadFormat ParseFormatTag(string? tag) => tag switch
        {
            "audio" => DownloadFormat.AudioOnly,
            "1080" => DownloadFormat.Video1080,
            "720" => DownloadFormat.Video720,
            "480" => DownloadFormat.Video480,
            _ => DownloadFormat.Best
        };

        private void RenderProgressInfo(DownloadProgressInfo info)
        {
            switch (info.Kind)
            {
                case DownloadLineKind.Progress:
                    // yt-dlp reestima o tamanho total a cada fragmento em downloads fragmentados
                    // (DASH/HLS), então o percentual bruto pode oscilar levemente para baixo antes
                    // de subir de novo. Trava a exibição para nunca regredir visualmente.
                    double displayPercent = Math.Max(info.Percent ?? _maxProgressPercent, _maxProgressPercent);
                    _maxProgressPercent = displayPercent;
                    SetProgress((int)displayPercent, $"{displayPercent:F1}%  –  {info.SizeText}  –  {info.SpeedText}");
                    break;
                case DownloadLineKind.Destination:
                    AppendLog($"📄 {info.RawLine}");
                    SetProgress(0, info.RawLine.Length > 90 ? info.RawLine[..90] + "…" : info.RawLine);
                    break;
                case DownloadLineKind.Info:
                    AppendLog($"ℹ️  {info.RawLine}");
                    break;
                case DownloadLineKind.Warning:
                    AppendLog($"⚠️  {info.RawLine}");
                    break;
                case DownloadLineKind.Raw:
                    AppendLog(info.RawLine);
                    break;
            }
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
