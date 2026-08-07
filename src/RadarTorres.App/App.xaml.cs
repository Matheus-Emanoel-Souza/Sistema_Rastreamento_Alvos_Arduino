using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
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

        // Captura qualquer exceção não tratada (UI thread, threads de fundo e tasks)
        // e exibe uma mensagem clara em vez de deixar o aplicativo fechar sem explicação
        // ou mostrar a tela padrão de erro do Windows. Ver Requisito 10 do instalador.
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;

        try
        {
            var baseDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)!;
            var configPath = Path.Combine(baseDirectory, "appsettings.json");

            if (!File.Exists(configPath))
            {
                // appsettings.json ausente (instalação corrompida/incompleta) — segue com
                // valores padrão em vez de derrubar o aplicativo, e avisa o usuário.
                AppConfig.Current = new AppSettings();
                MessageBox.Show(
                    "O arquivo de configuração 'appsettings.json' não foi encontrado na pasta de instalação.\n" +
                    "O aplicativo será iniciado com valores padrão.\n\n" +
                    "Se o problema persistir, reinstale o RadarTorres.",
                    "RadarTorres - Configuração não encontrada",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var configuration = new ConfigurationBuilder()
                .SetBasePath(baseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .Build();

            var settings = new AppSettings();
            configuration.Bind(settings);
            AppConfig.Current = settings;
        }
        catch (Exception ex)
        {
            // Falha ao ler/interpretar o appsettings.json (ex.: JSON inválido). Mostra uma
            // mensagem compreensível e segue com configuração padrão em vez de crashar.
            AppConfig.Current = new AppSettings();
            MessageBox.Show(
                $"Não foi possível carregar o arquivo de configuração 'appsettings.json'.\n\n" +
                $"Detalhes: {ex.Message}\n\n" +
                "O aplicativo será iniciado com valores padrão.",
                "RadarTorres - Erro ao iniciar",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    /// <summary>Exceções não tratadas geradas na thread de UI (bindings, comandos, eventos).</summary>
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"Ocorreu um erro inesperado e o RadarTorres pode não continuar funcionando corretamente.\n\n" +
            $"Detalhes: {e.Exception.Message}",
            "RadarTorres - Erro inesperado",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        // Marca como tratada para evitar o encerramento abrupto sempre que for seguro
        // continuar (ex.: falha pontual ao processar uma leitura do sensor).
        e.Handled = true;
    }

    /// <summary>Exceções não tratadas geradas em threads de fundo (ex.: leitura serial, timers).</summary>
    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception;
        MessageBox.Show(
            $"Ocorreu um erro crítico no RadarTorres e o aplicativo será encerrado.\n\n" +
            $"Detalhes: {exception?.Message ?? "Erro desconhecido."}",
            "RadarTorres - Erro crítico",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        base.OnExit(e);
    }
}
