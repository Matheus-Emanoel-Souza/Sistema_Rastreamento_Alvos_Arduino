using System.IO;
using System.Windows;
using Microsoft.Extensions.Configuration;
using RadarTorres.App.Configuration;

namespace RadarTorres.App;

/// <summary>
/// Ponto de entrada da aplicação. Responsável por carregar o arquivo de configuração
/// (appsettings.json) e disponibilizá-lo de forma estática e tipada para o restante
/// do software através de <see cref="AppConfig.Current"/>.
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)!)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .Build();

        var settings = new AppSettings();
        configuration.Bind(settings);
        AppConfig.Current = settings;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        base.OnExit(e);
    }
}
