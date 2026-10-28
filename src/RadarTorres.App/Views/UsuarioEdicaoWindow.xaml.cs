using System.Windows;
using RadarTorres.App.ViewModels;
using RadarTorres.App.Views.Shared;

namespace RadarTorres.App.Views;

/// <summary>
/// Code-behind do modal de cadastro/edição de usuário. Sem lógica de negócio: compartilha a
/// <see cref="UsuariosViewModel"/> da tela, de modo que validação e gravação continuam
/// acontecendo em <c>SalvarCommand</c>. A única responsabilidade aqui é fechar a janela quando a
/// gravação der certo (ver <see cref="JanelaModal.FecharQuando"/>) — em caso de erro o modal
/// permanece aberto exibindo a mensagem de validação.
/// </summary>
public partial class UsuarioEdicaoWindow : Window
{
    public UsuarioEdicaoWindow(UsuariosViewModel viewModel)
    {
        InitializeComponent();

        DataContext = viewModel;
        JanelaModal.FecharQuando(this, nameof(UsuariosViewModel.SucessoFormulario));
    }
}
