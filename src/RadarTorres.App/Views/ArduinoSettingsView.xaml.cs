using System;
using System.Collections.Specialized;
using System.Windows.Controls;
using Microsoft.Win32;
using RadarTorres.App.ViewModels;

namespace RadarTorres.App.Views;

/// <summary>
/// Code-behind da aba Configurações do Arduino. Contém apenas o mínimo que exige acesso
/// direto ao WPF (diálogos de arquivo, rolagem automática do console) — toda regra de
/// negócio (detecção do CLI, compilação, monitor serial) vive em <see cref="ArduinoSettingsViewModel"/>.
/// A View é Singleton via DI (mesmo padrão de <see cref="MonitoramentoView"/>) e vive durante
/// toda a sessão, então as inscrições feitas aqui no construtor não precisam ser desfeitas em
/// cada navegação — apenas uma vez, como as demais telas do projeto.
/// </summary>
public partial class ArduinoSettingsView : UserControl
{
    private readonly ArduinoSettingsViewModel _viewModel;

    public ArduinoSettingsView(ArduinoSettingsViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = _viewModel;

        _viewModel.BrowseCliPathRequested += OnBrowseCliPathRequested;
        _viewModel.BrowseSketchRequested += OnBrowseSketchRequested;
        _viewModel.MonitorLines.CollectionChanged += OnMonitorLinesChanged;
    }

    private void OnBrowseCliPathRequested(object? sender, EventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Selecionar arduino-cli.exe",
            Filter = "arduino-cli.exe|arduino-cli.exe|Executáveis (*.exe)|*.exe|Todos os arquivos (*.*)|*.*",
            CheckFileExists = true,
        };

        if (dialog.ShowDialog() == true)
        {
            _viewModel?.SetCliPathFromDialog(dialog.FileName);
        }
    }

    private void OnBrowseSketchRequested(object? sender, EventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Selecionar sketch Arduino (.ino)",
            Filter = "Sketch Arduino (*.ino)|*.ino|Todos os arquivos (*.*)|*.*",
            CheckFileExists = true,
        };

        if (dialog.ShowDialog() == true)
        {
            _viewModel?.SetSketchPathFromDialog(dialog.FileName);
        }
    }

    /// <summary>Rola o console do monitor serial para o final a cada nova linha, quando "Rolagem automática" está ativa.</summary>
    private void OnMonitorLinesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_viewModel.AutoScroll)
        {
            MonitorScrollViewer.ScrollToEnd();
        }
    }
}
