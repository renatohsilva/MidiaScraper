using System.Windows;
using MidiaScraper.Services.Logging;
using Serilog;

namespace MidiaScraper
{
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            LoggingSetup.Initialize();
            Log.Information("MídiaScraper iniciado.");

            DispatcherUnhandledException += (_, args) =>
            {
                Log.Error(args.Exception, "Exceção não tratada na UI thread.");
            };

            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Log.Information("MídiaScraper encerrado.");
            LoggingSetup.Shutdown();
            base.OnExit(e);
        }
    }
}
