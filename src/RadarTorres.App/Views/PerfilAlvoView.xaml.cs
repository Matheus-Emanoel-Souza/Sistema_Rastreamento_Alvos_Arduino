using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;
using RadarTorres.App.ViewModels;

namespace RadarTorres.App.Views;

/// <summary>
/// Code-behind da tela "Perfis de Alvo". Libera a câmera usada para a foto de referência quando
/// a view sai da árvore visual (mesmo padrão de <see cref="CamerasView"/>) e traduz o arrasto do
/// mouse sobre a foto na marcação do objeto a observar.
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

        Unloaded += (_, _) => _viewModel.Dispose();
    }

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
