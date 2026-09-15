using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Forms;
using System.Windows.Media.Animation;
using MidiaScraper.Models;
using MidiaScraper.Services.YtDlp;
using MidiaScraper.ViewModels;

namespace MidiaScraper
{
    public partial class MainWindow : Window
    {
        // ── Services ─────────────────────────────────────────────────────────────
        private readonly IYtDlpLocator _ytdlpLocator = new YtDlpLocator();
        private readonly IMediaDownloader _mediaDownloader = new YtDlpMediaDownloader();
        private readonly IMediaMetadataProvider _metadataProvider = new YtDlpMetadataProvider();
        private readonly Services.Downloads.DownloadHistoryStore _historyStore = new();
        private readonly Services.Settings.SettingsStore _settingsStore = new();

        // ── State ────────────────────────────────────────────────────────────────
        private string _outputFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                                       + Path.DirectorySeparatorChar + "Downloads";
        private int _completedDownloads = 0;
        private bool _isDownloading = false;
        private CancellationTokenSource? _cts;
        private string _ytdlpPath = string.Empty;
        private double _maxProgressPercent;
        private readonly ObservableCollection<DownloadItemViewModel> _playlistItems = new();
        private readonly ObservableCollection<DownloadHistoryEntry> _history = new();
        private AppSettings _settings = new();
        private bool _settingsLoaded;
        private string? _lastDownloadedFilePath;

        // ── Constructor ──────────────────────────────────────────────────────────
        public MainWindow()
        {
            InitializeComponent();
            PlaylistItemsControl.ItemsSource = _playlistItems;
            HistoryItemsControl.ItemsSource = _history;
            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Activate();

            _settings = await _settingsStore.LoadAsync(AppendLog);
            _outputFolder = _settings.OutputFolder;
            ApplySettingsToControls();
            _settingsLoaded = true;

            UpdateFolderDisplay();
            AppendLog("🚀 MídiaScraper iniciado.");
            AppendLog($"📁 Pasta de saída: {_outputFolder}");
            AppendLog("");

            foreach (var entry in await _historyStore.LoadAsync(AppendLog))
                _history.Add(entry);

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

        private void RecentUrlsButton_Click(object sender, RoutedEventArgs e)
        {
            var recentUrls = _history.Select(h => h.Url).Distinct().Take(8).ToList();
            if (recentUrls.Count == 0)
            {
                AppendLog("ℹ️  Nenhuma URL recente ainda.");
                return;
            }

            RecentUrlsList.ItemsSource = recentUrls;
            RecentUrlsPopup.IsOpen = true;
        }

        private void RecentUrlItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button { Content: string url })
            {
                UrlTextBox.Text = url;
                UrlTextBox.Focus();
                UrlTextBox.SelectionStart = url.Length;
            }
            RecentUrlsPopup.IsOpen = false;
        }

