using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;
using System.Windows.Threading;
using RadarTorres.App.ViewModels;

namespace RadarTorres.App.Views;

/// <summary>
/// Code-behind da tela "Perfis de Alvo". Cuida do que é mesmo responsabilidade da View: o
/// arrasto do mouse que marca o objeto na foto, e o comportamento de janela do modal de cadastro
/// (foco, tecla Esc e confirmação antes de descartar um rascunho).
/// </summary>
/// <remarks>
/// O arrasto fica aqui, e não na ViewModel, porque é entrada de ponteiro — a ViewModel recebe só
/// o retângulo final (<see cref="PerfilAlvoViewModel.MarcarObjeto"/>). Como o Canvas do XAML tem
/// o tamanho exato da foto em pixels e é escalado por um Viewbox, a posição devolvida por
/// <see cref="MouseEventArgs.GetPosition"/> já está em pixels da foto: não há conversão de
/// escala nem cálculo de borda vazia ("letterbox") para errar.
/// </remarks>
public partial class PerfilAlvoView : UserControl
{
    private readonly PerfilAlvoViewModel _viewModel;

    private Point? _inicioArrasto;

    public PerfilAlvoView(PerfilAlvoViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = _viewModel;

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        Unloaded += (_, _) =>
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.Dispose();
        };
    }

    // ---------------------------------------------------------------- Modal: foco e teclado

    /// <summary>
    /// Ao abrir o modal, o foco vai para o primeiro campo; ao fechar, volta para o botão que o
    /// abriu — sem isso o teclado ficaria "perdido" na tela de fundo depois de fechar.
    /// </summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(PerfilAlvoViewModel.ModalAberto)) return;

        if (_viewModel.ModalAberto)
        {
            // Na abertura o conteúdo ainda não foi renderizado; focar no próximo passe de
            // layout é o que garante que o campo já exista.
            Dispatcher.BeginInvoke(DispatcherPriority.Input, () => CampoNome.Focus());
        }
        else
        {
            BotaoCadastrarAlvo.Focus();
        }
    }

    /// <summary>
    /// Esc fecha o modal, respeitando o mesmo cuidado do botão "✕": com dados preenchidos, o
    /// descarte é confirmado antes.
    /// </summary>
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _viewModel.ModalAberto)
        {
            e.Handled = true;
            TentarFecharModal();
            return;
        }

        base.OnPreviewKeyDown(e);
    }

    private void FecharModal_Click(object sender, RoutedEventArgs e) => TentarFecharModal();

    private void TentarFecharModal()
    {
        if (_viewModel.TemDadosPreenchidos)
        {
            MessageBoxResult resposta = MessageBox.Show(
                "Descartar este cadastro? As informações preenchidas e as fotografias vinculadas a ele serão perdidas.",
                "Cadastro de alvo",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.No);

            if (resposta != MessageBoxResult.Yes) return;
        }

        _viewModel.FecharModalCommand.Execute(null);
    }

    // ---------------------------------------------------------------- Marcação do objeto

    private void SelecaoCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_viewModel.PodeMarcarObjeto) return;

        _inicioArrasto = e.GetPosition(SelecaoCanvas);
        SelecaoCanvas.CaptureMouse();

        DesenharSelecaoEmCurso(_inicioArrasto.Value, _inicioArrasto.Value);
        SelecaoEmCurso.Visibility = Visibility.Visible;
    }

    private void SelecaoCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_inicioArrasto is not { } inicio) return;

        DesenharSelecaoEmCurso(inicio, e.GetPosition(SelecaoCanvas));
    }

    private void SelecaoCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_inicioArrasto is not { } inicio) return;

        _inicioArrasto = null;
        SelecaoCanvas.ReleaseMouseCapture();
        SelecaoEmCurso.Visibility = Visibility.Collapsed;

        Point fim = e.GetPosition(SelecaoCanvas);
        Rect area = new(inicio, fim);

        _viewModel.MarcarObjeto(
            (int)Math.Round(area.X),
            (int)Math.Round(area.Y),
            (int)Math.Round(area.Width),
            (int)Math.Round(area.Height));
    }

    private void DesenharSelecaoEmCurso(Point inicio, Point atual)
    {
        Rect area = new(inicio, atual);

        Canvas.SetLeft(SelecaoEmCurso, area.X);
        Canvas.SetTop(SelecaoEmCurso, area.Y);
        SelecaoEmCurso.Width = area.Width;
        SelecaoEmCurso.Height = area.Height;
    }
}
