using System;
using System.Windows.Controls;
using RadarTorres.App.Services;
using RadarTorres.App.ViewModels;

namespace RadarTorres.App.Views;

/// <summary>
/// Code-behind do painel principal. A única responsabilidade fora do MVVM é o layout dos
/// cards (posição/tamanho definidos livremente pelo usuário) — é um estado puramente visual do
/// <see cref="Shared.DashboardCanvas"/>, então é carregado/salvo aqui em vez de na ViewModel,
/// seguindo o mesmo princípio já usado em <see cref="ArduinoSettingsView"/> para diálogos de
/// arquivo (a ViewModel só expõe o comando/evento; quem mexe em elementos visuais é a View).
/// </summary>
public partial class PainelPrincipalView : UserControl
{
    private readonly PainelPrincipalViewModel _viewModel;
    private readonly IDashboardLayoutRepository _layoutRepository;
    private bool _layoutLoaded;

    public PainelPrincipalView(PainelPrincipalViewModel viewModel, IDashboardLayoutRepository layoutRepository)
    {
        InitializeComponent();

        _viewModel = viewModel;
        _layoutRepository = layoutRepository;
        DataContext = _viewModel;

        _viewModel.RestoreLayoutRequested += OnRestoreLayoutRequested;
        LayoutCanvas.LayoutChanged += OnLayoutChanged;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        // A View é Singleton (recriada só uma vez) e o painel pode ser reexibido várias vezes
        // pela navegação lateral — só precisamos aplicar o layout salvo na primeira vez que o
        // canvas ganha um tamanho real; nas próximas, o próprio DashboardCanvas já mantém a
        // proporção via SizeChanged.
        if (_layoutLoaded) return;
        _layoutLoaded = true;

        var saved = _layoutRepository.Load();
        if (saved is { Count: > 0 })
        {
            LayoutCanvas.ApplyLayoutSnapshot(saved);
        }
        else
        {
            LayoutCanvas.ResetToDefaultLayout();
        }
    }

    private void OnLayoutChanged(object? sender, EventArgs e)
    {
        _layoutRepository.Save(LayoutCanvas.GetLayoutSnapshot());
    }

    private void OnRestoreLayoutRequested(object? sender, EventArgs e)
    {
        LayoutCanvas.ResetToDefaultLayout();
        _layoutRepository.Save(LayoutCanvas.GetLayoutSnapshot());
    }
}