        private void UrlTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key != System.Windows.Input.Key.Enter) return;
            if (System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift))
                return; // Shift+Enter insere uma quebra de linha (para digitar várias URLs manualmente)

            // Enter sozinho continua disparando o download em vez de inserir uma quebra de linha.
            e.Handled = true;
            DownloadButton_Click(sender, e);
        }

        private static bool IsValidHttpUrl(string url) =>
            Uri.TryCreate(url, UriKind.Absolute, out Uri? parsed) &&
            (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps);

        private async void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isDownloading)
            {
                StopDownload();
                return;
            }

            var lines = UrlTextBox.Text
                .Split('\n')
                .Select(l => l.Trim())
                .Where(l => l.Length > 0)
                .ToList();

            if (lines.Count == 0)
            {
                AppendLog("⚠️  Por favor insira uma URL.");
                return;
            }

            if (string.IsNullOrEmpty(_ytdlpPath) || !File.Exists(_ytdlpPath))
            {
                AppendLog("❌ yt-dlp não está disponível. Aguarde o download ou instale manualmente.");
                return;
            }

            if (lines.Count == 1)
            {
                if (!IsValidHttpUrl(lines[0]))
                {
                    AppendLog("❌ URL inválida. Use um endereço http:// ou https:// completo.");
                    return;
                }
                await StartDownloadAsync(lines[0]);
                return;
            }

            var validUrls = new List<string>();
            foreach (string line in lines)
            {
                if (IsValidHttpUrl(line))
                    validUrls.Add(line);
                else
                    AppendLog($"⚠️  Ignorando linha inválida: {line}");
            }

            if (validUrls.Count == 0)
            {
                AppendLog("❌ Nenhuma URL válida encontrada.");
                return;
            }

            if (validUrls.Count == 1)
            {
                await StartDownloadAsync(validUrls[0]);
                return;
            }

            ShowManualUrlQueue(validUrls);
        }

        private void StopButton_Click(object sender, RoutedEventArgs e) => StopDownload();

        private async void FolderButton_Click(object sender, RoutedEventArgs e)
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
                await SaveSettingsAsync();
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

        private void HistoryButton_Click(object sender, RoutedEventArgs e)
        {
            EmptyStateCard.Visibility = Visibility.Collapsed;
            ProgressCard.Visibility = Visibility.Collapsed;
            PlaylistSelectionCard.Visibility = Visibility.Collapsed;
            HistoryCard.Visibility = Visibility.Visible;
        }

        private void CloseHistoryButton_Click(object sender, RoutedEventArgs e)
        {
            HistoryCard.Visibility = Visibility.Collapsed;
            EmptyStateCard.Visibility = Visibility.Visible;
        }

        private void ErrorBannerCloseButton_Click(object sender, RoutedEventArgs e) => HideErrorBanner();

        private void ShowErrorBanner(string message)
        {
            ErrorBannerText.Text = message;
            ErrorBanner.Visibility = Visibility.Visible;
        }

        private void HideErrorBanner()
        {
            ErrorBanner.Visibility = Visibility.Collapsed;
        }

        private async void FormatCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            await SaveSettingsAsync();
        }

        private async void SettingsCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            await SaveSettingsAsync();
        }

        private async void SettingsComboBox_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            await SaveSettingsAsync();
        }

        private static void SelectComboItemByTag(System.Windows.Controls.ComboBox combo, string tag)
        {
            foreach (var obj in combo.Items)
            {
                if (obj is System.Windows.Controls.ComboBoxItem item && (string?)item.Tag == tag)
                {
                    combo.SelectedItem = item;
                    return;
                }
            }
        }

        private void ApplySettingsToControls()
        {
            string formatTag = _settings.DefaultFormat switch
            {
                DownloadFormat.AudioOnly => "audio",
                DownloadFormat.Video1080 => "1080",
                DownloadFormat.Video720 => "720",
                DownloadFormat.Video480 => "480",
                _ => "best"
            };
            SelectComboItemByTag(FormatCombo, formatTag);
            SelectComboItemByTag(RateLimitCombo, _settings.RateLimit ?? "");

            SubtitleCheck.IsChecked = _settings.DefaultSubtitles;
            PlaylistCheck.IsChecked = _settings.DefaultPlaylist;
        }

        private async Task SaveSettingsAsync()
        {
            if (!_settingsLoaded) return;

            _settings.OutputFolder = _outputFolder;
            _settings.DefaultFormat = ParseFormatTag((FormatCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Tag as string);
            _settings.DefaultSubtitles = SubtitleCheck.IsChecked == true;
            _settings.DefaultPlaylist = PlaylistCheck.IsChecked == true;
            string? rateLimitTag = (RateLimitCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Tag as string;
            _settings.RateLimit = string.IsNullOrEmpty(rateLimitTag) ? null : rateLimitTag;

            try
            {
                await _settingsStore.SaveAsync(_settings);
            }
            catch (Exception ex)
            {
                AppendLog($"⚠️  Não foi possível salvar as configurações: {ex.Message}");
            }
        }

        // ── Download Logic ───────────────────────────────────────────────────────

        private async Task StartDownloadAsync(string url)
        {
            _isDownloading = true;
            _cts = new CancellationTokenSource();
            _maxProgressPercent = 0;
            EmptyStateCard.Visibility = Visibility.Collapsed;
            PlaylistSelectionCard.Visibility = Visibility.Collapsed;
            HistoryCard.Visibility = Visibility.Collapsed;
            ProgressCard.Visibility = Visibility.Visible;
            HideErrorBanner();
            HideMetadataPreview();
            HideOpenFileButton();
            SetDownloadingState(true);
            SetProgress(0, "Buscando informações do vídeo...");
            ProgressEta.Text = "";

            AppendLog("");
            AppendLog($"🔗 URL: {url}");
            AppendLog($"📁 Destino: {_outputFolder}");

            try
            {
                bool wantsPlaylist = PlaylistCheck.IsChecked == true;
                var metadata = await TryFetchMetadataAsync(url, wantsPlaylist, _cts.Token);

                if (metadata != null && metadata.IsPlaylist)
                {
                    ShowPlaylistSelection(metadata);
                    return;
                }

                if (metadata != null)
                    ShowMetadataPreview(metadata);

                await DownloadOneAsync(url, metadata?.Title ?? url, _cts.Token);
            }
            catch (OperationCanceledException)
            {
                AppendLog("");
                AppendLog("⛔ Cancelado pelo usuário.");
                SetProgress(0, "Cancelado");
                ProgressEta.Text = "";
                SetStatus("Cancelado", false);
            }
            finally
            {
                ResetDownloadState();
            }
        }

        /// <summary>
        /// Executa um único download (opções já resolvidas a partir dos controles + a URL dada) e
        /// atualiza o card de progresso compartilhado. Não gerencia o ciclo de vida de
        /// _isDownloading/_cts — quem chama decide isso (permite reuso tanto para o caminho de uma
        /// única URL quanto para cada item de um lote de playlist).
        /// </summary>
        private async Task<bool> DownloadOneAsync(string url, string title, CancellationToken ct)
        {
            var options = BuildDownloadOptions(url);
            var args = YtDlpArgumentBuilder.Build(options);
            AppendLog($"⚙️  Argumentos: {string.Join(" ", args)}");
            AppendLog("─────────────────────────────────────────────");

            var progress = new Progress<DownloadProgressInfo>(RenderProgressInfo);

            try
            {
                SetProgress(0, "Iniciando download...");
                var result = await _mediaDownloader.DownloadAsync(_ytdlpPath, args, progress, ct);
                if (result.Success)
                {
                    _completedDownloads++;
                    UpdateDownloadCount();
                    SetProgress(100, "Download concluído com sucesso! ✅");
                    ProgressEta.Text = "";
                    SetStatus("Concluído", true);
                    AppendLog("─────────────────────────────────────────────");
                    AppendLog("✅ Download concluído!");
                    ShowOpenFileButton(result.FilePath);
                    await RecordHistoryAsync(url, title, "Concluído");
                    return true;
                }

                SetProgress(0, $"yt-dlp terminou com código {result.ExitCode}");
                ProgressEta.Text = "";
                SetStatus("Erro", false);
                AppendLog($"⚠️  yt-dlp encerrou com código de saída: {result.ExitCode}");
                ShowErrorBanner($"O download falhou (yt-dlp encerrou com código {result.ExitCode}). Veja o console para detalhes.");
                await RecordHistoryAsync(url, title, "Falhou");
                return false;
            }
            catch (OperationCanceledException)
            {
                await RecordHistoryAsync(url, title, "Cancelado");
                throw;
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Erro inesperado ao baixar {Url}", url);
                AppendLog($"❌ Erro inesperado: {ex.Message}");
                SetProgress(0, "Erro no download");
                ProgressEta.Text = "";
                SetStatus("Erro", false);
                ShowErrorBanner($"Erro inesperado: {ex.Message}");
                await RecordHistoryAsync(url, title, "Falhou");
                return false;
            }
        }

        private async Task RecordHistoryAsync(string url, string title, string status)
        {
            var entry = new DownloadHistoryEntry
            {
                Url = url,
                Title = title,
                CompletedAt = DateTimeOffset.Now,
                Status = status
            };
            _history.Insert(0, entry);

            try
            {
                await _historyStore.SaveAsync(_history.ToList());
            }
            catch (Exception ex)
            {
                AppendLog($"⚠️  Não foi possível salvar o histórico: {ex.Message}");
            }
        }

        private void ResetDownloadState()
        {
            _isDownloading = false;
            SetDownloadingState(false);
            _cts?.Dispose();
            _cts = null;
        }

        // ── Playlist Selection ───────────────────────────────────────────────────

        private void ShowPlaylistSelection(MediaMetadata metadata)
        {
            _playlistItems.Clear();
            foreach (var entry in metadata.Entries)
            {
                _playlistItems.Add(new DownloadItemViewModel
                {
                    Id = entry.Id,
                    Title = entry.Title,
                    ThumbnailUrl = entry.ThumbnailUrl,
                    DurationSeconds = entry.DurationSeconds,
                    WatchUrl = entry.WatchUrl,
                    IsSelected = true,
                    Status = "Pendente"
                });
            }

            DisplayQueueSelection($"{metadata.Title} — {_playlistItems.Count} itens", "Playlist detectada");
        }

        private void ShowManualUrlQueue(List<string> urls)
        {
            _playlistItems.Clear();
            foreach (string url in urls)
            {
                _playlistItems.Add(new DownloadItemViewModel
                {
                    Id = url,
                    Title = url.Length > 70 ? url[..70] + "…" : url,
                    WatchUrl = url,
                    IsSelected = true,
                    Status = "Pendente"
                });
            }

            DisplayQueueSelection($"Fila de downloads — {_playlistItems.Count} URLs", "Fila detectada");
        }

        private void DisplayQueueSelection(string headerText, string logPrefix)
        {
            PlaylistTitleText.Text = headerText;
            AppendLog($"📃 {logPrefix}: {_playlistItems.Count} itens. Selecione o que deseja baixar.");

            ProgressCard.Visibility = Visibility.Collapsed;
            EmptyStateCard.Visibility = Visibility.Collapsed;
            HistoryCard.Visibility = Visibility.Collapsed;
            PlaylistSelectionCard.Visibility = Visibility.Visible;
        }

        private void SelectAllButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _playlistItems) item.IsSelected = true;
        }

        private void SelectNoneButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _playlistItems) item.IsSelected = false;
        }

        private async void DownloadSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isDownloading) return;
            await RunSelectedPlaylistItemsAsync();
        }

        private async Task RunSelectedPlaylistItemsAsync()
        {
            var selected = _playlistItems.Where(i => i.IsSelected).ToList();
            if (selected.Count == 0)
            {
                AppendLog("⚠️  Nenhum item selecionado.");
                return;
            }

            PlaylistSelectionCard.Visibility = Visibility.Collapsed;
            ProgressCard.Visibility = Visibility.Visible;
            HideErrorBanner();

            AppendLog("");
            AppendLog($"▶ Iniciando lote de {selected.Count} item(ns) da playlist...");

            foreach (var item in selected)
            {
                _isDownloading = true;
                _cts = new CancellationTokenSource();
                _maxProgressPercent = 0;
                SetDownloadingState(true);
                HideMetadataPreview();
                HideOpenFileButton();
                SetProgress(0, "Iniciando download...");
                ProgressEta.Text = "";

                AppendLog("");
                AppendLog($"▶ {item.Title}");

                if (string.IsNullOrWhiteSpace(item.WatchUrl))
                {
                    item.Status = "Erro (sem URL)";
                    AppendLog("❌ Não foi possível determinar a URL deste item.");
                    ResetDownloadState();
                    continue;
                }

                item.Status = "Baixando...";
                try
                {
                    bool ok = await DownloadOneAsync(item.WatchUrl, item.Title, _cts.Token);
                    item.Status = ok ? "Concluído" : "Falhou";
                }
                catch (OperationCanceledException)
                {
                    item.Status = "Cancelado";
                    AppendLog("⛔ Item cancelado pelo usuário.");
                    SetStatus("Cancelado", false);
                }
                finally
                {
                    ResetDownloadState();
                }
            }

            AppendLog("");
            AppendLog("✅ Lote de playlist finalizado.");
        }

        private DownloadOptions BuildDownloadOptions(string url)
        {
            var selectedItem = FormatCombo.SelectedItem as System.Windows.Controls.ComboBoxItem;
            string? tag = selectedItem?.Tag as string;
            string? rateLimitTag = (RateLimitCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Tag as string;

            return new DownloadOptions
            {
                Url = url,
                OutputFolder = _outputFolder,
                Format = ParseFormatTag(tag),
                DownloadSubtitles = SubtitleCheck.IsChecked == true,
                DownloadPlaylist = PlaylistCheck.IsChecked == true,
                RateLimit = string.IsNullOrEmpty(rateLimitTag) ? null : rateLimitTag
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

        private async Task<MediaMetadata?> TryFetchMetadataAsync(string url, bool includePlaylist, CancellationToken ct)
        {
            try
            {
                var metadata = await _metadataProvider.FetchAsync(_ytdlpPath, url, includePlaylist, ct);
                if (metadata == null)
                    AppendLog("ℹ️  Não foi possível obter uma prévia (metadados); baixando diretamente.");
                return metadata;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                AppendLog($"ℹ️  Não foi possível obter uma prévia (metadados): {ex.Message}");
                return null;
            }
        }

        private void ShowMetadataPreview(MediaMetadata metadata)
        {
            MetadataTitle.Text = metadata.Title;
            MetadataDuration.Text = FormatDuration(metadata.DurationSeconds);

            if (!string.IsNullOrWhiteSpace(metadata.ThumbnailUrl) &&
                Uri.TryCreate(metadata.ThumbnailUrl, UriKind.Absolute, out Uri? thumbnailUri))
            {
                try
                {
                    var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bitmap.UriSource = thumbnailUri;
                    bitmap.EndInit();
                    MetadataThumbnail.Source = bitmap;
                }
                catch (Exception)
                {
                    MetadataThumbnail.Source = null;
                }
            }
            else
            {
                MetadataThumbnail.Source = null;
            }

            MetadataPreviewPanel.Visibility = Visibility.Visible;
        }

        private void HideMetadataPreview()
        {
            MetadataPreviewPanel.Visibility = Visibility.Collapsed;
            MetadataThumbnail.Source = null;
        }

        private void ShowOpenFileButton(string? filePath)
        {
            _lastDownloadedFilePath = filePath;
            OpenDownloadedFileButton.Visibility =
                !string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void HideOpenFileButton()
        {
            _lastDownloadedFilePath = null;
            OpenDownloadedFileButton.Visibility = Visibility.Collapsed;
        }

        private void OpenDownloadedFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_lastDownloadedFilePath) || !File.Exists(_lastDownloadedFilePath))
            {
                AppendLog("⚠️  O arquivo não está mais disponível no caminho esperado.");
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo(_lastDownloadedFilePath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                AppendLog($"❌ Não foi possível abrir o arquivo: {ex.Message}");
            }
        }

        private static string FormatDuration(double? durationSeconds)
        {
            if (durationSeconds is not double seconds || seconds <= 0)
                return "";

            var span = TimeSpan.FromSeconds(seconds);
            return span.Hours > 0
                ? span.ToString(@"h\:mm\:ss")
                : span.ToString(@"m\:ss");
        }

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
                    ProgressEta.Text = FormatEta(info.Eta);
                    break;
                case DownloadLineKind.Destination:
                    // Sinaliza o início de um novo arquivo (ex.: yt-dlp baixa vídeo e áudio como
                    // arquivos separados antes de mesclar) — o percentual precisa poder recomeçar
                    // do zero aqui, diferente da oscilação fina dentro do mesmo arquivo.
                    _maxProgressPercent = 0;
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
                case DownloadLineKind.Retry:
                    _maxProgressPercent = 0;
                    AppendLog($"🔄 {info.RawLine}");
                    SetProgress(0, info.RawLine);
                    break;
            }
        }

        private static string FormatEta(string? eta)
        {
            if (string.IsNullOrWhiteSpace(eta) ||
                eta.Equals("Unknown", StringComparison.OrdinalIgnoreCase) ||
                eta.Equals("NA", StringComparison.OrdinalIgnoreCase))
                return string.Empty;
            return $"ETA {eta}";
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
            DownloadSelectedButton.IsEnabled = !downloading;
            SelectAllButton.IsEnabled = !downloading;
            SelectNoneButton.IsEnabled = !downloading;
            HistoryButton.IsEnabled = !downloading;
            RecentUrlsButton.IsEnabled = !downloading;
            RateLimitCombo.IsEnabled = !downloading;
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
            AutomationProperties.SetName(StatusDot, $"Status: {text}");
        }

        private void AppendLog(string line)
        {
            LogTextBox.AppendText(line + Environment.NewLine);
            LogTextBox.ScrollToEnd();

            if (!string.IsNullOrWhiteSpace(line))
                Serilog.Log.Information(line);
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
