using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using RadarTorres.App.ViewModels;

namespace RadarTorres.App.Views;

/// <summary>
/// Code-behind da tela "Configurações". Liga a View ao seu ViewModel e trata o que depende de
/// janela — abrir os Termos de Uso e mostrar o diálogo de salvamento do PDF —, porque abrir
/// <see cref="Window"/> e <see cref="SaveFileDialog"/> é responsabilidade da View no padrão do
/// projeto (mesmo arranjo de <see cref="ObjetosDetectadosView"/>). Toda a lógica de configuração
/// (edição em cópia, validação de limites, gravação em disco) continua em
/// <see cref="ConfiguracoesViewModel"/>.
/// </summary>
public partial class ConfiguracoesView : UserControl
{
    private readonly ConfiguracoesViewModel _viewModel;
    private readonly Func<TermosDeUsoWindow> _criarJanelaTermos;
    private readonly TermosDeUsoViewModel _termosViewModel;

    public ConfiguracoesView(
        ConfiguracoesViewModel viewModel,
        Func<TermosDeUsoWindow> criarJanelaTermos,
        TermosDeUsoViewModel termosViewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        _criarJanelaTermos = criarJanelaTermos;
        _termosViewModel = termosViewModel;
        DataContext = _viewModel;

        _viewModel.AbrirTermosDeUsoSolicitado += OnAbrirTermosDeUso;
        _viewModel.BaixarTermosDeUsoSolicitado += OnBaixarTermosDeUso;

        Unloaded += (_, _) =>
        {
            _viewModel.AbrirTermosDeUsoSolicitado -= OnAbrirTermosDeUso;
            _viewModel.BaixarTermosDeUsoSolicitado -= OnBaixarTermosDeUso;
        };
    }

    private void OnAbrirTermosDeUso(object? sender, EventArgs e)
    {
        TermosDeUsoWindow janela = _criarJanelaTermos();
        janela.Owner = Window.GetWindow(this);
        janela.ShowDialog();
    }

    /// <summary>Atalho para baixar o PDF sem precisar abrir a janela de leitura antes.</summary>
    private async void OnBaixarTermosDeUso(object? sender, string nomeSugerido)
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

        if (dialogo.ShowDialog(Window.GetWindow(this)) != true) return;

        await _termosViewModel.ExportarPdfAsync(dialogo.FileName);

        MessageBox.Show(
            _termosViewModel.MensagemStatus ?? "Operação concluída.",
            "Termos de Uso",
            MessageBoxButton.OK,
            _termosViewModel.StatusSucesso ? MessageBoxImage.Information : MessageBoxImage.Error);
    }
}
