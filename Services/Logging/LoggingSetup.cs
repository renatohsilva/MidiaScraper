using System.IO;
using Serilog;

namespace MidiaScraper.Services.Logging
{
    public static class LoggingSetup
    {
        /// <summary>
        /// Configura o Serilog para gravar em %AppData%\MidiaScraper\logs\, um arquivo por dia,
        /// mantendo apenas os últimos 7 dias para não crescer indefinidamente.
        /// </summary>
        public static void Initialize()
        {
            string logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MidiaScraper", "logs");
            Directory.CreateDirectory(logDir);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.File(
                    Path.Combine(logDir, "midiascraper-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();
        }

        public static void Shutdown() => Log.CloseAndFlush();
    }
}
