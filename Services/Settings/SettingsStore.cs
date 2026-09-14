using System.IO;
using System.Text.Json;
using MidiaScraper.Models;

namespace MidiaScraper.Services.Settings
{
    public class SettingsStore
    {
        private readonly string _filePath;

        public SettingsStore()
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MidiaScraper");
            Directory.CreateDirectory(dir);
            _filePath = Path.Combine(dir, "settings.json");
        }

        /// <summary>
        /// Loads persisted settings. A missing file (first run) or a corrupted/unreadable one both
        /// fall back to defaults instead of crashing the app; the latter is reported via <paramref name="log"/>.
        /// </summary>
        public async Task<AppSettings> LoadAsync(Action<string>? log = null)
        {
            if (!File.Exists(_filePath))
                return new AppSettings();

            try
            {
                string json = await File.ReadAllTextAsync(_filePath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            catch (Exception ex)
            {
                log?.Invoke($"⚠️  Configurações corrompidas ou ilegíveis ({ex.Message}); usando padrões.");
                return new AppSettings();
            }
        }

        public async Task SaveAsync(AppSettings settings)
        {
            string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_filePath, json);
        }
    }
}
