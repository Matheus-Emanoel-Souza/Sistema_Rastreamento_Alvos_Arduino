using System.Windows.Controls;
using RadarTorres.App.ViewModels;

namespace RadarTorres.App.Views;

/// <summary>
/// Code-behind da tela "Usuários" (exclusiva do Administrador — ver
/// <see cref="Services.IPermissionService.PodeVerMenu"/>). Sem lógica própria: campos de senha
/// usam <see cref="Shared.PasswordRevealBox"/>, que já expõe Binding normal (TwoWay).
/// </summary>
public partial class UsuariosView : UserControl
{
    private readonly UsuariosViewModel _viewModel;

    public UsuariosView(UsuariosViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = _viewModel;

        // View é Singleton (ver App.xaml.cs) — Loaded refaz a cada navegação pela barra
        // lateral, mesmo padrão de ObjetosDetectadosView.
        Loaded += (_, _) => _viewModel.Reload();
    }
}
