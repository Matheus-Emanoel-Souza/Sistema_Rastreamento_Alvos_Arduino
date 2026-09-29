using System.Windows.Controls;
using RadarTorres.App.Services;
using RadarTorres.App.ViewModels;

namespace RadarTorres.App.Views.Shell;

/// <summary>
/// Conteúdo pós-login: barra superior + barra lateral + área de conteúdo navegável (Requisitos
/// 1 e 2). Antes uma <c>Window</c> própria (<c>ShellWindow</c>), agora um <see cref="UserControl"/>
/// hospedado como conteúdo de uma aba do <c>MainWindow</c> (Requisito "abas como em navegador") —
/// uma instância por sessão/aba (Scoped via DI, ver <c>App.OpenNewSession</c>/<c>ShowShellInTab</c>).
/// Dispose do <see cref="ShellViewModel"/> acontece automaticamente quando o <c>IServiceScope</c>
/// da sessão é descartado (o container de DI chama <see cref="System.IDisposable.Dispose"/> em
/// todo serviço Scoped que implementa a interface) — não precisa de código aqui para isso.
/// </summary>
public partial class ShellView : UserControl
{
    public ShellViewModel ViewModel { get; }

    public ShellView(ShellViewModel viewModel, INavigationService navigationService)
    {
        InitializeComponent();

        ViewModel = viewModel;
        DataContext = ViewModel;

        navigationService.SetHost(MainContent);
        ViewModel.NavigateToDefault();
    }
}
