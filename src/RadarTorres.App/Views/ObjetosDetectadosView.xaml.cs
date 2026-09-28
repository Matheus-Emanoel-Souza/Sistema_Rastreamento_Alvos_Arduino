using System;
using System.Windows.Controls;
using Microsoft.Win32;
using RadarTorres.App.ViewModels;

namespace RadarTorres.App.Views;

/// <summary>
/// Code-behind da tela "Objetos Detectados". Único conteúdo fora do MVVM: o diálogo de arquivo
/// de exportação (mesmo padrão de <see cref="ArduinoSettingsView"/>) — a ViewModel só pede um
/// caminho através de <see cref="ObjetosDetectadosViewModel.ExportRequested"/> e recebe de
/// volta o que o usuário escolheu. Sem importação de propósito — ver comentário na ViewModel.
/// </summary>
public partial class ObjetosDetectadosView : UserControl
{
    private readonly ObjetosDetectadosViewModel _viewModel;

    public ObjetosDetectadosView(ObjetosDetectadosViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = _viewModel;

        _viewModel.ExportRequested += OnExportRequested;

        // View é Singleton (ver App.xaml.cs) — Loaded refaz a cada navegação pela barra
        // lateral, mesmo padrão de recarregamento já usado em MonitoramentoView/PainelPrincipalView.
        Loaded += (_, _) => _viewModel.Reload();
    }

    private void OnExportRequested(object? sender, string formato)
    {
        var dialog = new SaveFileDialog
        {
            Title = $"Exportar Objetos Detectados ({formato.ToUpperInvariant()})",
            Filter = FilterFor(formato),
            FileName = $"objetos_detectados.{formato}",
        };

        if (dialog.ShowDialog() == true)
        {
            _viewModel.ExportTo(formato, dialog.FileName);
        }
    }

    private static string FilterFor(string formato) => formato switch
    {
        "csv" => "Arquivo CSV (*.csv)|*.csv",
        "xml" => "Arquivo XML (*.xml)|*.xml",
        "pdf" => "Documento PDF (*.pdf)|*.pdf",
        _ => "Todos os arquivos (*.*)|*.*"
    };
}
