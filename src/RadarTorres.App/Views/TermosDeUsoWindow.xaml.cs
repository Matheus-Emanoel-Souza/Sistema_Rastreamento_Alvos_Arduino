using System.Windows;
using Microsoft.Win32;
using RadarTorres.App.ViewModels;

namespace RadarTorres.App.Views;

/// <summary>
/// Janela de leitura dos Termos de Uso. Sem lógica própria além de mostrar o
/// <see cref="SaveFileDialog"/> quando a ViewModel pede um caminho para o PDF — mesmo padrão de
/// <see cref="ObjetosDetectadosView"/>, que também mantém o diálogo de arquivo fora da ViewModel.
/// </summary>
public partial class TermosDeUsoWindow : Window
{
    private readonly TermosDeUsoViewModel _viewModel;

    public TermosDeUsoWindow(TermosDeUsoViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = _viewModel;

        _viewModel.SalvarPdfSolicitado += OnSalvarPdfSolicitado;
        Closed += (_, _) => _viewModel.SalvarPdfSolicitado -= OnSalvarPdfSolicitado;
    }

    private async void OnSalvarPdfSolicitado(object? sender, string nomeSugerido)
    {
        var dialogo = new SaveFileDialog
        {
            Title = "Salvar os Termos de Uso",
            FileName = nomeSugerido,
            DefaultExt = ".pdf",
            Filter = "Documento PDF (*.pdf)|*.pdf",
            AddExtension = true,
            OverwritePrompt = true
        };

        if (dialogo.ShowDialog(this) != true) return;

        await _viewModel.ExportarPdfAsync(dialogo.FileName);
    }

    private void Fechar_Click(object sender, RoutedEventArgs e) => Close();
}
